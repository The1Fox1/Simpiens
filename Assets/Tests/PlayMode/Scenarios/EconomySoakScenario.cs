using Simpiens.Cognition;
using Simpiens.Core.Session;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Environment;
using UnityEngine;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Economy soak (Milestone 1 closure, Phase 3; review §3.3 step 2). Runs the real session for 30 sim-minutes at 10x
    /// with no injection. It plays one kind of player (SIMPIENS_SOAK_PROFILE: fair, greedy or generous), whose stall
    /// prices are set at tick 0 through the same typed command the dashboard sliders send. Every sim-minute it records
    /// stall, ledger, transfer, crawler, trust and supply figures to a CSV. It asserts integrity (ledger conservation,
    /// horizon reached, prices applied, CSV written) and the profile's pace band (docs/economy-model.md §7).
    /// The seed comes from SimulationSettings (SIMPIENS_SEED).
    /// </summary>
    public class EconomySoakScenario : ISimulationScenario
    {
        public const uint HorizonTicks = 18000;     // 30 sim-minutes at 10 Hz
        public const uint SampleIntervalTicks = 600; // one caravan cycle = one sim-minute
        private const float WallClockTimeoutSeconds = 600f;
        private const int MaxRows = (int)(HorizonTicks / SampleIntervalTicks) + 2;

        public string Name => "EconomySoakScenario";
        public string SessionId => OutpostSession.SessionId;
        public float DurationSeconds => 0f; // SetupAsync observes until the horizon
        public float TimeMultiplier => 10f;

        public string CsvPath { get; private set; }

        /// <summary>A kind of player: the stall prices it sets at tick 0 (review §3.3 §2.5).</summary>
        public readonly struct SoakProfile
        {
            public readonly string Name;
            public readonly ushort RationPrice, OreBuyPrice, HerbBuyPrice;
            public readonly bool SendsCommand;

            public SoakProfile(string name, ushort rationPrice, ushort oreBuyPrice, ushort herbBuyPrice, bool sendsCommand)
            {
                Name = name;
                RationPrice = rationPrice;
                OreBuyPrice = oreBuyPrice;
                HerbBuyPrice = herbBuyPrice;
                SendsCommand = sendsCommand;
            }
        }

        public const string ProfileEnvironmentVariable = "SIMPIENS_SOAK_PROFILE";

        /// <summary>Fair keeps the session's default prices (no command); the others set theirs at tick 0.</summary>
        public static SoakProfile ResolveProfile(string name, EconomyTuning economy)
        {
            switch (string.IsNullOrWhiteSpace(name) ? "fair" : name.Trim().ToLowerInvariant())
            {
                case "fair": return new SoakProfile("fair", economy.StallRationPrice, economy.StallOreBuyPrice, economy.StallHerbBuyPrice, sendsCommand: false);
                case "greedy": return new SoakProfile("greedy", 30, 10, 12, sendsCommand: true);
                case "generous": return new SoakProfile("generous", 5, 35, 40, sendsCommand: true);
                default: throw new System.ArgumentException($"Unknown soak profile '{name}' (fair, greedy, generous).");
            }
        }

        public SoakProfile Profile { get; private set; }

        // Pace bands per profile (docs/economy-model.md §7; review §3.3 D5), asserted per run at the horizon
        // Re-set for Epic 10 G2's glowstone (new money from the hazard sectors); was 375-875g (1.5x-3.5x the 250g start)
        public const uint FairTreasuryMin = 1000;
        public const uint FairTreasuryMax = 2200;
        public const int FairTrustUpMin = 8;       // of 12 crawlers
        public const int GreedyTrustUpMax = 2;
        public const int GenerousTrustUpPeakMin = 8; // at its best, before bankruptcy erodes trade

        private struct Row
        {
            public long Tick;
            public uint Treasury, RationStock, OreStock, HerbStock, RationPrice, OreBuyPrice, HerbBuyPrice;
            public uint Fauceted, Sunk, Escrow, FaucetInitialSeed, FaucetCaravanSalvage, SinkSupplyCaravan, SinkUpkeep;
            public EconomyFlowTotals Flows;
            public int CrawlerGoldTotal, CrawlerGoldMin, CrawlerGoldMax, CrawlersHungry, CrawlersStarving;
            public float CrawlerHungerMean;
            public int Restocks, SkippedRestocks, SalvageVisits, UpkeepShort, InArrearsNow, Regrowths, DormantNodes, ActiveBounties;
            // Lifecycle on Floor 1 (Epic MAP Phase 3): cumulative except the "now" counts
            public int RationsHarvested, OreHarvested, HerbsHarvested, BunkSleeps, RestingInPlaceNow, SleepingInBunkNow;
            public int Collapses, StarvingEpisodes;
            // Trust toward the player (AffinityRecord.Trust); up/down compare each crawler with its value at tick 0
            public float TrustMean;
            public int TrustMin, TrustMax, TrustUp, TrustDown;
            public bool ConservationOk;
            public int Discrepancy;
        }

        private readonly Row[] _rows = new Row[MaxRows];
        private int _rowCount;
        private ulong _seed;
        private long _observedTicks;
        private int _conservationViolations;
        private int _worstDiscrepancy;
        private bool _csvWritten;

        // Per-frame lifecycle tracking (a collapse or starving episode counts once when it begins)
        public const float StarvingHunger = 95f;
        public const uint StarvingTicks = 300;
        private readonly System.Collections.Generic.HashSet<EntityId> _collapsedNow = new System.Collections.Generic.HashSet<EntityId>();
        private readonly System.Collections.Generic.Dictionary<EntityId, long> _hungrySince = new System.Collections.Generic.Dictionary<EntityId, long>();
        private readonly System.Collections.Generic.HashSet<EntityId> _starvingNow = new System.Collections.Generic.HashSet<EntityId>();
        private int _collapses;
        private int _starvingEpisodes;
        private readonly System.Collections.Generic.Dictionary<EntityId, int> _trustAtStart = new System.Collections.Generic.Dictionary<EntityId, int>();
        private ISimulationClock _clock;
        private bool _pricesApplied;
        private string _pricesSeen = "never sampled";

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            _clock = clock;
            var sim = resolver.Resolve<ISimulationManager>();
            var spawner = resolver.Resolve<SwarmSpawner>();
            var settings = resolver.Resolve<SimulationSettings>();
            Assert.IsNotNull(clock, "ISimulationClock not registered.");
            Assert.IsNotNull(sim, "ISimulationManager not registered.");
            Assert.IsNotNull(spawner, "SwarmSpawner not registered.");
            Assert.IsNotNull(sim.EconomyLedger, "IEconomyLedger not wired into the SimulationManager.");
            Assert.IsNotNull(sim.Supply, "ISupplyService not wired into the SimulationManager.");

            _seed = settings != null ? settings.Seed : SimulationSettings.DefaultSeed;
            Profile = ResolveProfile(System.Environment.GetEnvironmentVariable(ProfileEnvironmentVariable), sim.Economy);
            CsvPath = $"Logs/economy-soak-{Profile.Name}-seed{_seed}.csv";

            // The player's prices, through the same typed command the dashboard sliders send
            if (Profile.SendsCommand)
            {
                var interventions = resolver.Resolve<Simpiens.Intervention.IPlayerInterventionService>();
                Assert.IsNotNull(interventions, "IPlayerInterventionService not registered.");
                Assert.Greater(sim.ActiveStalls.Count, 0, "The session has no stall to price.");
                interventions.QueueInfluence(new Simpiens.Intervention.InfluenceEvent(
                    "EconomySoak:" + Profile.Name,
                    Simpiens.Intervention.InfluenceCommandType.UpdateStallPrices,
                    updateStallPrices: new Simpiens.Intervention.UpdateStallPricesCommand(
                        sim.ActiveStalls[0].StallEntityId, Profile.RationPrice, Profile.OreBuyPrice, Profile.HerbBuyPrice)));
            }

            if (clock is SimulationClock simClock)
            {
                simClock.SetTimeMultiplier(TimeMultiplier);
            }

            long startTick = clock.CurrentTick;
            long nextSample = 0;
            float elapsed = 0f;

            while (elapsed < WallClockTimeoutSeconds)
            {
                long rel = clock.CurrentTick - startTick;
                _observedTicks = rel;

                bool ok = CheckConservation(sim, spawner, out int discrepancy);
                TrackLifecycle(spawner, rel);
                if (rel >= nextSample)
                {
                    if (rel > 0 && !_pricesApplied) CheckPrices(sim);
                    Sample(sim, spawner, rel, ok, discrepancy);
                    nextSample += SampleIntervalTicks;
                }

                if (rel >= HorizonTicks) break;

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            WriteCsv();
            LogSummary();
        }

        private void CheckPrices(ISimulationManager sim)
        {
            if (sim.ActiveStalls.Count == 0) return;
            var stall = sim.ActiveStalls[0];
            _pricesSeen = $"{stall.RationSellPrice}/{stall.OreBuyPrice}/{stall.HerbBuyPrice}";
            _pricesApplied = stall.RationSellPrice == Profile.RationPrice && stall.OreBuyPrice == Profile.OreBuyPrice && stall.HerbBuyPrice == Profile.HerbBuyPrice;
        }

        private static int Trust(AutonomousAgent agent, long tick) =>
            agent != null && agent.Memory != null ? agent.Memory.GetAffinity(SimulationManager.PlayerEntityId, (uint)tick).Trust : 0;

        private bool CheckConservation(ISimulationManager sim, SwarmSpawner spawner, out int discrepancy)
        {
            uint stallGold = 0;
            var stalls = sim.ActiveStalls;
            for (int i = 0; i < stalls.Count; i++) stallGold += stalls[i].TreasuryGold;

            uint agentGold = 0;
            var agents = spawner.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var inv = agents[i] != null ? agents[i].State?.Inventory : null;
                if (inv != null) agentGold += (uint)inv.Gold;
            }

            bool ok = sim.EconomyLedger.ValidateConservation(stallGold, agentGold, out discrepancy);
            if (!ok)
            {
                _conservationViolations++;
                if (SimMath.Abs(discrepancy) > SimMath.Abs(_worstDiscrepancy)) _worstDiscrepancy = discrepancy;
            }
            return ok;
        }

        private void TrackLifecycle(SwarmSpawner spawner, long rel)
        {
            var agents = spawner.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var state = agents[i] != null ? agents[i].State : null;
                if (state == null) continue;
                if (state.IsExhaustionCollapsed) { if (_collapsedNow.Add(state.AgentId)) _collapses++; }
                else _collapsedNow.Remove(state.AgentId);

                if (state.Needs.Hunger >= StarvingHunger)
                {
                    if (!_hungrySince.TryGetValue(state.AgentId, out long since)) _hungrySince[state.AgentId] = since = rel;
                    if (rel - since >= StarvingTicks && _starvingNow.Add(state.AgentId)) _starvingEpisodes++;
                }
                else
                {
                    _hungrySince.Remove(state.AgentId);
                    _starvingNow.Remove(state.AgentId);
                }
            }
        }

        private void Sample(ISimulationManager sim, SwarmSpawner spawner, long rel, bool conservationOk, int discrepancy)
        {
            if (_rowCount >= _rows.Length) return;

            var ledger = sim.EconomyLedger;
            var supply = sim.Supply;
            var row = new Row
            {
                Tick = rel,
                Fauceted = ledger.TotalFaucetedGold,
                Sunk = ledger.TotalSinkedGold,
                Escrow = ledger.TotalEscrowGold,
                FaucetInitialSeed = ledger.GetFaucetTotal("InitialSeed"),
                FaucetCaravanSalvage = ledger.GetFaucetTotal(SupplyService.SalvageSalesFaucetName),
                SinkSupplyCaravan = ledger.GetSinkTotal(SupplyService.CaravanSinkName),
                SinkUpkeep = ledger.GetSinkTotal(SupplyService.UpkeepSinkName),
                UpkeepShort = supply.UpkeepShortCount,
                Flows = ledger.Flows,
                Restocks = supply.RestockCount,
                SkippedRestocks = supply.SkippedRestockCount,
                SalvageVisits = supply.SalvageSaleVisits,
                Regrowths = supply.RegrowthCount,
                DormantNodes = supply.DormantCount,
                ActiveBounties = sim.ActiveBounties.Count,
                ConservationOk = conservationOk,
                Discrepancy = discrepancy,
                CrawlerGoldMin = int.MaxValue,
                CrawlerGoldMax = int.MinValue
            };

            var stalls = sim.ActiveStalls;
            if (stalls.Count > 0)
            {
                var stall = stalls[0]; // the session has one stall (the hub stall)
                row.Treasury = stall.TreasuryGold;
                row.RationStock = stall.RationStock;
                row.OreStock = stall.OreStock;
                row.HerbStock = stall.HerbStock;
                row.RationPrice = stall.RationSellPrice;
                row.OreBuyPrice = stall.OreBuyPrice;
                row.HerbBuyPrice = stall.HerbBuyPrice;
                row.InArrearsNow = stall.InArrears ? 1 : 0;
            }

            // Every agent of the outpost session is a free crawler
            int crawlers = 0;
            float crawlerHunger = 0f;
            var agents = spawner.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var state = agents[i] != null ? agents[i].State : null;
                if (state == null) continue;
                int gold = state.Inventory != null ? state.Inventory.Gold : 0;
                float hunger = state.Needs.Hunger;

                crawlers++;
                row.CrawlerGoldTotal += gold;
                if (gold < row.CrawlerGoldMin) row.CrawlerGoldMin = gold;
                if (gold > row.CrawlerGoldMax) row.CrawlerGoldMax = gold;
                crawlerHunger += hunger;
                if (hunger >= 60f) row.CrawlersHungry++;
                if (hunger >= 85f) row.CrawlersStarving++;
            }
            if (crawlers == 0) { row.CrawlerGoldMin = 0; row.CrawlerGoldMax = 0; }

            // Trust toward the player, against each crawler's own value at the first sample
            long absoluteTick = _clock != null ? _clock.CurrentTick : rel;
            int trusted = 0;
            float trustSum = 0f;
            row.TrustMin = int.MaxValue;
            row.TrustMax = int.MinValue;
            for (int i = 0; i < agents.Count; i++)
            {
                var agent = agents[i];
                if (agent == null || agent.State == null) continue;
                int trust = Trust(agent, absoluteTick);
                if (!_trustAtStart.TryGetValue(agent.State.AgentId, out int start)) _trustAtStart[agent.State.AgentId] = start = trust;
                trusted++;
                trustSum += trust;
                if (trust < row.TrustMin) row.TrustMin = trust;
                if (trust > row.TrustMax) row.TrustMax = trust;
                if (trust > start) row.TrustUp++;
                else if (trust < start) row.TrustDown++;
            }
            if (trusted == 0) { row.TrustMin = 0; row.TrustMax = 0; }
            row.TrustMean = trusted > 0 ? trustSum / trusted : 0f;

            if (sim is SimulationManager manager)
            {
                row.RationsHarvested = manager.HarvestedUnits(ItemType.Ration);
                row.OreHarvested = manager.HarvestedUnits(ItemType.IronOre);
                row.HerbsHarvested = manager.HarvestedUnits(ItemType.MedicinalHerb);
                foreach (var kvp in manager.BunkSleeps) row.BunkSleeps += kvp.Value;
                row.SleepingInBunkNow = manager.BunkOccupants.Count;
            }
            for (int i = 0; i < agents.Count; i++)
            {
                var state = agents[i] != null ? agents[i].State : null;
                if (state != null && state.HasActivePlan && state.ActivePlan.CurrentAction is Simpiens.Cognition.Planning.Actions.RestAction) row.RestingInPlaceNow++;
            }
            row.Collapses = _collapses;
            row.StarvingEpisodes = _starvingEpisodes;
            row.CrawlerHungerMean = crawlers > 0 ? crawlerHunger / crawlers : 0f;

            _rows[_rowCount++] = row;
        }

        private const string Header =
            "profile,seed,tick,sim_minute," +
            "treasury,ration_stock,ore_stock,herb_stock,ration_price,ore_buy_price,herb_buy_price," +
            "fauceted_total,sunk_total,escrow,faucet_initial_seed,faucet_caravan_salvage,sink_supply_caravan,sink_upkeep," +
            "purchases,purchase_gold,sales,sale_gold,escrow_locked,escrow_released,escrow_refunded," +
            "crawler_gold_total,crawler_gold_min,crawler_gold_max,crawler_hunger_mean,crawlers_hungry,crawlers_starving," +
            "restocks,skipped_restocks,caravan_salvage_visits,upkeep_short,in_arrears,regrowths,dormant_nodes,active_bounties," +
            "rations_harvested,ore_harvested,herbs_harvested,bunk_sleeps,sleeping_in_bunk_now,resting_in_place_now,collapses,starving_episodes," +
            "trust_mean,trust_min,trust_max,trust_up,trust_down," +
            "conservation_ok,discrepancy";

        private void WriteCsv()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(Header.Length * (_rowCount + 1));
            sb.AppendLine(Header);
            for (int i = 0; i < _rowCount; i++)
            {
                var r = _rows[i];
                var f = r.Flows;
                sb.Append(Profile.Name).Append(',').Append(_seed).Append(',')
                  .Append(r.Tick).Append(',').Append((r.Tick / (double)SampleIntervalTicks).ToString("F1", inv)).Append(',')
                  .Append(r.Treasury).Append(',').Append(r.RationStock).Append(',').Append(r.OreStock).Append(',').Append(r.HerbStock).Append(',')
                  .Append(r.RationPrice).Append(',').Append(r.OreBuyPrice).Append(',').Append(r.HerbBuyPrice).Append(',')
                  .Append(r.Fauceted).Append(',').Append(r.Sunk).Append(',').Append(r.Escrow).Append(',')
                  .Append(r.FaucetInitialSeed).Append(',').Append(r.FaucetCaravanSalvage).Append(',').Append(r.SinkSupplyCaravan).Append(',').Append(r.SinkUpkeep).Append(',')
                  .Append(f.Purchases).Append(',').Append(f.PurchaseGold).Append(',').Append(f.Sales).Append(',').Append(f.SaleGold).Append(',')
                  .Append(f.EscrowLockedGold).Append(',').Append(f.EscrowReleasedGold).Append(',').Append(f.EscrowRefundedGold).Append(',')
                  .Append(r.CrawlerGoldTotal).Append(',').Append(r.CrawlerGoldMin).Append(',').Append(r.CrawlerGoldMax).Append(',')
                  .Append(r.CrawlerHungerMean.ToString("F1", inv)).Append(',').Append(r.CrawlersHungry).Append(',').Append(r.CrawlersStarving).Append(',')
                  .Append(r.Restocks).Append(',').Append(r.SkippedRestocks).Append(',').Append(r.SalvageVisits).Append(',').Append(r.UpkeepShort).Append(',').Append(r.InArrearsNow).Append(',')
                  .Append(r.Regrowths).Append(',').Append(r.DormantNodes).Append(',').Append(r.ActiveBounties).Append(',')
                  .Append(r.RationsHarvested).Append(',').Append(r.OreHarvested).Append(',').Append(r.HerbsHarvested).Append(',')
                  .Append(r.BunkSleeps).Append(',').Append(r.SleepingInBunkNow).Append(',').Append(r.RestingInPlaceNow).Append(',')
                  .Append(r.Collapses).Append(',').Append(r.StarvingEpisodes).Append(',')
                  .Append(r.TrustMean.ToString("F1", inv)).Append(',').Append(r.TrustMin).Append(',').Append(r.TrustMax).Append(',')
                  .Append(r.TrustUp).Append(',').Append(r.TrustDown).Append(',')
                  .Append(r.ConservationOk ? 1 : 0).Append(',').Append(r.Discrepancy)
                  .AppendLine();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(CsvPath));
            File.WriteAllText(CsvPath, sb.ToString());
            _csvWritten = true;
        }

        private void LogSummary()
        {
            if (_rowCount == 0) return;
            var first = _rows[0];
            var last = _rows[_rowCount - 1];
            Debug.Log($"[EconomySoakScenario] profile {Profile.Name} ({Profile.RationPrice}/{Profile.OreBuyPrice}/{Profile.HerbBuyPrice}), seed {_seed}, {_observedTicks} ticks: " +
                      $"treasury {first.Treasury}g -> {last.Treasury}g, crawler gold {first.CrawlerGoldTotal}g -> {last.CrawlerGoldTotal}g, " +
                      $"faucets {last.Fauceted}g, sinks {last.Sunk}g, purchases {last.Flows.Purchases} ({last.Flows.PurchaseGold}g), " +
                      $"sales {last.Flows.Sales} ({last.Flows.SaleGold}g), bounty payouts {last.Flows.EscrowReleasedGold}g, " +
                      $"restocks {last.Restocks} (skipped {last.SkippedRestocks}), caravan salvage visits {last.SalvageVisits}, upkeep {last.SinkUpkeep}g (short {last.UpkeepShort}), regrowths {last.Regrowths}; " +
                      $"harvested rations {last.RationsHarvested} / ore {last.OreHarvested} / herbs {last.HerbsHarvested}, bunk sleeps {last.BunkSleeps}, " +
                      $"collapses {last.Collapses}, starving episodes {last.StarvingEpisodes}; " +
                      $"trust mean {last.TrustMean:F1} (min {last.TrustMin}, max {last.TrustMax}), up {last.TrustUp} / down {last.TrustDown}. CSV: {CsvPath}");
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            // Integrity, then the profile's pace band (review §3.3 D5)
            Assert.GreaterOrEqual(_observedTicks, (long)HorizonTicks,
                $"Soak ended early at tick {_observedTicks} (wall-clock timeout); simulation ran too slowly.");
            Assert.AreEqual(0, _conservationViolations,
                $"Ledger conservation violated {_conservationViolations} times (worst discrepancy {_worstDiscrepancy}g).");
            Assert.IsTrue(_csvWritten && File.Exists(CsvPath), $"Soak CSV was not written ({CsvPath}).");
            Assert.IsTrue(_pricesApplied, $"Profile {Profile.Name} prices {Profile.RationPrice}/{Profile.OreBuyPrice}/{Profile.HerbBuyPrice} not applied (stall {_pricesSeen}).");
            Assert.AreEqual(MaxRows - 1, _rowCount, $"Expected {MaxRows - 1} samples (every {SampleIntervalTicks} ticks incl. tick 0 and the horizon), got {_rowCount}.");
            AssertPace();
        }

        /// <summary>The profile's pace band (docs/economy-model.md §7).</summary>
        private void AssertPace()
        {
            var last = _rows[_rowCount - 1];
            uint minTreasury = uint.MaxValue;
            int peakTrustUp = 0;
            float lowestTrustMean = float.MaxValue;
            bool everInArrears = false;
            for (int i = 0; i < _rowCount; i++)
            {
                if (_rows[i].Treasury < minTreasury) minTreasury = _rows[i].Treasury;
                if (_rows[i].TrustUp > peakTrustUp) peakTrustUp = _rows[i].TrustUp;
                if (_rows[i].TrustMean < lowestTrustMean) lowestTrustMean = _rows[i].TrustMean;
                if (_rows[i].InArrearsNow != 0) everInArrears = true;
            }

            switch (Profile.Name)
            {
                case "fair":
                    Assert.That(last.Treasury, Is.InRange(FairTreasuryMin, FairTreasuryMax),
                        $"Fair: treasury {last.Treasury}g at 30 min, outside {FairTreasuryMin}-{FairTreasuryMax}g.");
                    Assert.GreaterOrEqual(last.TrustUp, FairTrustUpMin, $"Fair: trust rose for only {last.TrustUp} crawlers (need {FairTrustUpMin}).");
                    Assert.AreEqual(0, last.StarvingEpisodes, $"Fair: {last.StarvingEpisodes} starving episodes (need 0).");
                    break;
                case "greedy":
                    Assert.LessOrEqual(last.TrustUp, GreedyTrustUpMax, $"Greedy: trust rose for {last.TrustUp} crawlers (at most {GreedyTrustUpMax}).");
                    // At its lowest, as generous is judged at its peak: once greed has emptied the stall nobody trades,
                    // and trust drifts back to neutral by minute 30 (Epic 10 G0)
                    Assert.Less(lowestTrustMean, 0f, $"Greedy: trust mean never fell below 0 (lowest {lowestTrustMean:F1}).");
                    break;
                case "generous":
                    Assert.IsTrue(minTreasury == 0 || everInArrears, $"Generous: the stall never went broke (lowest treasury {minTreasury}g).");
                    Assert.GreaterOrEqual(peakTrustUp, GenerousTrustUpPeakMin, $"Generous: trust rose for at most {peakTrustUp} crawlers (need {GenerousTrustUpPeakMin} at its peak).");
                    break;
            }
        }
    }
}
