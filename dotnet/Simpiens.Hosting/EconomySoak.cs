using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;

namespace Simpiens.Hosting
{
    /// <summary>
    /// The economy soak, headless (Epic CORE C10): the outpost for 30 sim-minutes under one kind of player, sampled every
    /// sim-minute, judged against that player's pace band (docs/economy-model.md §7). The same profiles, samples and bands
    /// as Unity's EconomySoakScenario (Epic Test-2, §3.3), observed every tick instead of every frame; nothing is injected
    /// except the player's prices, set by a typed command before the first tick.
    /// </summary>
    public static class EconomySoak
    {
        public const long HorizonTicks = 18000;       // 30 sim-minutes at 10 Hz
        public const long SampleIntervalTicks = 600;   // one caravan cycle = one sim-minute
        public const float StarvingHunger = 95f;
        public const uint StarvingTicks = 300;

        // Pace bands (docs/economy-model.md §7; review §3.3 D5), as EconomySoakScenario asserts them
        // Re-set for Epic 10 G2's glowstone (new money from the hazard sectors); was 375-875g (docs/economy-model.md §7)
        public const uint FairTreasuryMin = 1000;
        public const uint FairTreasuryMax = 2200;
        public const int FairTrustUpMin = 8;
        public const int GreedyTrustUpMax = 2;
        public const int GenerousTrustUpPeakMin = 8;

        /// <summary>The seeds scripts/run-economy-soak.sh plays.</summary>
        public static readonly ulong[] DefaultSeeds = { 20261009UL, 1UL, 2UL };
        public static readonly string[] Profiles = { "fair", "greedy", "generous" };

        public readonly struct Player
        {
            public readonly string Name;
            public readonly ushort RationPrice, OreBuyPrice, HerbBuyPrice;
            public readonly bool SendsCommand;

            public Player(string name, ushort rationPrice, ushort oreBuyPrice, ushort herbBuyPrice, bool sendsCommand)
            {
                Name = name;
                RationPrice = rationPrice;
                OreBuyPrice = oreBuyPrice;
                HerbBuyPrice = herbBuyPrice;
                SendsCommand = sendsCommand;
            }
        }

        /// <summary>The player the soak plays: fair keeps the session's prices; greedy and generous set their own.</summary>
        public static Player Resolve(string name, EconomyTuning economy)
        {
            switch ((name ?? "fair").Trim().ToLowerInvariant())
            {
                case "fair": return new Player("fair", economy.StallRationPrice, economy.StallOreBuyPrice, economy.StallHerbBuyPrice, sendsCommand: false);
                case "greedy": return new Player("greedy", 30, 10, 12, sendsCommand: true);
                case "generous": return new Player("generous", 5, 35, 40, sendsCommand: true);
                // Slightly greedy, for the drifters' comparison (Epic 10 G1); it has no pace band of its own
                case "keen": return new Player("keen", 20, 18, 22, sendsCommand: true);
                default: throw new ArgumentException($"Unknown soak profile '{name}' (fair, greedy, generous, keen).", nameof(name));
            }
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

            public Row Last => Rows[Rows.Count - 1];

            /// <summary>Everything that fails the soak: integrity first, then the player's pace band. Empty when it holds.</summary>
            public List<string> Failures()
            {
                var failures = new List<string>();
                if (ObservedTicks < HorizonTicks) failures.Add($"ended at t{ObservedTicks}, before the horizon t{HorizonTicks}");
                if (ConservationViolations > 0) failures.Add($"ledger conservation violated {ConservationViolations} times (worst {WorstDiscrepancy}g)");
                if (!PricesApplied) failures.Add($"prices {Player.RationPrice}/{Player.OreBuyPrice}/{Player.HerbBuyPrice} not applied (stall {PricesSeen})");
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
                        if (last.Treasury < FairTreasuryMin || last.Treasury > FairTreasuryMax) failures.Add($"fair: treasury {last.Treasury}g at 30 min, outside {FairTreasuryMin}-{FairTreasuryMax}g");
                        if (last.TrustUp < FairTrustUpMin) failures.Add($"fair: trust rose for only {last.TrustUp} crawlers (need {FairTrustUpMin})");
                        if (last.StarvingEpisodes != 0) failures.Add($"fair: {last.StarvingEpisodes} starving episodes (need 0)");
                        break;
                    case "greedy":
                        if (last.TrustUp > GreedyTrustUpMax) failures.Add($"greedy: trust rose for {last.TrustUp} crawlers (at most {GreedyTrustUpMax})");
                        // At its lowest, as generous is judged at its peak: once greed has emptied the stall nobody
                        // trades, and trust drifts back to neutral by minute 30 (Epic 10 G0)
                        if (!(lowestTrustMean < 0f)) failures.Add($"greedy: trust mean never fell below 0 (lowest {lowestTrustMean:F1})");
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
                string Treasury(int minute) => Rows.Count > minute ? Rows[minute].Treasury.ToString(CultureInfo.InvariantCulture) : "?";
                return $"{Player.Name,-8} seed {Seed,-9} treasury T0 {Treasury(0)} T10 {Treasury(10)} T20 {Treasury(20)} T30 {Treasury(30)}g; trust mean {last.TrustMean:F1}, up {last.TrustUp}, down {last.TrustDown}; starving {last.StarvingEpisodes}, collapses {last.Collapses}; ledger violations {ConservationViolations}";
            }

            public void WriteCsv(string path)
            {
                var sb = new StringBuilder("profile,seed,tick,sim_minute,treasury,in_arrears,crawler_gold,trust_mean,trust_min,trust_max,trust_up,trust_down,starving_episodes,collapses\n");
                foreach (var r in Rows)
                {
                    sb.Append(Player.Name).Append(',').Append(Seed).Append(',').Append(r.Tick).Append(',')
                      .Append((r.Tick / (double)SampleIntervalTicks).ToString("F1", CultureInfo.InvariantCulture)).Append(',')
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
                    updateStallPrices: new UpdateStallPricesCommand(manager.ActiveStalls[0].StallEntityId, result.Player.RationPrice, result.Player.OreBuyPrice, result.Player.HerbBuyPrice)));
            }

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
                        result.PricesSeen = $"{stall.RationSellPrice}/{stall.OreBuyPrice}/{stall.HerbBuyPrice}";
                        result.PricesApplied = stall.RationSellPrice == result.Player.RationPrice && stall.OreBuyPrice == result.Player.OreBuyPrice && stall.HerbBuyPrice == result.Player.HerbBuyPrice;
                    }
                    result.Rows.Add(Sample(manager, crawlers, rel, sim.Clock.CurrentTick, crawlerGold, trustAtStart, starvingEpisodes, collapses));
                }

                if (rel >= horizonTicks) break;
                sim.Step(1);
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
