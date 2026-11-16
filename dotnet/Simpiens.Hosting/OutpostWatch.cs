using System.Collections.Generic;
using System.Linq;
using System.Text;
using Simpiens.Cognition;
using Simpiens.Cognition.Memory;
using Simpiens.Core.Session;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.People;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Hosting
{
    /// <summary>
    /// What the Definition-of-Done scenarios watch on the live outpost, checked every tick of a headless run (Unity's
    /// scenarios check every frame): ledger conservation, starvation, the fog of war before the scout's report, the
    /// seeded bounties, the first sale, the supply faucets, each crawler's trust toward the player, and that every crawler
    /// has a look and a name of its own (Epic UI-Beta B1). Nothing is
    /// injected: it only reads. Shared by the headless DoD tests and the console host's seed sweep.
    /// </summary>
    public sealed class OutpostWatch
    {
        /// <summary>The outpost's day (Epic MAP's DoD): a game day from 06:00, 10,080 ticks (Epic 10 Part 2, milestone T; 9,000 before).</summary>
        public const long DayTicks = DayClock.TicksPerDay;

        /// <summary>Starving: hunger 95 or more for 2 game hours (on the 24-hour day hunger fills over a day; Epic 10 Part 2, T5).</summary>
        public const float StarvingHunger = 95f;
        public const uint StarvingTicks = 2 * DayClock.TicksPerHour;

        /// <summary>
        /// The seeds whose day must hold (Epic 10 G0, finding F2): the three golden seeds and 1 to 20. `dotnet test`
        /// plays each; `simpiens sweep --seeds 424242,20261009,20261010,1..20` prints them.
        /// </summary>
        public static readonly ulong[] GateSeeds =
        {
            424242UL, 20261009UL, 20261010UL,
            1UL, 2UL, 3UL, 4UL, 5UL, 6UL, 7UL, 8UL, 9UL, 10UL, 11UL, 12UL, 13UL, 14UL, 15UL, 16UL, 17UL, 18UL, 19UL, 20UL
        };


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

            /// <summary>It died (Epic 10 Part 3, K5): the day judges the living; deaths are counted apart.</summary>
            public bool Died;

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

        private readonly LocalSimulation _sim;
        private readonly int _baselineSalvage;
        private readonly long _startTick;

        public readonly List<Crawler> Crawlers = new List<Crawler>(16);
        public long ScoutTick = -1, GatherTick = -1, SaleTick = -1, RestockTick = -1, RegrowthTick = -1;
        public bool ScoutCharted, GatherEscrowReleased;
        public int GatherClaimantTrust;
        public int ConservationViolations, WorstDiscrepancy, FogLeaks;
        public string FirstFogLeak;

        /// <summary>
        /// Crawler-ticks spent alone where a hazard is past the lone limit through what the crawler wears (Epic 10 G5, D11;
        /// D51 since Epic 10 Part 2's W3): the Drowned Gallery is an expedition's unless a crawler wears a respirator, and a
        /// party's members don't count.
        /// </summary>
        public int AloneInTheChamber;
        public string FirstAloneInTheChamber;
        public long ObservedTicks;

        /// <summary>
        /// Jitter, stacking, wall penetration, boundary breaches and state thrashing, judged every tick (Epic CLIENT M2.8:
        /// the checks of Unity's visual anomaly detector, on the simulation's own data).
        /// </summary>
        public AnomalyDetector Anomalies { get; }

        /// <summary>Pawn stacking (Epic 10 G0), and the first one seen.</summary>
        public int Stackings => Anomalies.StackingCount;
        public string FirstStacking { get; private set; }

        /// <summary>Drifters seen while watching (Epic 10 G1): arrived, walked out, and what those who left bought.</summary>
        public int DriftersArrived, DriftersLeft, DrifterPurchases;

        /// <summary>Deaths (Epic 10 Part 3, K5.1, K5.4), residents' among them, and the first ("t1234: Name of Hazard at (x, y)").</summary>
        public int Deaths, ResidentDeaths;
        public string FirstDeath;
        private readonly int _driftersAtStart;
        private readonly int _faultsAtStart;

        /// <summary>
        /// Who's who (Epic UI-Beta B1), every crawler present, drifters included: ticks a crawler had no valid look or no
        /// given name, and ticks two living crawlers shared a name; any fails the day.
        /// </summary>
        public int Faceless, SharedNames;
        public string FirstIdentityFault;
        private readonly HashSet<string> _livingNames = new HashSet<string>();

        /// <summary>
        /// Creatures (Epic 10 Part 3, K2.3), every crawler present, drifters included: blows that fell (hit or miss), the
        /// crawlers they fell on, those that hurt, flights begun, crawlers a blow downed, when each of the map's lairs was
        /// charted (-1 for never), and lone entries into a lair after it had been charted for <see cref="KnownLairGraceTicks"/>:
        /// a crawler on its feet, off an expedition, not fleeing, not one who braves the lair (the bold and armed, Epic 10 Part
        /// 3, K3.3, D21), stepping from outside to within its reach less <see cref="LairEdgeBand"/> (a passer-by grazing the
        /// edge isn't going into it). Any entry fails the day.
        /// </summary>
        public int Blows, Hits, Flights, DownedByCreatures, EntriesIntoKnownLairs;

        /// <summary>Fighting back (Epic 10 Part 3, K3.3): fights begun, creatures slain while watched, and the most residents armed at once.</summary>
        public int Fights, Kills, MostArmed;
        private readonly int _killsAtStart;

        /// <summary>The rim of a lair's reach that a path may graze on its way past: an entry is deeper than this.</summary>
        public const float LairEdgeBand = 1f;
        public readonly HashSet<EntityId> Struck = new HashSet<EntityId>();
        public long[] LairChartedAt = System.Array.Empty<long>();
        public string FirstEntryIntoKnownLair, FirstFlight;

        /// <summary>A crawler that decided to go before a lair was charted may still be on its way: entries count from a game hour after.</summary>
        public const long KnownLairGraceTicks = DayClock.TicksPerHour;

        private readonly Dictionary<EntityId, (uint Struck, bool Fleeing, bool InChartedReach, bool Downed, bool Fighting)> _creatureWatch = new Dictionary<EntityId, (uint, bool, bool, bool, bool)>();

        /// <summary>Intent handlers that threw while watched (ROADMAP §1A.2 #17); any fails the day.</summary>
        public int IntentFaults => _sim.Simulation.IntentFaults - _faultsAtStart;

        public OutpostWatch(LocalSimulation sim)
        {
            _sim = sim;
            _startTick = sim.Clock.CurrentTick;
            _baselineSalvage = TotalSalvage(sim.Simulation);
            _faultsAtStart = sim.Simulation.IntentFaults;
            _killsAtStart = sim.Simulation.Creatures.Kills;
            Anomalies = AnomalyDetector.For(sim);
            Anomalies.AnomalyFound += record =>
            {
                if (record.Type == AnomalyType.PawnStacking) FirstStacking ??= $"t{record.Tick - _startTick}: {record.Description} at {record.Position}";
            };
            var host = sim.Host;
            _driftersAtStart = host.DriftersArrived;
            host.CrawlerDeparted += (runtime, entity) =>
            {
                var gone = runtime.State;
                if (gone.DiedOf != Simpiens.Simulation.DeathCause.None)
                {
                    Deaths++;
                    if (!gone.IsDrifter) ResidentDeaths++;
                    FirstDeath ??= $"t{_sim.Clock.CurrentTick - _startTick}: {gone.GivenName} of {gone.DiedOf} at {gone.Position}";
                    for (int i = 0; i < Crawlers.Count; i++) if (Crawlers[i].State == gone) Crawlers[i].Died = true;
                    return;
                }
                DriftersLeft++;
                DrifterPurchases += gone.Activity.RationPurchases;
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
            var map = _sim.Host.World?.Map;
            _livingNames.Clear();
            for (int i = 0; i < present.Count; i++)
            {
                var state = present[i];
                if (state.Inventory != null) agentGold += (uint)state.Inventory.Gold;
                // Past the lone limit through what it wears (Epic 10 Part 2, D51: a respirator opens the Gallery to the bold), off an expedition
                if (Vitality.LoneDanger(Vitality.HazardAt(map, state.Position), state.Inventory) > Vitality.LoneHazardLimit && !sim.Guild.IsOnExpedition(state.AgentId))
                {
                    AloneInTheChamber++;
                    FirstAloneInTheChamber ??= $"t{rel}: {_sim.Host.CrawlerNames[i]} at {state.Position}";
                }
                if (!LookCatalog.IsValid(state.Look) || string.IsNullOrEmpty(state.GivenName))
                {
                    Faceless++;
                    FirstIdentityFault ??= $"t{rel}: {_sim.Host.CrawlerNames[i]} has look [{state.Look}] and name '{state.GivenName}'";
                }
                else if (!_livingNames.Add(state.GivenName))
                {
                    SharedNames++;
                    FirstIdentityFault ??= $"t{rel}: two living crawlers are named {state.GivenName}";
                }
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

            ObserveCreatures(rel, (uint)tick, map);
            Anomalies.Observe(tick);

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

        private void ObserveCreatures(long rel, uint tick, Simpiens.Simulation.World.MapDefinition map)
        {
            var creatures = _sim.Simulation.Creatures;
            int lairCount = map != null ? map.Lairs.Count : 0;
            if (LairChartedAt.Length != lairCount)
            {
                LairChartedAt = new long[lairCount];
                for (int i = 0; i < lairCount; i++) LairChartedAt[i] = -1;
            }
            for (int i = 0; i < lairCount; i++)
                if (LairChartedAt[i] < 0 && creatures.IsCharted(i)) LairChartedAt[i] = rel;

            Kills = creatures.Kills - _killsAtStart;
            var present = _sim.Host.CrawlerStates;
            int armed = 0;
            for (int i = 0; i < present.Count; i++)
            {
                var state = present[i];
                _creatureWatch.TryGetValue(state.AgentId, out var was);
                bool fleeing = state.CurrentIntent is Simpiens.Cognition.Contracts.FleeIntent;
                bool fighting = state.CurrentIntent is Simpiens.Cognition.Contracts.FightIntent;
                if (fighting && !was.Fighting) Fights++;
                if (!state.IsDrifter && Vitality.IsArmed(state.Inventory)) armed++;
                if (state.StruckAtTick != 0 && state.StruckAtTick != was.Struck)
                {
                    Blows++;
                    if (state.HurtThisTick > 0f) Hits++;
                    Struck.Add(state.AgentId);
                    if (state.IsDowned && !was.Downed) DownedByCreatures++;
                }
                if (fleeing && !was.Fleeing)
                {
                    Flights++;
                    FirstFlight ??= $"t{rel}: {_sim.Host.CrawlerNames[i]} at {state.Position}";
                }
                // In a known lair's reach (by where it stands), and whether it braves that lair (K3.3, D21): an entry is a step
                // into the reach unbraved, not a brave crawler's health falling while it is there (K6.4: a hurt hunter)
                // A step is judged by where it stands, charted or not: one already standing in a lair's reach when its grace ran out
                // didn't step in (Epic 10 Part 3, K10.1b: seed 64's wanderer, who charted the hollow on its way in and stood there)
                bool inKnownLair = false, inChartedReach = false, braves = false;
                for (int l = 0; l < lairCount && !inKnownLair; l++)
                {
                    float deep = Simpiens.Simulation.Creatures.LairReach.Of(map.Lairs[l]) - LairEdgeBand;
                    bool within = LairChartedAt[l] >= 0 && deep > 0f && (state.Position - map.Lairs[l].Position).SqrMagnitude <= deep * deep;
                    inChartedReach |= within;
                    inKnownLair = within && rel - LairChartedAt[l] >= KnownLairGraceTicks;
                    if (inKnownLair) braves = Vitality.BravesLair(map.Lairs[l], _sim.Simulation.Creatures.IsBossAlive(l), state.Skills, state.Inventory, state.Health, state.Personality.RiskTolerancePercent, state.IsDowned);
                }
                // A victor stepping in for its own kill's drop at hand is D24's, not a lone venture
                bool ownDrop = state.CurrentIntent is Simpiens.Cognition.Contracts.PickUpIntent pick && _sim.Simulation.Piles.TryGet(pick.PileId, out var pile) && pile.ClaimedFor == state.AgentId;
                if (inKnownLair && !was.InChartedReach && !braves && !ownDrop && !fleeing && !state.IsDowned && !Vitality.StruckRecently(state.StruckAtTick, tick) && !_sim.Simulation.Guild.IsOnExpedition(state.AgentId))
                {
                    EntriesIntoKnownLairs++;
                    FirstEntryIntoKnownLair ??= $"t{rel}: {_sim.Host.CrawlerNames[i]} at {state.Position} ({state.CurrentIntent?.GetType().Name})";
                }
                _creatureWatch[state.AgentId] = (state.StruckAtTick, fleeing, inChartedReach, state.IsDowned, fighting);
            }
            if (armed > MostArmed) MostArmed = armed;
        }

        /// <summary>The creatures' line of the report (Epic 10 Part 3, K2.3).</summary>
        public string CreatureSummary() =>
            $"creatures: {Blows} blows ({Hits} hit) on {Struck.Count} crawlers, {Flights} flights{(FirstFlight != null ? $" (first {FirstFlight})" : "")}, {DownedByCreatures} downed by them; lairs charted at [{string.Join(", ", LairChartedAt.Select(t => t < 0 ? "never" : "t" + t))}]; lone entries into a known lair {EntriesIntoKnownLairs}{(FirstEntryIntoKnownLair != null ? $" (first {FirstEntryIntoKnownLair})" : "")}; armed at most {MostArmed}, fights {Fights}, kills {Kills}; deaths {Deaths}{(FirstDeath != null ? $" (first {FirstDeath})" : "")}";

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
        /// What fails the outpost's day (Epic MAP's DoD, OutpostLifecycleScenario's assertions, with every anomaly type but
        /// state thrashing, which the views judge) after a 9,000-tick run and <see cref="Finish"/>. Empty when the day
        /// emerged.
        /// </summary>
        public List<string> DayFailures()
        {
            var failures = new List<string>();
            // The living residents (Epic 10 Part 3, K5): a death is counted apart, and TheCarefulLoseFew bands it
            var living = Crawlers.Where(c => !c.Died).ToList();
            if (living.Count == 0) { failures.Add("no crawlers"); return failures; }
            var noCycle = living.Where(c => c.State.Activity.Cycles == 0).Select(c => c.Name).ToList();
            int sleepers = living.Count(c => c.State.Activity.BunkSleeps > 0);
            int eaters = living.Count(c => c.State.Activity.Meals > 0);
            int gatherings = living.Sum(c => c.State.Activity.HearthGatherings);
            int trustRose = living.Count(c => c.TrustEnd > c.TrustStart);
            int requiredTrustRose = SimMath.CeilToInt(living.Count * 2f / 3f);
            var starved = living.Where(c => c.Starved).Select(c => $"{c.Name} ({c.LongestHungry} ticks)").ToList();

            if (noCycle.Count > 0) failures.Add($"{noCycle.Count} crawlers never closed a harvest -> sell cycle: {string.Join(", ", noCycle)}");
            if (sleepers < 3) failures.Add($"only {sleepers} crawlers slept in a bunk (need 3)");
            if (eaters < 3) failures.Add($"only {eaters} crawlers ate (need 3)");
            if (gatherings == 0) failures.Add("no hearth gathering");
            if (ScoutTick < 0 || !ScoutCharted) failures.Add($"the Scout bounty was not fulfilled with the Deep Seam charted (t{ScoutTick}, charted {ScoutCharted})");
            if (FogLeaks > 0) failures.Add($"crawlers knew Deep Seam landmarks before the scout reported ({FogLeaks}; first {FirstFogLeak})");
            if (EntriesIntoKnownLairs > 0) failures.Add($"crawlers went alone into a known lair ({EntriesIntoKnownLairs}; first {FirstEntryIntoKnownLair})");
            if (AloneInTheChamber > 0) failures.Add($"crawlers went alone where only an expedition goes ({AloneInTheChamber} crawler-ticks; first {FirstAloneInTheChamber})");
            if (GatherTick < 0 || !GatherEscrowReleased) failures.Add($"the Gather bounty was not fulfilled and paid from escrow (t{GatherTick}, released {GatherEscrowReleased})");
            if (trustRose < requiredTrustRose) failures.Add($"trust toward the player rose for only {trustRose} of {living.Count} (need {requiredTrustRose})");
            if (ConservationViolations > 0) failures.Add($"ledger conservation violated {ConservationViolations} times (worst {WorstDiscrepancy}g)");
            if (starved.Count > 0) failures.Add($"{starved.Count} crawlers starved: {string.Join(", ", starved)}");
            if (Stackings > 0) failures.Add($"pawn stacking {Stackings} times (first {FirstStacking})");
            // The rest of Unity's anomaly checks (Epic CLIENT M2.8); thrashing is the views' to judge (AnomalyDetector)
            if (Anomalies.JitterCount + Anomalies.PenetrationCount + Anomalies.BoundaryBreachCount > 0)
                failures.Add($"{Anomalies.Summary()}; first {Anomalies.First}");
            if (IntentFaults > 0) failures.Add($"{IntentFaults} intent handlers threw (first {_sim.Simulation.FirstIntentFault})");
            if (Faceless + SharedNames > 0) failures.Add($"who's who: {Faceless} faceless or nameless, {SharedNames} shared names (first {FirstIdentityFault})");
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
            sb.AppendLine($"observed {ObservedTicks} ticks; scout@{ScoutTick} (charted {ScoutCharted}), gather@{GatherTick} (escrow released {GatherEscrowReleased}), sale@{SaleTick}, restock@{RestockTick}, regrowth@{RegrowthTick}; ledger violations {ConservationViolations}; fog leaks {FogLeaks}; alone in the chamber {AloneInTheChamber}; intent faults {IntentFaults}; drifters in {DriftersArrived}, out {DriftersLeft} (bought {DrifterPurchases}); {Anomalies.Summary()}{(Anomalies.First is AnomalyRecord first ? $" (first {first})" : "")}");
            sb.AppendLine(CreatureSummary());
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
            // Every resource the stall holds for the caravan (the item system's I5: ore and herbs as before, and glowstone)
            var resources = ItemCatalog.InCategory(ItemCategory.Resource);
            for (int i = 0; i < stalls.Count; i++)
            {
                for (int r = 0; r < resources.Count; r++) total += stalls[i].StockOf(resources[r]);
            }
            return total;
        }
    }
}
