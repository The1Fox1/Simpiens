using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Hosting
{
    /// <summary>
    /// The economy soak, headless (Epic CORE C10): the outpost for three game days under one kind of player, sampled at each
    /// caravan visit, judged against that player's pace band (docs/economy-model.md §7). Nothing is injected except the
    /// player's prices, set by a typed command before the first tick. Epic 10 Part 2's T4.4 (D28) set the horizon in days
    /// for the 24-hour day; before it the soak played 30 sim-minutes, sampled every sim-minute.
    /// </summary>
    public static class EconomySoak
    {
        public const long HorizonTicks = 3 * DayClock.TicksPerDay;
        public const long SampleIntervalTicks = 6 * DayClock.TicksPerHour; // one caravan visit (SupplyTuning)
        public const float StarvingHunger = 95f;
        public const uint StarvingTicks = 2 * DayClock.TicksPerHour;

        /// <summary>Samples a day: the summary's treasury is shown at the end of each day.</summary>
        private const int SamplesPerDay = (int)(DayClock.TicksPerDay / SampleIntervalTicks);

        // Pace bands (docs/economy-model.md §7), from T4.4's measurement over seeds 1-15 and 20261009 (docs/time-model.md
        // §2): the fair player ended day 3 at 771-1,277g with trust risen for 10-12; the greedy one raised trust for 0-3; the
        // generous one went broke on every seed; the guild player ended at 578-998g, paid every wage, and brought 1-2 hauls
        // home on every seed whose guild filled 8 game hours before the end. Before the 24-hour day: fair and guild
        // 1,000-2,000g at 30 minutes (the producer, 2026-10-25, and G6), greedy trust up for 2 at most.
        public const uint FairTreasuryMin = 600;
        public const uint FairTreasuryMax = 1600;
        public const int FairTrustUpMin = 8;
        /// <summary>
        /// Raised from 3 at Epic 10 Part 3's K9 (F9, a default for the producer): with skills the same 16 seeds measured 0-6
        /// (4 on seed 2, 6 on seed 12, 0-3 on the rest), the greedy player's trust still far below nothing on every one.
        /// </summary>
        public const int GreedyTrustUpMax = 6;
        public const int GenerousTrustUpPeakMin = 8;
        public const uint GuildTreasuryMin = 400;
        public const uint GuildTreasuryMax = 1200;
        public const int GuildTrustUpMin = 8;

        /// <summary>
        /// The guild profile's size: Part 1's guild, one party of four (G6's band, D14). The guildhall's guild of eight
        /// (Epic 10 Part 2, W5) gets its band at W6.
        /// </summary>
        public const int GuildProfileSize = Simpiens.Intervention.DispatchExpeditionCommand.MaxMembers;

        /// <summary>
        /// The crafting profile (Epic 10 Part 2, W6.3): fair prices, the forge making respirators and the still draughts,
        /// and the guildhall's guild of eight in two parties sent together, keeping a reserve (D67). Its band is proposed by
        /// exception from W6.3's measurement over seeds 1-15 and 20261009 (D68): treasuries at day 3 of 1-390g against the
        /// fair player's 771-1,277g (F14), so the band asks for the loop's integrity, not a treasury. Since Epic 10 Part 3's F3
        /// (option A, the developer's 2026-11-10) the integrity is the game's own rule, judged across seeds: the stations make
        /// goods on most of them (<see cref="CraftingMadeShare"/>), and on few does anyone quit for want of pay
        /// (<see cref="CraftingUnpaidQuitShare"/>).
        /// </summary>
        public const int CraftingGuildSize = 8;
        public const uint CraftingGuildReserve = 200;
        public const uint CraftingTreasuryMin = 0;
        public const uint CraftingTreasuryMax = 1200;
        public const int CraftingTrustUpMin = 8;

        /// <summary>
        /// The share of seeds on which the crafting profile's stations make goods within the horizon (Epic 10 Part 3, F3's
        /// option A): the forge's respirators wait on glowstone the expeditions bring home near the horizon's edge, so a seed
        /// alone is timing; measured 18 of seeds 1-20 with hunting off and on.
        /// </summary>
        public const float CraftingMadeShare = 0.8f;

        /// <summary>
        /// The share of seeds on which someone may quit the crafting profile's loop for want of pay (Epic 10 Part 3, F3's
        /// option A): a stall that has just bought its salvage can be empty at a caravan's visit; measured on seeds 1-20 and
        /// 20261009, one seed with hunting off (13) and two with it on (2 and 3). 0.25 since Epic 10 Part 3's K10.2a (F12): the
        /// rate sits at the old 0.15 by itself (6 of seeds 1-40 at K10.1b, 8 with the Matriarch; seeds 21-40 alike at 3), and
        /// which seeds quit moves with any change of the creatures' timing.
        /// </summary>
        public const float CraftingUnpaidQuitShare = 0.25f;

        /// <summary>
        /// The hunting profile (Epic 10 Part 3, K11.2): fair prices, the forge making blades and ring coats and the still
        /// draughts, the scripted hunt player posting on a lair the bold would brave, and a guild of four clearing the first
        /// charted lair with two draughts each, every player keeping a reserve (D71's). Its band is proposed by exception from
        /// K11.2's measurement over seeds 1-20 (D73): the loop's integrity at day 3 (a treasury, trust rising for most, nobody
        /// starving, no quit for pay: measured 66-397g, 11-12 up, none, none) and few deaths (measured none in eight days). The
        /// fighting itself starts at the horizon's edge (0-2 kills in three days; 0-8 in eight), so kills aren't banded.
        /// </summary>
        public const uint HuntingReserve = 150;
        public const uint HuntingTreasuryMin = 0;
        public const uint HuntingTreasuryMax = 1200;
        public const int HuntingTrustUpMin = 8;
        public const int HuntingDeathsMax = 1;

        /// <summary>A guild full this long before the horizon has had time to send a party and see it home.</summary>
        public const long GuildSpareTicks = 8 * DayClock.TicksPerHour;

        /// <summary>The soak's seeds.</summary>
        public static readonly ulong[] DefaultSeeds = { 20261009UL, 1UL, 2UL };
        public static readonly string[] Profiles = { "fair", "greedy", "generous", "guild", "crafting", "hunting" };

        /// <summary>A soak player: the prices it sets (item, price), as the typed command it sends on the first tick.</summary>
        public readonly struct Player
        {
            public readonly string Name;
            public readonly UpdateStallPricesCommand Prices;
            public readonly bool SendsCommand;

            public Player(string name, in UpdateStallPricesCommand prices, bool sendsCommand)
            {
                Name = name;
                Prices = prices;
                SendsCommand = sendsCommand;
            }

            /// <summary>"Ration 15g, IronOre 20g, …".</summary>
            public string PriceText()
            {
                var parts = new List<string>(Prices.Count);
                for (int i = 0; i < Prices.Count; i++) parts.Add($"{ItemCatalog.NameOf(Prices[i].Item)} {Prices[i].Price}g");
                return string.Join(", ", parts);
            }
        }

        /// <summary>
        /// The player the soak plays: fair keeps the session's prices (rations, ore, herbs, hazard gear); greedy, generous
        /// and keen set their own, as shares of the fair ones, so a profile means the same on any economy (Epic 10 Part 2,
        /// T4.4: on Part 1's economy the shares gave exactly its fixed prices); guild keeps fair prices and runs a
        /// guild (Epic 10 G6, D14: the scripted <see cref="GuildPlayer"/> recruits four, outfits them and sends them to the
        /// Drowned Gallery whenever its seam is back). The profiles are content: they name the goods they price.
        /// </summary>
        public static Player Resolve(string name, EconomyTuning economy)
        {
            UpdateStallPricesCommand Prices(float ration, float ore, float herb, float gear) => new UpdateStallPricesCommand(default)
                .With(ItemType.Ration, Share(economy, ItemType.Ration, ration)).With(ItemType.IronOre, Share(economy, ItemType.IronOre, ore))
                .With(ItemType.MedicinalHerb, Share(economy, ItemType.MedicinalHerb, herb)).With(ItemType.HazardGear, Share(economy, ItemType.HazardGear, gear));
            switch ((name ?? "fair").Trim().ToLowerInvariant())
            {
                case "fair": return new Player("fair", Prices(1f, 1f, 1f, 1f), sendsCommand: false);
                case "guild": return new Player("guild", Prices(1f, 1f, 1f, 1f), sendsCommand: false);
                case "crafting": return new Player("crafting", Prices(1f, 1f, 1f, 1f), sendsCommand: false);
                case "hunting": return new Player("hunting", Prices(1f, 1f, 1f, 1f), sendsCommand: false); // Epic 10 Part 3, K11.2
                // Rations 30g, ore 2g, herbs 3g, gear 60g
                case "greedy": return new Player("greedy", Prices(2f, 0.5f, 0.5f, 2f), sendsCommand: true);
                // Rations 5g, ore 9g, herbs 10g, gear 20g
                case "generous": return new Player("generous", Prices(1f / 3f, 1.75f, 1.6f, 2f / 3f), sendsCommand: true);
                // Slightly greedy, for the drifters' comparison (Epic 10 G1); it has no pace band of its own. Rations 20g,
                // ore 4g, herbs 5g, gear 35g
                case "keen": return new Player("keen", Prices(4f / 3f, 0.9f, 0.88f, 7f / 6f), sendsCommand: true);
                default: throw new ArgumentException($"Unknown soak profile '{name}' (fair, greedy, generous, guild, crafting, hunting, keen).", nameof(name));
            }
        }

        private static ushort Share(EconomyTuning economy, ItemType item, float share)
        {
            return (ushort)SimMath.RoundToInt(economy.Terms(item).StallPrice * share);
        }

        public struct Row
        {
            public long Tick;
            public uint Treasury;
            public bool InArrears;
            public int CrawlerGold, StarvingEpisodes, Collapses;
            public float TrustMean;
            public int TrustMin, TrustMax, TrustUp, TrustDown;
        }

        public sealed class Result
        {
            public Player Player;
            public ulong Seed;
            public readonly List<Row> Rows = new List<Row>(32);
            public long ObservedTicks;
            public int ConservationViolations, WorstDiscrepancy;
            public bool PricesApplied;
            public string PricesSeen = "never sampled";

            /// <summary>Gold the drifters brought and took away (Epic 10 G1): the faucet and sink of that name.</summary>
            public uint DrifterPurses, DrifterDepartures;

            /// <summary>
            /// The guild profile's (Epic 10 G6): members at the end and signed in all, when the guild filled (or -1), the
            /// worst run of unpaid visits, wages and shares paid, and expeditions home with glowstone.
            /// </summary>
            public int Members, Signed, WorstUnpaid, ExpeditionsHome;

            /// <summary>
            /// Guild members and artisans who quit for want of pay (Epic 10 Part 3, F3's option A): gone from their roster with
            /// one unpaid visit short of the quitting line, on the visit that reached it. A single unpaid visit is a warning the
            /// game recovers from; a quit is the loop failing.
            /// </summary>
            public int UnpaidQuits;

            /// <summary>The crafting profile's (Epic 10 Part 2, W6.3): stations standing, artisans at work at the end, and what the stations made.</summary>
            public int StationsBuilt, Artisans;
            public uint Made;

            /// <summary>The hunting profile's (Epic 10 Part 3, K11.2): creatures slain, hunts paid, rescues made and deaths, over the horizon.</summary>
            public int Kills, HuntsPaid, Rescues, Deaths;
            public long GuildFullTick = -1;
            public uint Wages, WageGold, Shares, ShareGold;

            public Row Last => Rows[Rows.Count - 1];

            /// <summary>Everything that fails the soak: integrity first, then the player's pace band. Empty when it holds.</summary>
            public List<string> Failures()
            {
                var failures = new List<string>();
                if (ObservedTicks < HorizonTicks) failures.Add($"ended at t{ObservedTicks}, before the horizon t{HorizonTicks}");
                if (ConservationViolations > 0) failures.Add($"ledger conservation violated {ConservationViolations} times (worst {WorstDiscrepancy}g)");
                if (!PricesApplied) failures.Add($"prices {Player.PriceText()} not applied (stall {PricesSeen})");
                if (Rows.Count != HorizonTicks / SampleIntervalTicks + 1) failures.Add($"{Rows.Count} samples, expected {HorizonTicks / SampleIntervalTicks + 1}");
                if (failures.Count > 0 || Rows.Count == 0) return failures;

                var last = Last;
                uint minTreasury = uint.MaxValue;
                int peakTrustUp = 0;
                float lowestTrustMean = float.MaxValue;
                bool everInArrears = false;
                foreach (var row in Rows)
                {
                    if (row.Treasury < minTreasury) minTreasury = row.Treasury;
                    if (row.TrustUp > peakTrustUp) peakTrustUp = row.TrustUp;
                    if (row.TrustMean < lowestTrustMean) lowestTrustMean = row.TrustMean;
                    everInArrears |= row.InArrears;
                }
                switch (Player.Name)
                {
                    case "fair":
                        if (last.Treasury < FairTreasuryMin || last.Treasury > FairTreasuryMax) failures.Add($"fair: treasury {last.Treasury}g at day 3, outside {FairTreasuryMin}-{FairTreasuryMax}g");
                        if (last.TrustUp < FairTrustUpMin) failures.Add($"fair: trust rose for only {last.TrustUp} crawlers (need {FairTrustUpMin})");
                        if (last.StarvingEpisodes != 0) failures.Add($"fair: {last.StarvingEpisodes} starving episodes (need 0)");
                        break;
                    case "greedy":
                        if (last.TrustUp > GreedyTrustUpMax) failures.Add($"greedy: trust rose for {last.TrustUp} crawlers (at most {GreedyTrustUpMax})");
                        // At its lowest, as generous is judged at its peak: once greed has emptied the stall nobody
                        // trades, and trust drifts back to neutral by minute 30 (Epic 10 G0)
                        if (!(lowestTrustMean < 0f)) failures.Add($"greedy: trust mean never fell below 0 (lowest {lowestTrustMean:F1})");
                        break;
                    case "guild":
                        if (last.Treasury < GuildTreasuryMin || last.Treasury > GuildTreasuryMax) failures.Add($"guild: treasury {last.Treasury}g at day 3, outside {GuildTreasuryMin}-{GuildTreasuryMax}g");
                        if (last.TrustUp < GuildTrustUpMin) failures.Add($"guild: trust rose for only {last.TrustUp} crawlers (need {GuildTrustUpMin})");
                        if (last.StarvingEpisodes != 0) failures.Add($"guild: {last.StarvingEpisodes} starving episodes (need 0)");
                        if (UnpaidQuits > 0) failures.Add($"guild: {UnpaidQuits} quit for want of pay (worst run {WorstUnpaid} unpaid)");
                        if (GuildFullTick >= 0 && GuildFullTick <= HorizonTicks - GuildSpareTicks && ExpeditionsHome == 0)
                        {
                            failures.Add($"guild: full at t{GuildFullTick}, but no expedition came home with glowstone");
                        }
                        break;
                    case "crafting":
                        if (last.Treasury < CraftingTreasuryMin || last.Treasury > CraftingTreasuryMax) failures.Add($"crafting: treasury {last.Treasury}g at day 3, outside {CraftingTreasuryMin}-{CraftingTreasuryMax}g");
                        if (last.TrustUp < CraftingTrustUpMin) failures.Add($"crafting: trust rose for only {last.TrustUp} crawlers (need {CraftingTrustUpMin})");
                        if (last.StarvingEpisodes != 0) failures.Add($"crafting: {last.StarvingEpisodes} starving episodes (need 0)");
                        // Pay and makes are judged across seeds (CraftingMadeShare, CraftingUnpaidQuitShare): a seed alone is timing
                        if (StationsBuilt == 0) failures.Add($"crafting: no station built");
                        break;
                    case "hunting":
                        if (last.Treasury < HuntingTreasuryMin || last.Treasury > HuntingTreasuryMax) failures.Add($"hunting: treasury {last.Treasury}g at day 3, outside {HuntingTreasuryMin}-{HuntingTreasuryMax}g");
                        if (last.TrustUp < HuntingTrustUpMin) failures.Add($"hunting: trust rose for only {last.TrustUp} crawlers (need {HuntingTrustUpMin})");
                        if (last.StarvingEpisodes != 0) failures.Add($"hunting: {last.StarvingEpisodes} starving episodes (need 0)");
                        if (UnpaidQuits > 0) failures.Add($"hunting: {UnpaidQuits} quit for want of pay (worst run {WorstUnpaid} unpaid)");
                        if (Deaths > HuntingDeathsMax) failures.Add($"hunting: {Deaths} deaths (at most {HuntingDeathsMax})");
                        break;
                    case "generous":
                        if (!(minTreasury == 0 || everInArrears)) failures.Add($"generous: the stall never went broke (lowest treasury {minTreasury}g)");
                        if (peakTrustUp < GenerousTrustUpPeakMin) failures.Add($"generous: trust rose for at most {peakTrustUp} crawlers (need {GenerousTrustUpPeakMin} at its peak)");
                        break;
                }
                return failures;
            }

            public string Summary()
            {
                var last = Rows.Count > 0 ? Last : default;
                var checkpoints = new List<string>(4);
                for (int day = 0; day * SamplesPerDay <= HorizonTicks / SampleIntervalTicks; day++)
                {
                    int sample = day * SamplesPerDay;
                    checkpoints.Add($"D{day} {(Rows.Count > sample ? Rows[sample].Treasury.ToString(CultureInfo.InvariantCulture) : "?")}");
                }
                string guild = Player.Name == "guild" || Player.Name == "crafting"
                    ? $"; guild {Members} (signed {Signed}, full at t{GuildFullTick}), wages {Wages} ({WageGold}g), worst unpaid {WorstUnpaid}, quit unpaid {UnpaidQuits}, expeditions home {ExpeditionsHome}, shares {ShareGold}g"
                    : string.Empty;
                if (Player.Name == "crafting") guild += $"; stations {StationsBuilt}, artisans {Artisans}, made {Made}";
                if (Player.Name == "hunting") guild = $"; guild {Members} (full at t{GuildFullTick}), quit unpaid {UnpaidQuits}; kills {Kills}, hunts paid {HuntsPaid}, rescues {Rescues}, deaths {Deaths}";
                return $"{Player.Name,-8} seed {Seed,-9} treasury {string.Join(" ", checkpoints)}g; trust mean {last.TrustMean:F1}, up {last.TrustUp}, down {last.TrustDown}; starving {last.StarvingEpisodes}, collapses {last.Collapses}; ledger violations {ConservationViolations}{guild}";
            }

            public void WriteCsv(string path)
            {
                var sb = new StringBuilder("profile,seed,tick,game_hour,treasury,in_arrears,crawler_gold,trust_mean,trust_min,trust_max,trust_up,trust_down,starving_episodes,collapses\n");
                foreach (var r in Rows)
                {
                    sb.Append(Player.Name).Append(',').Append(Seed).Append(',').Append(r.Tick).Append(',')
                      .Append((r.Tick / (double)DayClock.TicksPerHour).ToString("F1", CultureInfo.InvariantCulture)).Append(',')
                      .Append(r.Treasury).Append(',').Append(r.InArrears ? 1 : 0).Append(',').Append(r.CrawlerGold).Append(',')
                      .Append(r.TrustMean.ToString("F2", CultureInfo.InvariantCulture)).Append(',').Append(r.TrustMin).Append(',').Append(r.TrustMax).Append(',')
                      .Append(r.TrustUp).Append(',').Append(r.TrustDown).Append(',').Append(r.StarvingEpisodes).Append(',').Append(r.Collapses).Append('\n');
                }
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, sb.ToString());
            }
        }

        /// <summary>
        /// Plays the outpost with <paramref name="seed"/> for <paramref name="horizonTicks"/> as one kind of player.
        /// Without <paramref name="drifters"/> the outpost is closed, as it was before Epic 10 G1.
        /// </summary>
        /// <summary>
        /// The scripted players a profile plays with, acting only through typed commands (the guild profile's D14, the
        /// crafting profile's Epic 10 Part 2 W6.3, the hunting profile's Part 3 K11.2); the guild's player, or null for a
        /// profile without one. Prices are the caller's. Shared with measurements that play a profile's players (Epic 11, S0.3).
        /// </summary>
        public static GuildPlayer AttachPlayers(LocalSimulation sim, string profile)
        {
            GuildPlayer guildPlayer = null;
            if (profile == "guild")
            {
                guildPlayer = new GuildPlayer(sim, targetSize: GuildProfileSize);
                guildPlayer.SendExpeditions(Floor1Layout.DrownedGallerySectorId, Floor1Layout.GallerySeamYield);
            }
            // The crafting profile's players (Epic 10 Part 2, W6.3, D67): both stations, and the guildhall's guild of eight
            if (profile == "crafting")
            {
                new WorkshopPlayer(sim, Simpiens.Simulation.Items.StationKind.Forge, ItemType.Respirator);
                new WorkshopPlayer(sim, Simpiens.Simulation.Items.StationKind.Still, ItemType.HealingDraught);
                guildPlayer = new GuildPlayer(sim, targetSize: CraftingGuildSize, reserve: CraftingGuildReserve);
                guildPlayer.SendExpeditions(Floor1Layout.DrownedGallerySectorId, Floor1Layout.GallerySeamYield);
                guildPlayer.SendSecondParty(Floor1Layout.GlowCavesSectorId, 3, together: true);
            }
            // The hunting profile's players (Epic 10 Part 3, K11.2): every fighting player at once, each keeping a reserve
            if (profile == "hunting")
            {
                new WorkshopPlayer(sim, Simpiens.Simulation.Items.StationKind.Forge, ItemType.IronBlade, batch: 4, reserve: HuntingReserve).Also(ItemType.RingCoat, keep: 4);
                new WorkshopPlayer(sim, Simpiens.Simulation.Items.StationKind.Still, ItemType.HealingDraught, batch: 2, reserve: HuntingReserve);
                new HuntPlayer(sim, Simpiens.Simulation.Creatures.CreatureKind.None);
                guildPlayer = new GuildPlayer(sim, targetSize: GuildProfileSize, reserve: HuntingReserve);
                guildPlayer.SendClears(new[] { Simpiens.Simulation.Creatures.CreatureKind.None }, Simpiens.Simulation.Guild.RetreatRule.Steady, draughts: 2, stockpile: 8);
            }
            return guildPlayer;
        }

        public static Result Run(ulong seed, string profile, long horizonTicks = HorizonTicks, bool drifters = true)
        {
            using var sim = LocalSimulation.Create(seed, drifters ? null : services =>
            {
                services.RemoveAll<ISessionDefinition>();
                services.AddSingleton<ISessionDefinition>(new OutpostSession(OutpostSession.DefaultCrawlerCount, EconomyTuning.Default.WithDrifters(DrifterTuning.None)));
            });
            sim.Respawn(OutpostSession.SessionId);
            var manager = sim.Simulation;
            var result = new Result { Player = Resolve(profile, manager.Economy), Seed = seed };

            // The player's prices, as a typed command applied on the first tick
            if (result.Player.SendsCommand && manager.ActiveStalls.Count > 0)
            {
                var interventions = (IPlayerInterventionService)sim.Services.GetService(typeof(IPlayerInterventionService));
                interventions.QueueInfluence(new InfluenceEvent(
                    "EconomySoak:" + result.Player.Name,
                    InfluenceCommandType.UpdateStallPrices,
                    updateStallPrices: result.Player.Prices));
            }

            // The profile's scripted players, acting only through commands
            GuildPlayer guildPlayer = AttachPlayers(sim, result.Player.Name);
            int guildSize = result.Player.Name == "crafting" ? CraftingGuildSize : GuildProfileSize;
            var told = new List<Simpiens.Narrative.ChronicleEvent>(16);
            int huntsOpen = 0;
            var guild = manager.Guild;
            var unpaidNow = new Dictionary<EntityId, (int Unpaid, int Max)>();
            var unpaidLast = new Dictionary<EntityId, (int Unpaid, int Max)>();
            bool partyOut = false;
            uint sharesSeen = 0;

            var crawlers = sim.Host.CrawlerStates;
            var trustAtStart = new Dictionary<EntityId, int>();
            var hungrySince = new Dictionary<EntityId, long>();
            var starvingNow = new HashSet<EntityId>();
            var collapsedNow = new HashSet<EntityId>();
            int starvingEpisodes = 0, collapses = 0;
            long start = sim.Clock.CurrentTick;

            while (true)
            {
                long rel = sim.Clock.CurrentTick - start;
                result.ObservedTicks = rel;

                // Integrity and the lifecycle counters, every tick
                uint stallGold = 0;
                for (int i = 0; i < manager.ActiveStalls.Count; i++) stallGold += manager.ActiveStalls[i].TreasuryGold;
                uint crawlerGold = 0;
                for (int i = 0; i < crawlers.Count; i++)
                {
                    var state = crawlers[i];
                    if (state.Inventory != null) crawlerGold += (uint)state.Inventory.Gold;
                    if (state.IsExhaustionCollapsed) { if (collapsedNow.Add(state.AgentId)) collapses++; }
                    else collapsedNow.Remove(state.AgentId);
                    if (state.Needs.Hunger >= StarvingHunger)
                    {
                        if (!hungrySince.TryGetValue(state.AgentId, out long since)) hungrySince[state.AgentId] = since = rel;
                        if (rel - since >= StarvingTicks && starvingNow.Add(state.AgentId)) starvingEpisodes++;
                    }
                    else
                    {
                        hungrySince.Remove(state.AgentId);
                        starvingNow.Remove(state.AgentId);
                    }
                }
                if (manager.EconomyLedger != null && !manager.EconomyLedger.ValidateConservation(stallGold, crawlerGold, out int discrepancy))
                {
                    result.ConservationViolations++;
                    if (Math.Abs(discrepancy) > Math.Abs(result.WorstDiscrepancy)) result.WorstDiscrepancy = discrepancy;
                }

                if (rel % SampleIntervalTicks == 0)
                {
                    if (rel > 0 && !result.PricesApplied && manager.ActiveStalls.Count > 0)
                    {
                        var stall = manager.ActiveStalls[0];
                        var seen = new List<string>(result.Player.Prices.Count);
                        bool applied = true;
                        for (int i = 0; i < result.Player.Prices.Count; i++)
                        {
                            var wanted = result.Player.Prices[i];
                            ushort price = stall.TryGetGood(wanted.Item, out var good) ? good.Price : (ushort)0;
                            seen.Add($"{ItemCatalog.NameOf(wanted.Item)} {price}g");
                            applied &= price == wanted.Price;
                        }
                        result.PricesSeen = string.Join(", ", seen);
                        result.PricesApplied = applied;
                    }
                    result.Rows.Add(Sample(manager, crawlers, rel, sim.Clock.CurrentTick, crawlerGold, trustAtStart, starvingEpisodes, collapses));
                }

                // Who is unpaid now, and who left at the quitting line's eve (F3's option A)
                unpaidNow.Clear();
                for (int i = 0; i < guild.Members.Count; i++) unpaidNow[guild.Members[i].CrawlerId] = (guild.Members[i].UnpaidVisits, manager.Economy.Guild.MaxUnpaidVisits);
                var artisans = manager.Workshops?.Artisans;
                for (int i = 0; artisans != null && i < artisans.Count; i++) unpaidNow[artisans[i].CrawlerId] = (artisans[i].UnpaidVisits, manager.Economy.Workshops.MaxUnpaidVisits);
                foreach (var pair in unpaidLast)
                    if (!unpaidNow.ContainsKey(pair.Key) && pair.Value.Unpaid + 1 >= pair.Value.Max) result.UnpaidQuits++;
                foreach (var pair in unpaidNow) result.WorstUnpaid = Math.Max(result.WorstUnpaid, pair.Value.Unpaid);
                (unpaidLast, unpaidNow) = (unpaidNow, unpaidLast);

                if (guildPlayer != null)
                {
                    if (result.GuildFullTick < 0 && guild.Members.Count >= guildSize) result.GuildFullTick = rel;
                    if (guild.HasExpedition) partyOut = true;
                    else if (partyOut)
                    {
                        partyOut = false;
                        if (manager.EconomyLedger != null && manager.EconomyLedger.Flows.Shares > sharesSeen) result.ExpeditionsHome++;
                    }
                    if (manager.EconomyLedger != null) sharesSeen = manager.EconomyLedger.Flows.Shares;
                }

                // The hunting profile's counts (K11.2): rescues told this tick, and a hunt paid when the hunts' list empties
                // with a bounty fulfilled on the tick
                if (result.Player.Name == "hunting" && sim.Chronicle != null)
                {
                    told.Clear();
                    sim.Chronicle.GetAllRecentEvents(told, 16);
                    int open = manager.Hunts.Count;
                    for (int i = 0; i < told.Count; i++)
                    {
                        if (told[i].SimulationTick != sim.Clock.CurrentTick) continue;
                        if (told[i].EventType == Simpiens.Narrative.ChronicleEventType.CrawlerRescued) result.Rescues++;
                        if (told[i].EventType == Simpiens.Narrative.ChronicleEventType.BountyFulfilled && open < huntsOpen) { result.HuntsPaid++; huntsOpen = open; }
                    }
                    huntsOpen = open;
                }

                if (rel >= horizonTicks) break;
                sim.Step(1);
            }
            result.Kills = manager.Creatures?.Kills ?? 0;
            foreach (var gone in sim.Host.Departed) if (gone.Died) result.Deaths++;
            if (guildPlayer != null)
            {
                result.Members = guild.Members.Count;
                result.Signed = guildPlayer.Recruited.Count;
                if (manager.EconomyLedger != null)
                {
                    var flows = manager.EconomyLedger.Flows;
                    result.Wages = flows.Wages;
                    result.WageGold = flows.WageGold;
                    result.Shares = flows.Shares;
                    result.ShareGold = flows.ShareGold;
                }
            }
            var workshops = manager.Workshops;
            for (int i = 0; workshops != null && i < workshops.Count; i++)
            {
                if (workshops[i].Built) result.StationsBuilt++;
                result.Made += workshops[i].Made;
            }
            result.Artisans = workshops?.Artisans.Count ?? 0;
            if (manager.EconomyLedger != null)
            {
                result.DrifterPurses = manager.EconomyLedger.GetFaucetTotal("DrifterPurses");
                result.DrifterDepartures = manager.EconomyLedger.GetSinkTotal("DrifterDepartures");
            }
            return result;
        }

        private static Row Sample(ISimulationManager manager, IReadOnlyList<AgentState> crawlers, long rel, long absoluteTick, uint crawlerGold,
            Dictionary<EntityId, int> trustAtStart, int starvingEpisodes, int collapses)
        {
            var row = new Row { Tick = rel, CrawlerGold = (int)crawlerGold, StarvingEpisodes = starvingEpisodes, Collapses = collapses, TrustMin = int.MaxValue, TrustMax = int.MinValue };
            if (manager.ActiveStalls.Count > 0)
            {
                var stall = manager.ActiveStalls[0]; // the session has one stall (the hub stall)
                row.Treasury = stall.TreasuryGold;
                row.InArrears = stall.InArrears;
            }

            // Trust toward the player, against each resident's own value at the first sample (drifters pass through:
            // the bands are about the outpost's own crawlers, Epic 10 G1)
            float sum = 0f;
            int residents = 0;
            for (int i = 0; i < crawlers.Count; i++)
            {
                var state = crawlers[i];
                if (state.IsDrifter) continue;
                residents++;
                int trust = state.Memory != null ? state.Memory.GetAffinity(SimulationManager.PlayerEntityId, (uint)absoluteTick).Trust : 0;
                if (!trustAtStart.TryGetValue(state.AgentId, out int startTrust)) trustAtStart[state.AgentId] = startTrust = trust;
                sum += trust;
                if (trust < row.TrustMin) row.TrustMin = trust;
                if (trust > row.TrustMax) row.TrustMax = trust;
                if (trust > startTrust) row.TrustUp++;
                else if (trust < startTrust) row.TrustDown++;
            }
            if (residents == 0) { row.TrustMin = 0; row.TrustMax = 0; }
            row.TrustMean = residents > 0 ? sum / residents : 0f;
            return row;
        }
    }
}
