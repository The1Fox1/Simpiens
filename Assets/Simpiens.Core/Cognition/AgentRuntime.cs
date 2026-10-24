using Simpiens.Diagnostics;
using ZLogger;
using System;
using System.Collections.Generic;
using System.Threading;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Evaluators;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Planning;
using Simpiens.Entities;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Cognition
{
    /// <summary>
    /// One crawler's tick, engine-free (Epic CORE C5): its drives and emotion timers, group membership, reflexive
    /// pre-emption, the dispatch of its next decision, and perception while it is busy. It used to live in the
    /// <c>AutonomousAgent</c> MonoBehaviour, which also copied the rendered transform into the simulation's position
    /// every tick (ROADMAP §1A.2 #16); now the simulation owns the position and views only read it. Main thread.
    /// </summary>
    public sealed class AgentRuntime : IDisposable
    {
        public const int DefaultVisionRadius = 6;

        /// <summary>Ticks between looks around while carrying out an intent (walking, harvesting, sleeping).</summary>
        public const uint BusyPerceptionIntervalTicks = 5;

        public const float FixedTickDeltaTime = 0.1f;

        private readonly ISpatialPartition _spatialPartition;
        private readonly ICognitiveEvaluator[] _evaluators;
        private readonly ISimulationManager _simulationManager;
        private readonly ISimulationClock _clock;
        private readonly IGoapPlanner _planner;
        private readonly ActionGraph _actionGraph;
        private readonly List<GoapGoal> _goals;
        private readonly IAgentCognitionService _cognitionService;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private long _lastBusyPerceptionTick = -BusyPerceptionIntervalTicks;
        private bool _disposed;

        public AgentRuntime(
            AgentState state,
            ISpatialPartition spatialPartition,
            ICognitiveEvaluator[] evaluators,
            ISimulationManager simulationManager,
            ISimulationClock clock,
            IGoapPlanner planner = null,
            ActionGraph actionGraph = null,
            List<GoapGoal> goals = null,
            IAgentCognitionService cognitionService = null)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            _spatialPartition = spatialPartition;
            _evaluators = evaluators;
            _simulationManager = simulationManager;
            _clock = clock;
            _planner = planner;
            _actionGraph = actionGraph;
            _goals = goals;
            _cognitionService = cognitionService ?? new AgentCognitionService();

            _simulationManager?.Agents?.RegisterAgent(State);
        }

        public AgentState State { get; }

        public EntityId AgentId => State.AgentId;

        /// <summary>Current logical tick, or -1 without a clock (headless). Read-only, for views.</summary>
        public long CurrentSimTick => _clock != null ? _clock.CurrentTick : -1;

        public bool IsDisposed => _disposed;

        /// <summary>One fixed tick of this crawler, after the simulation's own tick.</summary>
        public void Tick(long currentTick)
        {
            if (_disposed) return;
            if (_simulationManager != null && _simulationManager.IsPaused) return;
            if (_clock != null && _clock.IsPaused) return;

            // Advance biological drives and emotion timers on fixed logical tick dt
            State.Tick(FixedTickDeltaTime);

            // Keep membership synced with authoritative main-thread group registry
            if (_simulationManager?.Groups != null && State.Membership.HasGroup)
            {
                if (_simulationManager.Groups.TryGetMembership(State.AgentId, out var currentMem))
                {
                    State.Membership = currentMem;
                }
                else
                {
                    State.Membership = GroupMembership.None;
                }
            }

            // Danger (Epic 10 G2): a hazard sector takes health; at 0 the crawler is downed. Getting up again is the state's
            bool wasDowned = State.IsDowned;
            var hazard = Vitality.HazardAt(_spatialPartition?.GetActiveSnapshot()?.Map, State.Position);
            if (hazard.IsHazardous)
            {
                State.Activity.HazardTicks++;
                State.Health = SimMath.Max(0f, (float)(State.Health - (float)(hazard.DamagePerSecond * FixedTickDeltaTime)));
                if (State.Health <= 0f && !State.IsDowned)
                {
                    State.IsDowned = true;
                    State.Activity.TimesDowned++;
                    SimLog.Drives.ZLogInformation($"{SimLog.Agent(State.AgentId)} was downed by {hazard.Kind} at {State.Position}");
                    RecordStory(ChronicleEventType.CrawlerDowned, ChronicleIntensity.Crisis);
                }
            }
            if (wasDowned && !State.IsDowned) RecordStory(ChronicleEventType.CrawlerRecovered, ChronicleIntensity.Notable);
            bool hurtInDanger = !State.IsDowned && hazard.IsHazardous && State.Health < Vitality.RetreatBelow;
            // Tending to itself; resting counts only out of danger (a downed crawler must not rest in a hazard sector)
            bool tending = State.CurrentIntent is RetreatIntent || State.CurrentIntent is TreatWoundsIntent || State.CurrentIntent is SleepAtBunkIntent
                || (State.CurrentIntent is IdleIntent && !hazard.IsHazardous);

            // Reflexive preemption check
            bool emergencyExhaustion = State.Energy <= MentalBreakEvaluator.ExhaustionThreshold;
            bool emergencyStarvation = State.Hunger >= MentalBreakEvaluator.StarvationThreshold && !State.HasFoodInInventory && !State.HasKnownFood();

            if (emergencyExhaustion)
            {
                State.IsExhaustionCollapsed = true;
                State.VisualState = AgentVisualState.Resting;
            }

            bool shouldPreempt = false;
            if (emergencyExhaustion && !(State.CurrentIntent is IdleIntent))
            {
                shouldPreempt = true;
            }
            else if (emergencyStarvation && !(State.CurrentIntent is PanicIntent) && !tending)
            {
                // Not while it tends to itself: a starving crawler retreating from a hazard sector was stopped every tick
                // and stayed in it all day (Epic 10 G2)
                shouldPreempt = true;
            }
            else if ((State.IsDowned || hurtInDanger) && !tending)
            {
                if (hurtInDanger) State.Activity.Retreats++;
                shouldPreempt = true;
            }

            if (State.HasActiveIntent && shouldPreempt)
            {
                if (_simulationManager != null)
                {
                    _simulationManager.AbortIntent(State.AgentId);
                }
                State.AbortActivePlan();
                State.CurrentIntent = null;
                State.HasActiveIntent = false;

                if (State.IsExhaustionCollapsed)
                {
                    State.VisualState = AgentVisualState.Resting;
                }
                else if (emergencyStarvation && State.TantrumTimer <= 0f)
                {
                    State.VisualState = AgentVisualState.Panicking;
                }

                if (!State.IsEvaluating)
                {
                    BeginEvaluation();
                }
                return;
            }

            if (!State.IsEvaluating && State.HasActiveIntent)
            {
                PerceiveWhileBusy();
                return;
            }
            if (State.IsEvaluating) return;

            BeginEvaluation();
        }

        private void BeginEvaluation()
        {
            _cognitionService?.BeginEvaluation(
                State,
                _spatialPartition,
                _simulationManager,
                _clock,
                _planner,
                _actionGraph,
                _goals,
                _evaluators,
                _cts.Token);
        }

        public bool HasKnownFood()
        {
            return State.HasKnownFood(_clock != null ? (uint)_clock.CurrentTick : 0);
        }

        /// <summary>A tantrum, recorded in the chronicle.</summary>
        public void TriggerTantrumVisual(float timestamp = 0f)
        {
            State.TriggerTantrumVisual(timestamp);
            RecordStory(ChronicleEventType.TantrumStarted, ChronicleIntensity.Dramatic);
        }

        /// <summary>An exhaustion collapse, recorded in the chronicle.</summary>
        public void CollapseFromExhaustion()
        {
            State.CollapseFromExhaustion();
            RecordStory(ChronicleEventType.ExhaustionCollapsed, ChronicleIntensity.Crisis);
        }

        private void RecordStory(ChronicleEventType type, ChronicleIntensity intensity)
        {
            uint tick = _clock != null ? (uint)_clock.CurrentTick : 0;
            _simulationManager?.RecordStoryEvent(new ChronicleEvent(type, State.AgentId, default, 0, tick, intensity));
        }

        /// <summary>
        /// Agents also see while they walk, not only between intents: on a walled floor a long walk would otherwise
        /// pass food, bunks and doors unseen (Epic MAP Phase 3). Main thread, never while a background evaluation
        /// is reading the memory; every BusyPerceptionIntervalTicks.
        /// </summary>
        private void PerceiveWhileBusy()
        {
            if (_spatialPartition == null || _clock == null || State.Memory == null) return;
            long tick = _clock.CurrentTick;
            // (a clock rewound by a respawn starts a new agent anyway; tick < last only after a rewind)
            if (tick >= _lastBusyPerceptionTick && tick - _lastBusyPerceptionTick < BusyPerceptionIntervalTicks) return;
            var snapshot = _spatialPartition.GetActiveSnapshot();
            if (snapshot == null) return;
            _lastBusyPerceptionTick = tick;
            State.Memory.UpdateMemory(snapshot, GridPoint.RoundToInt(State.Position), DefaultVisionRadius, (uint)tick,
                surveySectorId: BountyQueries.SurveyingSector(snapshot, State.AgentId));
        }

        /// <summary>Stops the crawler: cancels its decision in flight and leaves the agent registry.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
            _simulationManager?.Agents?.UnregisterAgent(State.AgentId);
        }
    }
}
