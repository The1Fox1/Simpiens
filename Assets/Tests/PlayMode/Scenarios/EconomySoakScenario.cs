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
    /// Milestone 1 closure, Phase 3: economy OBSERVATION soak. Runs the real session (SwarmSpawner.Respawn) for
    /// 30 sim-minutes at 10x with no injection and no player commands. Every sim-minute it records stall, ledger,
    /// transfer, crawler, pen and supply figures to a CSV. It asserts integrity only (ledger conservation,
    /// horizon reached, CSV written), never economic values: economy design is deferred by the producer.
    /// Seed and provisional-export switch come from SimulationSettings (SIMPIENS_SEED / SIMPIENS_PROVISIONAL_EXPORT).
    /// </summary>
    public class EconomySoakScenario : ISimulationScenario
    {
        public const uint HorizonTicks = 18000;     // 30 sim-minutes at 10 Hz
        public const uint SampleIntervalTicks = 600; // one caravan cycle = one sim-minute
        private const float WallClockTimeoutSeconds = 600f;
        private const int MaxRows = (int)(HorizonTicks / SampleIntervalTicks) + 2;

        public string Name => "EconomySoakScenario";
        public float DurationSeconds => 0f; // SetupAsync observes until the horizon
        public float TimeMultiplier => 10f;

        public string CsvPath { get; private set; }

        private struct Row
        {
            public long Tick;
            public uint Treasury, RationStock, OreStock, HerbStock, RationPrice, OreBuyPrice, HerbBuyPrice;
            public uint Fauceted, Sunk, Escrow, FaucetInitialSeed, FaucetProvisionalExport, SinkSupplyCaravan;
            public EconomyFlowTotals Flows;
            public int CrawlerGoldTotal, CrawlerGoldMin, CrawlerGoldMax, CrawlersHungry, CrawlersStarving;
            public float CrawlerHungerMean;
            public int PenGoldTotal;
            public float PenHungerMean;
            public int Restocks, SkippedRestocks, Exports, Regrowths, DormantNodes, ActiveBounties;
            public bool ConservationOk;
            public int Discrepancy;
        }

        private readonly Row[] _rows = new Row[MaxRows];
        private int _rowCount;
        private ulong _seed;
        private bool _exportEnabled;
        private long _observedTicks;
        private int _conservationViolations;
        private int _worstDiscrepancy;
        private bool _csvWritten;

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            var sim = resolver.Resolve<ISimulationManager>();
            var spawner = resolver.Resolve<SwarmSpawner>();
            var settings = resolver.Resolve<SimulationSettings>();
            Assert.IsNotNull(clock, "ISimulationClock not registered.");
            Assert.IsNotNull(sim, "ISimulationManager not registered.");
            Assert.IsNotNull(spawner, "SwarmSpawner not registered.");
            Assert.IsNotNull(sim.EconomyLedger, "IEconomyLedger not wired into the SimulationManager.");
            Assert.IsNotNull(sim.Supply, "ISupplyService not wired into the SimulationManager.");

            _seed = settings != null ? settings.Seed : SimulationSettings.DefaultSeed;
            _exportEnabled = settings == null || settings.ProvisionalExportEnabled;
            CsvPath = $"Logs/economy-soak-seed{_seed}-export{(_exportEnabled ? "on" : "off")}.csv";

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
                if (rel >= nextSample)
                {
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
                if (Mathf.Abs(discrepancy) > Mathf.Abs(_worstDiscrepancy)) _worstDiscrepancy = discrepancy;
            }
            return ok;
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
                FaucetProvisionalExport = ledger.GetFaucetTotal(SupplyService.ProvisionalExportFaucetName),
                SinkSupplyCaravan = ledger.GetSinkTotal(SupplyService.CaravanSinkName),
                Flows = ledger.Flows,
                Restocks = supply.RestockCount,
                SkippedRestocks = supply.SkippedRestockCount,
                Exports = supply.ExportCount,
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
                var stall = stalls[0]; // the session has one stall (Outpost Commons)
                row.Treasury = stall.TreasuryGold;
                row.RationStock = stall.RationStock;
                row.OreStock = stall.OreStock;
                row.HerbStock = stall.HerbStock;
                row.RationPrice = stall.RationSellPrice;
                row.OreBuyPrice = stall.OreBuyPrice;
                row.HerbBuyPrice = stall.HerbBuyPrice;
            }

            // Crawlers (index >= ScriptedPenAgentCount) can reach the stall; walled pen agents would only dilute them
            int crawlers = 0, pens = 0;
            float crawlerHunger = 0f, penHunger = 0f;
            var agents = spawner.Agents;
            for (int i = 0; i < agents.Count; i++)
            {
                var state = agents[i] != null ? agents[i].State : null;
                if (state == null) continue;
                int gold = state.Inventory != null ? state.Inventory.Gold : 0;
                float hunger = state.Needs.Hunger;

                if (i >= SwarmSpawner.ScriptedPenAgentCount)
                {
                    crawlers++;
                    row.CrawlerGoldTotal += gold;
                    if (gold < row.CrawlerGoldMin) row.CrawlerGoldMin = gold;
                    if (gold > row.CrawlerGoldMax) row.CrawlerGoldMax = gold;
                    crawlerHunger += hunger;
                    if (hunger >= 60f) row.CrawlersHungry++;
                    if (hunger >= 85f) row.CrawlersStarving++;
                }
                else
                {
                    pens++;
                    row.PenGoldTotal += gold;
                    penHunger += hunger;
                }
            }
            if (crawlers == 0) { row.CrawlerGoldMin = 0; row.CrawlerGoldMax = 0; }
            row.CrawlerHungerMean = crawlers > 0 ? crawlerHunger / crawlers : 0f;
            row.PenHungerMean = pens > 0 ? penHunger / pens : 0f;

            _rows[_rowCount++] = row;
        }

        private const string Header =
            "seed,export_enabled,tick,sim_minute," +
            "treasury,ration_stock,ore_stock,herb_stock,ration_price,ore_buy_price,herb_buy_price," +
            "fauceted_total,sunk_total,escrow,faucet_initial_seed,faucet_provisional_export,sink_supply_caravan," +
            "purchases,purchase_gold,sales,sale_gold,escrow_locked,escrow_released,escrow_refunded," +
            "crawler_gold_total,crawler_gold_min,crawler_gold_max,crawler_hunger_mean,crawlers_hungry,crawlers_starving," +
            "pen_gold_total,pen_hunger_mean," +
            "restocks,skipped_restocks,exports,regrowths,dormant_nodes,active_bounties," +
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
                sb.Append(_seed).Append(',').Append(_exportEnabled ? 1 : 0).Append(',')
                  .Append(r.Tick).Append(',').Append((r.Tick / (double)SampleIntervalTicks).ToString("F1", inv)).Append(',')
                  .Append(r.Treasury).Append(',').Append(r.RationStock).Append(',').Append(r.OreStock).Append(',').Append(r.HerbStock).Append(',')
                  .Append(r.RationPrice).Append(',').Append(r.OreBuyPrice).Append(',').Append(r.HerbBuyPrice).Append(',')
                  .Append(r.Fauceted).Append(',').Append(r.Sunk).Append(',').Append(r.Escrow).Append(',')
                  .Append(r.FaucetInitialSeed).Append(',').Append(r.FaucetProvisionalExport).Append(',').Append(r.SinkSupplyCaravan).Append(',')
                  .Append(f.Purchases).Append(',').Append(f.PurchaseGold).Append(',').Append(f.Sales).Append(',').Append(f.SaleGold).Append(',')
                  .Append(f.EscrowLockedGold).Append(',').Append(f.EscrowReleasedGold).Append(',').Append(f.EscrowRefundedGold).Append(',')
                  .Append(r.CrawlerGoldTotal).Append(',').Append(r.CrawlerGoldMin).Append(',').Append(r.CrawlerGoldMax).Append(',')
                  .Append(r.CrawlerHungerMean.ToString("F1", inv)).Append(',').Append(r.CrawlersHungry).Append(',').Append(r.CrawlersStarving).Append(',')
                  .Append(r.PenGoldTotal).Append(',').Append(r.PenHungerMean.ToString("F1", inv)).Append(',')
                  .Append(r.Restocks).Append(',').Append(r.SkippedRestocks).Append(',').Append(r.Exports).Append(',')
                  .Append(r.Regrowths).Append(',').Append(r.DormantNodes).Append(',').Append(r.ActiveBounties).Append(',')
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
            Debug.Log($"[EconomySoakScenario] seed {_seed}, export {(_exportEnabled ? "on" : "off")}, {_observedTicks} ticks: " +
                      $"treasury {first.Treasury}g -> {last.Treasury}g, crawler gold {first.CrawlerGoldTotal}g -> {last.CrawlerGoldTotal}g, " +
                      $"faucets {last.Fauceted}g, sinks {last.Sunk}g, purchases {last.Flows.Purchases} ({last.Flows.PurchaseGold}g), " +
                      $"sales {last.Flows.Sales} ({last.Flows.SaleGold}g), bounty payouts {last.Flows.EscrowReleasedGold}g, " +
                      $"restocks {last.Restocks} (skipped {last.SkippedRestocks}), exports {last.Exports}, regrowths {last.Regrowths}. CSV: {CsvPath}");
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            // Integrity only. Economic values are recorded, never asserted (economy design is deferred).
            Assert.GreaterOrEqual(_observedTicks, (long)HorizonTicks,
                $"Soak ended early at tick {_observedTicks} (wall-clock timeout); simulation ran too slowly.");
            Assert.AreEqual(0, _conservationViolations,
                $"Ledger conservation violated {_conservationViolations} times (worst discrepancy {_worstDiscrepancy}g).");
            Assert.IsTrue(_csvWritten && File.Exists(CsvPath), $"Soak CSV was not written ({CsvPath}).");
            Assert.AreEqual(MaxRows - 1, _rowCount, $"Expected {MaxRows - 1} samples (every {SampleIntervalTicks} ticks incl. tick 0 and the horizon), got {_rowCount}.");
        }
    }
}
