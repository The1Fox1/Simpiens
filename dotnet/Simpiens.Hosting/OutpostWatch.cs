using System.Collections.Generic;
using System.Linq;
using System.Text;
using Simpiens.Cognition;
using Simpiens.Cognition.Memory;
using Simpiens.Core.Session;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Hosting
{
    /// <summary>
    /// What the Definition-of-Done scenarios watch on the live outpost, checked every tick of a headless run (Unity's
    /// scenarios check every frame): ledger conservation, starvation, the fog of war before the scout's report, the
    /// seeded bounties, the first sale, the supply faucets and each crawler's trust toward the player. Nothing is
    /// injected: it only reads. Shared by the headless DoD tests and the console host's seed sweep.
    /// </summary>
    public sealed class OutpostWatch
    {
        public const float StarvingHunger = 95f;
        public const uint StarvingTicks = 300;

        /// <summary>
        /// The seeds whose day must hold (Epic 10 G0, finding F2): the three golden seeds and 1 to 20. `dotnet test`
        /// plays each; `simpiens sweep --seeds 424242,20261009,20261010,1..20` prints them.
        /// </summary>
        public static readonly ulong[] GateSeeds =
        {
            424242UL, 20261009UL, 20261010UL,
            1UL, 2UL, 3UL, 4UL, 5UL, 6UL, 7UL, 8UL, 9UL, 10UL, 11UL, 12UL, 13UL, 14UL, 15UL, 16UL, 17UL, 18UL, 19UL, 20UL
        };

        // Unity's VisualAnomalyDetector calls two pawns closer than this for a second "stacked" (Epic 10 G0)
        public const float StackingDistance = 0.35f;
        public const float StackingSeconds = 1f;
        private const float TickSeconds = 0.1f;

        public sealed class Crawler
        {
            public AgentState State;
            public string Name;
            public int TrustStart, TrustEnd;
            public int MealsAtStart, CyclesAtStart;
            public float PeakHunger;
            public long HungrySince = -1;
            public long LongestHungry, LongestHungryFrom = -1;
            public bool Starved;

            /// <summary>Danger (Epic 10 G2): the lowest health seen, and whether it came back to full after being hurt.</summary>
            public float LowestHealth = 100f;
            public bool HurtThenRecovered;
            private bool _hurt;

            internal void ObserveHealth(float health)
            {
                if (health < LowestHealth) LowestHealth = health;
                if (health < HurtBelow) _hurt = true;
                else if (_hurt && health >= RecoveredAt) HurtThenRecovered = true;
            }
        }

        /// <summary>A crawler under this health counts as hurt; back at RecoveredAt it has recovered (Epic 10 G2).</summary>
        public const float HurtBelow = 70f;
        public const float RecoveredAt = 80f;

        // Per pawn, residents and drifters alike: Unity's detector watches every pawn
        private sealed class StackingClock
        {
            public float StackedFor, LastAlert = -10f;
        }
        private readonly Dictionary<EntityId, StackingClock> _stacking = new Dictionary<EntityId, StackingClock>();

        private readonly LocalSimulation _sim;
        private readonly int _baselineSalvage;
        private readonly long _startTick;

        public readonly List<Crawler> Crawlers = new List<Crawler>(16);
        public long ScoutTick = -1, GatherTick = -1, SaleTick = -1, RestockTick = -1, RegrowthTick = -1;
        public bool ScoutCharted, GatherEscrowReleased;
        public int GatherClaimantTrust;
        public int ConservationViolations, WorstDiscrepancy, FogLeaks;
        public string FirstFogLeak;
        public long ObservedTicks;

        /// <summary>Pawn stacking as Unity's anomaly detector counts it, from the simulation's positions.</summary>
        public int Stackings;
        public string FirstStacking;

        /// <summary>Drifters seen while watching (Epic 10 G1): arrived, walked out, and what those who left bought.</summary>
        public int DriftersArrived, DriftersLeft, DrifterPurchases;
        private readonly int _driftersAtStart;

        public OutpostWatch(LocalSimulation sim)
        {
            _sim = sim;
            _startTick = sim.Clock.CurrentTick;
            _baselineSalvage = TotalSalvage(sim.Simulation);
            var host = sim.Host;
            _driftersAtStart = host.DriftersArrived;
            host.CrawlerDeparted += (runtime, entity) =>
            {
                DriftersLeft++;
                DrifterPurchases += runtime.State.Activity.RationPurchases;
            };
            // The day judges the residents: drifters pass through (Epic 10 G1)
            for (int i = 0; i < host.CrawlerStates.Count; i++)
            {
                var state = host.CrawlerStates[i];
                if (state.IsDrifter) continue;
                Crawlers.Add(new Crawler
                {
                    State = state,
                    Name = host.CrawlerNames[i],
                    TrustStart = Trust(state, (uint)_startTick),
                    MealsAtStart = state.Activity.Meals,
                    CyclesAtStart = state.Activity.Cycles
                });
            }
        }

        /// <summary>Observes the world now (after the current tick).</summary>
        public void Observe()
        {
            var sim = _sim.Simulation;
            long tick = _sim.Clock.CurrentTick;
            long rel = tick - _startTick;
            ObservedTicks = rel;

            int delivered = 0;
            if (sim.TryGetBounty(OutpostSession.SeedGatherBountyId, out var gather) && gather.Status == BountyStatus.Completed)
            {
                delivered = gather.TargetQuantity;
                if (GatherTick < 0)
                {
                    GatherTick = rel;
                    GatherEscrowReleased = sim.EconomyLedger != null && !sim.EconomyLedger.HasEscrow(OutpostSession.SeedGatherBountyId);
                    if (sim.Agents != null && sim.Agents.TryGetAgent(gather.ClaimantId, out var claimant) && claimant.Memory != null)
                    {
                        GatherClaimantTrust = claimant.Memory.GetAffinity(SimulationManager.PlayerEntityId, (uint)tick).Trust;
                    }
                }
            }
            if (SaleTick < 0 && TotalSalvage(sim) - _baselineSalvage - delivered > 0) SaleTick = rel;

            if (ScoutTick < 0 && sim.TryGetBounty(OutpostSession.SeedScoutBountyId, out var scout) && scout.Status == BountyStatus.Completed)
            {
                ScoutTick = rel;
                ScoutCharted = _sim.Sectors.IsExplored(OutpostSession.SeedBountySectorId);
            }
            if (ScoutTick < 0) CheckFog(rel);

            if (RestockTick < 0 && sim.Supply != null && sim.Supply.RestockCount > 0) RestockTick = rel;
            if (RegrowthTick < 0 && sim.Supply != null && sim.Supply.RegrowthCount > 0) RegrowthTick = rel;

            DriftersArrived = _sim.Host.DriftersArrived - _driftersAtStart;
            uint agentGold = 0;
            var present = _sim.Host.CrawlerStates;
            for (int i = 0; i < present.Count; i++)
            {
                if (present[i].Inventory != null) agentGold += (uint)present[i].Inventory.Gold;
            }
            for (int i = 0; i < Crawlers.Count; i++)
            {
                var c = Crawlers[i];
                c.ObserveHealth(c.State.Health);
                float hunger = c.State.Hunger;
                if (hunger > c.PeakHunger) c.PeakHunger = hunger;
                if (hunger >= StarvingHunger)
                {
                    if (c.HungrySince < 0) c.HungrySince = rel;
                    long stretch = rel - c.HungrySince;
                    if (stretch > c.LongestHungry) { c.LongestHungry = stretch; c.LongestHungryFrom = c.HungrySince; }
                    if (stretch >= StarvingTicks) c.Starved = true;
                }
                else
                {
                    c.HungrySince = -1;
                }
            }

            CheckStacking(rel);

            if (sim.EconomyLedger != null)
            {
                uint stallGold = 0;
                var stalls = sim.ActiveStalls;
                for (int i = 0; i < stalls.Count; i++) stallGold += stalls[i].TreasuryGold;
                if (!sim.EconomyLedger.ValidateConservation(stallGold, agentGold, out int discrepancy))
                {
                    ConservationViolations++;
                    if (SimMath.Abs(discrepancy) > SimMath.Abs(WorstDiscrepancy)) WorstDiscrepancy = discrepancy;
                }
            }
        }

        // VisualAnomalyDetector.EvaluateStacking, per tick: a pawn accrues time while closer than StackingDistance to a
        // later pawn in the registry (gossip and sharing exempt), loses it twice as fast otherwise, and counts once a
        // second while past StackingSeconds. Unity's lifecycle, supply-loop and save/load scenarios assert none.
        private void CheckStacking(long rel)
        {
            var pawns = _sim.Registry.ActiveEntities;
            float now = rel * TickSeconds;
            for (int i = 0; i < pawns.Count; i++)
            {
                var a = pawns[i];
                if (a.Type != EntityType.Pawn || a.Agent == null) continue;
                if (!_stacking.TryGetValue(a.Id, out var watched)) _stacking[a.Id] = watched = new StackingClock();
                bool stacked = false;
                for (int j = i + 1; j < pawns.Count; j++)
                {
                    var b = pawns[j];
                    if (b.Type != EntityType.Pawn) continue;
                    if (a.Agent.IsGossiping || a.Agent.IsSharing || (b.Agent != null && (b.Agent.IsGossiping || b.Agent.IsSharing))) continue;
                    if (Vec2.Distance(a.Position, b.Position) >= StackingDistance) continue;
                    stacked = true;
                    watched.StackedFor += TickSeconds;
                    if (watched.StackedFor >= StackingSeconds && now - watched.LastAlert >= 1f)
                    {
                        watched.LastAlert = now;
                        Stackings++;
                        FirstStacking ??= $"t{rel}: {a.Name} with {b.Name} at {a.Position}";
                    }
                    break;
                }
                if (!stacked) watched.StackedFor = SimMath.Max(0f, watched.StackedFor - TickSeconds * 2f);
            }
        }

        /// <summary>Steps <paramref name="ticks"/> ticks, observing after each.</summary>
        public void Run(long ticks)
        {
            long end = _sim.Clock.CurrentTick + ticks;
            while (_sim.Clock.CurrentTick < end)
            {
                _sim.Step(1);
                Observe();
            }
        }

        /// <summary>
        /// What fails the outpost's day (Epic MAP's DoD, OutpostLifecycleScenario's assertions; of the anomaly detector's,
        /// only stacking, which the simulation's positions show) after a 9,000-tick run and <see cref="Finish"/>. Empty
        /// when the day emerged.
        /// </summary>
        public List<string> DayFailures()
        {
            var failures = new List<string>();
            if (Crawlers.Count == 0) { failures.Add("no crawlers"); return failures; }
            var noCycle = Crawlers.Where(c => c.State.Activity.Cycles == 0).Select(c => c.Name).ToList();
            int sleepers = Crawlers.Count(c => c.State.Activity.BunkSleeps > 0);
            int eaters = Crawlers.Count(c => c.State.Activity.Meals > 0);
            int gatherings = Crawlers.Sum(c => c.State.Activity.HearthGatherings);
            int trustRose = Crawlers.Count(c => c.TrustEnd > c.TrustStart);
            int requiredTrustRose = SimMath.CeilToInt(Crawlers.Count * 2f / 3f);
            var starved = Crawlers.Where(c => c.Starved).Select(c => $"{c.Name} ({c.LongestHungry} ticks)").ToList();

            if (noCycle.Count > 0) failures.Add($"{noCycle.Count} crawlers never closed a harvest -> sell cycle: {string.Join(", ", noCycle)}");
            if (sleepers < 3) failures.Add($"only {sleepers} crawlers slept in a bunk (need 3)");
            if (eaters < 3) failures.Add($"only {eaters} crawlers ate (need 3)");
            if (gatherings == 0) failures.Add("no hearth gathering");
            if (ScoutTick < 0 || !ScoutCharted) failures.Add($"the Scout bounty was not fulfilled with the Deep Seam charted (t{ScoutTick}, charted {ScoutCharted})");
            if (FogLeaks > 0) failures.Add($"crawlers knew Deep Seam landmarks before the scout reported ({FogLeaks}; first {FirstFogLeak})");
            if (GatherTick < 0 || !GatherEscrowReleased) failures.Add($"the Gather bounty was not fulfilled and paid from escrow (t{GatherTick}, released {GatherEscrowReleased})");
            if (trustRose < requiredTrustRose) failures.Add($"trust toward the player rose for only {trustRose} of {Crawlers.Count} (need {requiredTrustRose})");
            if (ConservationViolations > 0) failures.Add($"ledger conservation violated {ConservationViolations} times (worst {WorstDiscrepancy}g)");
            if (starved.Count > 0) failures.Add($"{starved.Count} crawlers starved: {string.Join(", ", starved)}");
            if (Stackings > 0) failures.Add($"pawn stacking {Stackings} times (first {FirstStacking})");
            return failures;
        }

        /// <summary>Records each crawler's trust toward the player at the end.</summary>
        public void Finish()
        {
            uint end = (uint)_sim.Clock.CurrentTick;
            for (int i = 0; i < Crawlers.Count; i++) Crawlers[i].TrustEnd = Trust(Crawlers[i].State, end);
        }

        // Before the scout reports, nobody may know a landmark inside the Deep Seam (fog of war)
        private void CheckFog(long rel)
        {
            var map = _sim.Host.World?.Map;
            if (map == null) return;
            for (int i = 0; i < Crawlers.Count; i++)
            {
                var memory = Crawlers[i].State.Memory;
                if (memory == null) continue;
                foreach (var kvp in memory.SpatialMemoryMap)
                {
                    var record = kvp.Value;
                    if (!AgentMemory.IsLandmark(record.Type)) continue;
                    if (map.FindSector(record.LastKnownLocation).Id != OutpostSession.SeedBountySectorId) continue;
                    FogLeaks++;
                    FirstFogLeak ??= $"t{rel}: {Crawlers[i].Name} knew {record.Type} at {record.LastKnownLocation}";
                }
            }
        }

        public string Report()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"observed {ObservedTicks} ticks; scout@{ScoutTick} (charted {ScoutCharted}), gather@{GatherTick} (escrow released {GatherEscrowReleased}), sale@{SaleTick}, restock@{RestockTick}, regrowth@{RegrowthTick}; ledger violations {ConservationViolations}; fog leaks {FogLeaks}; drifters in {DriftersArrived}, out {DriftersLeft} (bought {DrifterPurchases}); stacking {Stackings}{(FirstStacking != null ? $" (first {FirstStacking})" : "")}");
            foreach (var c in Crawlers)
            {
                sb.AppendLine($"  {c.Name}: {c.State.Activity}; risk {c.State.Personality.RiskTolerance:+0.00;-0.00}, lowest health {c.LowestHealth:F0}{(c.HurtThenRecovered ? " (recovered)" : "")}; trust {c.TrustStart} -> {c.TrustEnd}; peak hunger {c.PeakHunger:F0}, longest at >= {StarvingHunger:F0}: {c.LongestHungry} ticks{(c.LongestHungryFrom >= 0 ? $" from t{c.LongestHungryFrom}" : "")}{(c.Starved ? " STARVED" : "")}");
            }
            return sb.ToString();
        }

        private static int Trust(AgentState state, uint tick) =>
            state.Memory != null ? state.Memory.GetAffinity(SimulationManager.PlayerEntityId, tick).Trust : 0;

        private static int TotalSalvage(ISimulationManager sim)
        {
            int total = 0;
            var stalls = sim.ActiveStalls;
            for (int i = 0; i < stalls.Count; i++) total += stalls[i].OreStock + stalls[i].HerbStock;
            return total;
        }
    }
}
