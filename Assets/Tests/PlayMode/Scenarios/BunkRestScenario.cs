using Simpiens.Core.Session;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Simpiens.Cognition.Contracts;
using Simpiens.Simulation;
using Simpiens.Testing.Diagnostics;
using Simpiens.Testing.Environment;
using UnityEngine;
using VContainer;

namespace Simpiens.Tests.PlayMode.Scenarios
{
    /// <summary>
    /// Definition-of-Done scenario for the rest anchor (Epic MAP Phase 3, epic §2.3). Runs the real outpost session
    /// on Floor 1 at 10x and injects nothing: crawlers start rested at the Road Gate and discover the Bunkhouse on
    /// their own. Within the bound, at least 3 distinct crawlers complete a sleep in a bunk; no bunk ever holds two
    /// sleepers; every sleeper lies on its bunk and is shown asleep; no crawler starves. Hunger rises 1.5 per second, so brief peaks at the cap are routine; starving
    /// means going without food: hunger at or above StarvingHunger for StarvingTicks in a row.
    /// </summary>
    public class BunkRestScenario : ISimulationScenario
    {
        /// <summary>Starvation and single occupancy are watched this long.</summary>
        public const uint HorizonTicks = 4000;

        /// <summary>The third distinct crawler has slept in a bunk by now: t1375 in 3 of 3 runs, x 1.5.</summary>
        public const uint SleepersDeadlineTicks = 2100;
        public const int RequiredSleepers = 3;
        public const float StarvingHunger = 95f;
        public const uint StarvingTicks = 300; // 30 s without getting food
        private const float WallClockTimeoutSeconds = 200f;

        public string Name => "BunkRestScenario";
        public string SessionId => OutpostSession.SessionId;
        public float DurationSeconds => 0f;
        public float TimeMultiplier => 10f;

        private readonly HashSet<EntityId> _sleepers = new HashSet<EntityId>();
        private readonly Dictionary<EntityId, int> _sleepersPerBunk = new Dictionary<EntityId, int>();
        private long _thirdSleeperTick = -1;
        private long _firstSleepTick = -1;
        private int _doubleOccupancy;
        private string _firstDoubleOccupancy;
        private int _misplacedSleepers;
        private string _firstMisplacedSleeper;
        private int _starving;
        private int _collapses;
        private long _observedTicks;
        private int _fieldRests;
        private float _peakHunger;
        private string _firstStarving;
        private long _longestHungry;

        public IEnumerator SetupAsync(IObjectResolver resolver)
        {
            var clock = resolver.Resolve<ISimulationClock>();
            var sim = resolver.Resolve<ISimulationManager>() as SimulationManager;
            var world = resolver.Resolve<IWorldRegistry>();
            var agentRegistry = resolver.Resolve<IAgentRegistry>();
            var spawner = resolver.Resolve<SwarmSpawner>();
            Assert.IsNotNull(sim, "SimulationManager not registered.");
            Assert.IsNotNull(spawner, "SwarmSpawner not registered.");
            if (clock is SimulationClock simClock) simClock.SetTimeMultiplier(TimeMultiplier);

            long start = clock.CurrentTick;
            float elapsed = 0f;
            var collapsed = new HashSet<EntityId>();
            var starving = new HashSet<EntityId>();
            var hungrySince = new Dictionary<EntityId, long>();
            var resting = new HashSet<EntityId>();

            while (elapsed < WallClockTimeoutSeconds)
            {
                long rel = clock.CurrentTick - start;
                _observedTicks = rel;

                foreach (var kvp in sim.BunkSleeps)
                {
                    if (kvp.Value > 0 && _sleepers.Add(kvp.Key))
                    {
                        if (_firstSleepTick < 0) _firstSleepTick = rel;
                        if (_sleepers.Count == RequiredSleepers) _thirdSleeperTick = rel;
                    }
                }

                // Single occupancy, checked against the sleepers themselves, not the occupancy table: every intent past
                // its first tick has been granted its bunk and is sleeping in it
                _sleepersPerBunk.Clear();
                foreach (var kvp in sim.ActiveIntents)
                {
                    if (!(kvp.Value.Intent is SleepAtBunkIntent sleep) || kvp.Value.ElapsedTime <= 0f) continue;
                    _sleepersPerBunk.TryGetValue(sleep.BunkId, out int n);
                    _sleepersPerBunk[sleep.BunkId] = n + 1;
                    if (n + 1 > 1)
                    {
                        _doubleOccupancy++;
                        _firstDoubleOccupancy ??= $"t{rel}: bunk {sleep.BunkId}";
                    }

                    // The sleeper lies in the bunk and is shown asleep
                    var bunk = world?.GetEntity(sleep.BunkId);
                    if (bunk != null && agentRegistry != null && agentRegistry.TryGetAgent(kvp.Key, out var sleeper)
                        && (Vec2.Distance(sleeper.Position, bunk.Position) > SimulationManager.BunkSettleDistance
                            || sleeper.VisualState != Simpiens.Entities.AgentVisualState.Sleeping))
                    {
                        _misplacedSleepers++;
                        _firstMisplacedSleeper ??= $"t{rel}: {sleeper.Position} for bunk {bunk.Position}, shown {sleeper.VisualState}";
                    }
                }

                var agents = spawner.Agents;
                for (int i = 0; i < agents.Count; i++)
                {
                    var state = agents[i] != null ? agents[i].State : null;
                    if (state == null) continue;
                    if (state.Hunger > _peakHunger) _peakHunger = state.Hunger;
                    if (state.Hunger >= StarvingHunger)
                    {
                        if (!hungrySince.TryGetValue(state.AgentId, out long since)) hungrySince[state.AgentId] = since = rel;
                        if (rel - since > _longestHungry) _longestHungry = rel - since;
                        if (rel - since >= StarvingTicks && starving.Add(state.AgentId))
                        {
                            _starving++;
                            _firstStarving ??= Describe(agents[i], state, rel);
                        }
                    }
                    else
                    {
                        hungrySince.Remove(state.AgentId);
                    }
                    if (state.IsExhaustionCollapsed && collapsed.Add(state.AgentId)) _collapses++;
                    if (state.HasActivePlan && state.ActivePlan.CurrentAction is Simpiens.Cognition.Planning.Actions.RestAction && resting.Add(state.AgentId)) _fieldRests++;
                }

                if (rel >= HorizonTicks) break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static string Describe(Simpiens.Cognition.AutonomousAgent agent, Simpiens.Cognition.AgentState state, long rel)
        {
            int knownFood = 0;
            if (state.Memory != null)
            {
                foreach (var kvp in state.Memory.SpatialMemoryMap)
                {
                    if (kvp.Value.Type == Simpiens.Simulation.Spatial.EntityType.Resource && kvp.Value.ResourceKind == Simpiens.Simulation.Economy.ItemType.Ration) knownFood++;
                }
            }
            string goal = state.HasActivePlan ? state.ActivePlan.ToString() : "no plan";
            return $"t{rel} {agent.name} at {state.Position}: hunger {state.Hunger:F0}, energy {state.Energy:F0}, gold {state.Inventory?.Gold}, " +
                   $"visual {state.VisualState}, intent {state.CurrentIntent?.GetType().Name ?? "none"}, collapsed {state.IsExhaustionCollapsed}, " +
                   $"knows {knownFood} ration nodes, plan {goal}";
        }

        public void AssertResults(IVisualAnomalyDetector detector)
        {
            if (_firstStarving != null) Debug.Log($"[BunkRestScenario] first starving crawler: {_firstStarving}");
            Debug.Log($"[BunkRestScenario] first sleep@{_firstSleepTick}, {RequiredSleepers} sleepers@{_thirdSleeperTick}, {_sleepers.Count} distinct sleepers, {_fieldRests} crawlers rested in place, {_collapses} collapsed, {_starving} starving (peak hunger {_peakHunger:F0}, longest at >= {StarvingHunger:F0}: {_longestHungry} ticks), observed {_observedTicks}");

            Assert.GreaterOrEqual(_observedTicks, (long)HorizonTicks, $"Observation ended early at tick {_observedTicks} (wall-clock timeout).");
            Assert.GreaterOrEqual(_sleepers.Count, RequiredSleepers, $"Only {_sleepers.Count} crawlers slept in a bunk within {HorizonTicks} ticks.");
            Assert.IsTrue(_thirdSleeperTick >= 0 && _thirdSleeperTick <= SleepersDeadlineTicks,
                $"The third crawler slept in a bunk at {_thirdSleeperTick}, after the {SleepersDeadlineTicks}-tick deadline.");
            Assert.AreEqual(0, _doubleOccupancy, $"A bunk held two sleepers ({_doubleOccupancy} observations; first {_firstDoubleOccupancy}).");
            Assert.AreEqual(0, _misplacedSleepers, $"A sleeper was off its bunk or not shown asleep ({_misplacedSleepers} observations; first {_firstMisplacedSleeper}).");
            Assert.AreEqual(0, _starving, $"{_starving} crawlers starved (hunger >= {StarvingHunger} for {StarvingTicks} ticks).");
            Assert.AreEqual(0, detector.StackingCount, $"Pawn stacking detected ({detector.StackingCount}).");
        }
    }
}
