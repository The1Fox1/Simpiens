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
    /// The economy soak, headless (Epic CORE C10): the outpost for 30 sim-minutes under one kind of player, sampled every
    /// sim-minute, judged against that player's pace band (docs/economy-model.md §7). The same profiles, samples and bands
    /// as Unity's EconomySoakScenario (Epic Test-2, §3.3), observed every tick instead of every frame; nothing is injected
    /// except the player's prices, set by a typed command before the first tick. The 24-hour day's soak (Epic 10 Part 2,
    /// T4.4) plays three game days on the day's economy, sampled at each caravan visit (<see cref="Pace.Day"/>).
    /// </summary>
    public static class EconomySoak
    {
        public const long HorizonTicks = 18000;       // 30 sim-minutes at 10 Hz
        public const long SampleIntervalTicks = 600;   // one caravan cycle = one sim-minute
        public const float StarvingHunger = 95f;
        public const uint StarvingTicks = 300;

        /// <summary>
        /// What a soak plays and how it is judged: the session's economy and map, the horizon, the sample (a caravan
        /// visit), how long at <see cref="StarvingHunger"/> is starving, and the pace bands. <see cref="Today"/> is the
        /// soak before the 24-hour day; <see cref="Day"/> is the day's (Epic 10 Part 2, T4.4, D28).
        /// </summary>
        public sealed class Pace
        {
            public string Name { get; init; }

            /// <summary>The session's economy and map (null: the outpost session's own).</summary>
            public EconomyTuning Economy { get; init; }
            public Simpiens.Simulation.World.MapDefinition Map { get; init; }

            public long HorizonTicks { get; init; }
            public long SampleIntervalTicks { get; init; }
            public uint StarvingTicks { get; init; }

            /// <summary>The summary's treasury every this many samples, labelled "T10" or "D1": the label, then the checkpoint times its step.</summary>
            public int SamplesPerCheckpoint { get; init; }
            public string CheckpointLabel { get; init; }
            public int CheckpointStep { get; init; }

            /// <summary>The horizon in words, for the failures: "30 min", "day 3".</summary>
            public string HorizonText { get; init; }

            public uint FairTreasuryMin { get; init; }
            public uint FairTreasuryMax { get; init; }
            public int FairTrustUpMin { get; init; }
            public int GreedyTrustUpMax { get; init; }
            public int GenerousTrustUpPeakMin { get; init; }
            public uint GuildTreasuryMin { get; init; }
            public uint GuildTreasuryMax { get; init; }
            public int GuildTrustUpMin { get; init; }

            /// <summary>A guild full this long before the horizon has had time to send a party and see it home.</summary>
            public long GuildSpareTicks { get; init; }

            /// <summary>The soak before the 24-hour day: 30 sim-minutes, sampled each visit, today's bands.</summary>
            public static readonly Pace Today = new Pace
            {
                Name = "today",
                HorizonTicks = EconomySoak.HorizonTicks,
                SampleIntervalTicks = EconomySoak.SampleIntervalTicks,
                StarvingTicks = EconomySoak.StarvingTicks,
                SamplesPerCheckpoint = 10,
                CheckpointLabel = "T",
                CheckpointStep = 10,
                HorizonText = "30 min",
                FairTreasuryMin = EconomySoak.FairTreasuryMin,
                FairTreasuryMax = EconomySoak.FairTreasuryMax,
                FairTrustUpMin = EconomySoak.FairTrustUpMin,
                GreedyTrustUpMax = EconomySoak.GreedyTrustUpMax,
                GenerousTrustUpPeakMin = EconomySoak.GenerousTrustUpPeakMin,
                GuildTreasuryMin = EconomySoak.GuildTreasuryMin,
                GuildTreasuryMax = EconomySoak.GuildTreasuryMax,
                GuildTrustUpMin = EconomySoak.GuildTrustUpMin,
                GuildSpareTicks = EconomySoak.GuildSpareTicks,
            };

            /// <summary>
            /// The 24-hour day's soak (D28): three game days on <see cref="EconomyTuning.FullDay"/> and its floor, sampled at
            /// each caravan visit (every 6 game hours); starving is hunger 95 for 2 game hours. The bands are proposed to the
            /// producer from T4.4's measurement over seeds 1-15 and 20261009 (docs/time-model.md §2): the fair player ended
            /// day 3 at 771-1,277g with trust risen for 10-12; the greedy one raised trust for 0-3; the generous one went
            /// broke on every seed; the guild player ended at 578-998g, paid every wage, and brought 1-2 hauls home on every
            /// seed whose guild filled 8 game hours before the end.
            /// </summary>
            public static readonly Pace Day = new Pace
            {
                Name = "day",
                Economy = EconomyTuning.FullDay,
                Map = Floor1Layout.FullDayDefinition,
                HorizonTicks = 3 * DayClock.TicksPerDay,
                SampleIntervalTicks = EconomyTuning.FullDay.Supply.RestockIntervalTicks,
                StarvingTicks = DayClock.Hours(2),
                SamplesPerCheckpoint = DayClock.TicksPerDay / (int)EconomyTuning.FullDay.Supply.RestockIntervalTicks,
                CheckpointLabel = "D",
                CheckpointStep = 1,
                HorizonText = "day 3",
                FairTreasuryMin = 600,
                FairTreasuryMax = 1600,
                FairTrustUpMin = 8,
                GreedyTrustUpMax = 3,
                GenerousTrustUpPeakMin = 8,
                GuildTreasuryMin = 400,
                GuildTreasuryMax = 1200,
                GuildTrustUpMin = 8,
                GuildSpareTicks = DayClock.Hours(8),
            };
        }

        // Pace bands (docs/economy-model.md §7; review §3.3 D5), as EconomySoakScenario asserts them
        // Re-set for Epic 10 G2's glowstone (new money from the hazard sectors), 1,000-2,000g by the producer on 2026-10-25;
        // was 375-875g (docs/economy-model.md §7)
        public const uint FairTreasuryMin = 1000;
        public const uint FairTreasuryMax = 2000;
        public const int FairTrustUpMin = 8;
        public const int GreedyTrustUpMax = 2;
        public const int GenerousTrustUpPeakMin = 8;

        // The guild profile's band (Epic 10 G6, D14), from G6.3's measurement: over seeds 1-15 the fair player with a guild
        // ended at 1,146-1,991g, never left a wage unpaid and brought a haul home on every seed whose guild filled
        public const uint GuildTreasuryMin = 1000;
        public const uint GuildTreasuryMax = 2000;
        public const int GuildTrustUpMin = 8;

        /// <summary>A guild full this long before the horizon has had time to send a party and see it home.</summary>
        public const long GuildSpareTicks = 3000;

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
        /// T4.4: on today's economy the shares give exactly the prices they replaced); guild keeps fair prices and runs a
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
                // Today: rations 30g, ore 10g, herbs 12g, gear 60g
                case "greedy": return new Player("greedy", Prices(2f, 0.5f, 0.5f, 2f), sendsCommand: true);
                // Today: rations 5g, ore 35g, herbs 40g, gear 20g
                case "generous": return new Player("generous", Prices(1f / 3f, 1.75f, 1.6f, 2f / 3f), sendsCommand: true);
                // Slightly greedy, for the drifters' comparison (Epic 10 G1); it has no pace band of its own. Today: rations
                // 20g, ore 18g, herbs 22g, gear 35g
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
            public Pace Pace = Pace.Today;
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
                if (ObservedTicks < Pace.HorizonTicks) failures.Add($"ended at t{ObservedTicks}, before the horizon t{Pace.HorizonTicks}");
                if (ConservationViolations > 0) failures.Add($"ledger conservation violated {ConservationViolations} times (worst {WorstDiscrepancy}g)");
                if (!PricesApplied) failures.Add($"prices {Player.PriceText()} not applied (stall {PricesSeen})");
                if (Rows.Count != Pace.HorizonTicks / Pace.SampleIntervalTicks + 1) failures.Add($"{Rows.Count} samples, expected {Pace.HorizonTicks / Pace.SampleIntervalTicks + 1}");
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
                        if (last.Treasury < Pace.FairTreasuryMin || last.Treasury > Pace.FairTreasuryMax) failures.Add($"fair: treasury {last.Treasury}g at {Pace.HorizonText}, outside {Pace.FairTreasuryMin}-{Pace.FairTreasuryMax}g");
                        if (last.TrustUp < Pace.FairTrustUpMin) failures.Add($"fair: trust rose for only {last.TrustUp} crawlers (need {Pace.FairTrustUpMin})");
                        if (last.StarvingEpisodes != 0) failures.Add($"fair: {last.StarvingEpisodes} starving episodes (need 0)");
                        break;
                    case "greedy":
                        if (last.TrustUp > Pace.GreedyTrustUpMax) failures.Add($"greedy: trust rose for {last.TrustUp} crawlers (at most {Pace.GreedyTrustUpMax})");
                        // At its lowest, as generous is judged at its peak: once greed has emptied the stall nobody
                        // trades, and trust drifts back to neutral by minute 30 (Epic 10 G0)
                        if (!(lowestTrustMean < 0f)) failures.Add($"greedy: trust mean never fell below 0 (lowest {lowestTrustMean:F1})");
                        break;
                    case "guild":
                        if (last.Treasury < Pace.GuildTreasuryMin || last.Treasury > Pace.GuildTreasuryMax) failures.Add($"guild: treasury {last.Treasury}g at {Pace.HorizonText}, outside {Pace.GuildTreasuryMin}-{Pace.GuildTreasuryMax}g");
                        if (last.TrustUp < Pace.GuildTrustUpMin) failures.Add($"guild: trust rose for only {last.TrustUp} crawlers (need {Pace.GuildTrustUpMin})");
                        if (last.StarvingEpisodes != 0) failures.Add($"guild: {last.StarvingEpisodes} starving episodes (need 0)");
                        if (WorstUnpaid != 0) failures.Add($"guild: a member went unpaid {WorstUnpaid} visit(s) in a row");
                        if (GuildFullTick >= 0 && GuildFullTick <= Pace.HorizonTicks - Pace.GuildSpareTicks && ExpeditionsHome == 0)
                        {
                            failures.Add($"guild: full at t{GuildFullTick}, but no expedition came home with glowstone");
                        }
                        break;
                    case "generous":
                        if (!(minTreasury == 0 || everInArrears)) failures.Add($"generous: the stall never went broke (lowest treasury {minTreasury}g)");
                        if (peakTrustUp < Pace.GenerousTrustUpPeakMin) failures.Add($"generous: trust rose for at most {peakTrustUp} crawlers (need {Pace.GenerousTrustUpPeakMin} at its peak)");
                        break;
                }
                return failures;
            }

            public string Summary()
            {
                var last = Rows.Count > 0 ? Last : default;
                var checkpoints = new List<string>(4);
                for (int k = 0; k * Pace.SamplesPerCheckpoint <= Pace.HorizonTicks / Pace.SampleIntervalTicks; k++)
                {
                    int sample = k * Pace.SamplesPerCheckpoint;
                    checkpoints.Add($"{Pace.CheckpointLabel}{k * Pace.CheckpointStep} {(Rows.Count > sample ? Rows[sample].Treasury.ToString(CultureInfo.InvariantCulture) : "?")}");
                }
                string guild = Player.Name == "guild"
                    ? $"; guild {Members} (signed {Signed}, full at t{GuildFullTick}), wages {Wages} ({WageGold}g), worst unpaid {WorstUnpaid}, expeditions home {ExpeditionsHome}, shares {ShareGold}g"
                    : string.Empty;
                return $"{Player.Name,-8} seed {Seed,-9} treasury {string.Join(" ", checkpoints)}g; trust mean {last.TrustMean:F1}, up {last.TrustUp}, down {last.TrustDown}; starving {last.StarvingEpisodes}, collapses {last.Collapses}; ledger violations {ConservationViolations}{guild}";
            }

            public void WriteCsv(string path)
            {
                var sb = new StringBuilder("profile,seed,tick,sim_minute,treasury,in_arrears,crawler_gold,trust_mean,trust_min,trust_max,trust_up,trust_down,starving_episodes,collapses\n");
                foreach (var r in Rows)
                {
                    sb.Append(Player.Name).Append(',').Append(Seed).Append(',').Append(r.Tick).Append(',')
                      .Append((r.Tick / (double)EconomySoak.SampleIntervalTicks).ToString("F1", CultureInfo.InvariantCulture)).Append(',')
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
        /// Plays the outpost with <paramref name="seed"/> at <paramref name="pace"/> (today's when null) as one kind of
        /// player, to the pace's horizon unless <paramref name="horizonTicks"/> is given. Without <paramref name="drifters"/>
        /// the outpost is closed, as it was before Epic 10 G1.
        /// </summary>
        public static Result Run(ulong seed, string profile, long horizonTicks = 0, bool drifters = true, Pace pace = null)
        {
            pace ??= Pace.Today;
            if (horizonTicks <= 0) horizonTicks = pace.HorizonTicks;
            var economy = pace.Economy ?? EconomyTuning.Default;
            bool ownSession = pace.Economy != null || pace.Map != null || !drifters;
            using var sim = LocalSimulation.Create(seed, !ownSession ? null : services =>
            {
                services.RemoveAll<ISessionDefinition>();
                services.AddSingleton<ISessionDefinition>(new OutpostSession(OutpostSession.DefaultCrawlerCount, drifters ? economy : economy.WithDrifters(DrifterTuning.None), pace.Map));
            });
            sim.Respawn(OutpostSession.SessionId);
            var manager = sim.Simulation;
            var result = new Result { Player = Resolve(profile, manager.Economy), Seed = seed, Pace = pace };

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
                guildPlayer = new GuildPlayer(sim, targetSize: manager.Economy.Guild.MaxMembers);
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
                        if (rel - since >= pace.StarvingTicks && starvingNow.Add(state.AgentId)) starvingEpisodes++;
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

                if (rel % pace.SampleIntervalTicks == 0)
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
                    if (result.GuildFullTick < 0 && guild.Members.Count >= manager.Economy.Guild.MaxMembers) result.GuildFullTick = rel;
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
