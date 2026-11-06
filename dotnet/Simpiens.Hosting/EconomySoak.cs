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
        public const int GreedyTrustUpMax = 3;
        public const int GenerousTrustUpPeakMin = 8;
        public const uint GuildTreasuryMin = 400;
        public const uint GuildTreasuryMax = 1200;
        public const int GuildTrustUpMin = 8;

        /// <summary>
        /// The guild profile's size: Part 1's guild, one party of four (G6's band, D14). The guildhall's guild of eight
        /// (Epic 10 Part 2, W5) gets its band at W6.
        /// </summary>
        public const int GuildProfileSize = Simpiens.Intervention.DispatchExpeditionCommand.MaxMembers;

        /// <summary>A guild full this long before the horizon has had time to send a party and see it home.</summary>
        public const long GuildSpareTicks = 8 * DayClock.TicksPerHour;

        /// <summary>The soak's seeds.</summary>
        public static readonly ulong[] DefaultSeeds = { 20261009UL, 1UL, 2UL };
        public static readonly string[] Profiles = { "fair", "greedy", "generous", "guild" };

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
                // Rations 30g, ore 2g, herbs 3g, gear 60g
                case "greedy": return new Player("greedy", Prices(2f, 0.5f, 0.5f, 2f), sendsCommand: true);
                // Rations 5g, ore 9g, herbs 10g, gear 20g
                case "generous": return new Player("generous", Prices(1f / 3f, 1.75f, 1.6f, 2f / 3f), sendsCommand: true);
                // Slightly greedy, for the drifters' comparison (Epic 10 G1); it has no pace band of its own. Rations 20g,
                // ore 4g, herbs 5g, gear 35g
                case "keen": return new Player("keen", Prices(4f / 3f, 0.9f, 0.88f, 7f / 6f), sendsCommand: true);
                default: throw new ArgumentException($"Unknown soak profile '{name}' (fair, greedy, generous, guild, keen).", nameof(name));
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
                        if (WorstUnpaid != 0) failures.Add($"guild: a member went unpaid {WorstUnpaid} visit(s) in a row");
                        if (GuildFullTick >= 0 && GuildFullTick <= HorizonTicks - GuildSpareTicks && ExpeditionsHome == 0)
                        {
                            failures.Add($"guild: full at t{GuildFullTick}, but no expedition came home with glowstone");
                        }
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
                string guild = Player.Name == "guild"
                    ? $"; guild {Members} (signed {Signed}, full at t{GuildFullTick}), wages {Wages} ({WageGold}g), worst unpaid {WorstUnpaid}, expeditions home {ExpeditionsHome}, shares {ShareGold}g"
                    : string.Empty;
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

            // The guild profile's player (D14), acting only through commands
            GuildPlayer guildPlayer = null;
            if (result.Player.Name == "guild")
            {
                guildPlayer = new GuildPlayer(sim, targetSize: GuildProfileSize);
                guildPlayer.SendExpeditions(Floor1Layout.DrownedGallerySectorId, Floor1Layout.GallerySeamYield);
            }
            var guild = manager.Guild;
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

                if (guildPlayer != null)
                {
                    for (int i = 0; i < guild.Members.Count; i++) result.WorstUnpaid = Math.Max(result.WorstUnpaid, guild.Members[i].UnpaidVisits);
                    if (result.GuildFullTick < 0 && guild.Members.Count >= GuildProfileSize) result.GuildFullTick = rel;
                    if (guild.HasExpedition) partyOut = true;
                    else if (partyOut)
                    {
                        partyOut = false;
                        if (manager.EconomyLedger != null && manager.EconomyLedger.Flows.Shares > sharesSeen) result.ExpeditionsHome++;
                    }
                    if (manager.EconomyLedger != null) sharesSeen = manager.EconomyLedger.Flows.Shares;
                }

                if (rel >= horizonTicks) break;
                sim.Step(1);
            }
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
