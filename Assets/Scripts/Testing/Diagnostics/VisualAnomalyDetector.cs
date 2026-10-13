using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Simpiens.Testing.Diagnostics
{
    /// <summary>
    /// High-performance, zero-allocation runtime observer that inspects active game views and physics states.
    /// Flags movement jitter, pawn stacking, collision penetration, and visual state flickering.
    /// </summary>
    public class VisualAnomalyDetector : IVisualAnomalyDetector, ITickable
    {
        /// <summary>Jitter is this many heading reversals of more than 120°...</summary>
        public const int JitterReversals = 3;

        /// <summary>...within this many simulation ticks (0.5 s at play speed), at any clock multiplier.</summary>
        public const int JitterWindowTicks = 5;

        /// <summary>At most one jitter alert per pawn per this many ticks (1 s at play speed).</summary>
        public const int JitterAlertCooldownTicks = 10;

        /// <summary>Logical positions kept per pawn for the jitter alert's description.</summary>
        public const int JitterTrailLength = 10;

        private class AgentTelemetryHistory
        {
            // Jitter, sampled once per simulation tick from logical positions
            public Vector2 LastTickPosition;
            public bool HasTickPosition;
            public Vector2 LastTickHeading;
            public bool HasTickHeading;
            public readonly long[] ReversalTicks = new long[4];
            public int ReversalIndex;
            public int ReversalCount;
            public long LastJitterAlertTick = long.MinValue / 2;
            public readonly Vector2[] Trail = new Vector2[JitterTrailLength];
            public int TrailIndex;
            public int TrailCount;

            public AgentVisualState LastVisualState;
            public AgentView View;
            public bool ViewResolved;
            public readonly float[] StateTransitionTimestamps = new float[5];
            public readonly AgentVisualState[] StateTransitionStates = new AgentVisualState[5];
            public int StateTransitionIndex;
            public int StateTransitionCount;

            public float StackingDuration;
            public GUID StackingPartner;

            public float LastStackingAlertTime = -10f;
            public float LastPenetrationAlertTime = -10f;
            public float LastThrashingAlertTime = -10f;
            public float LastBreachAlertTime = -10f;

            public void RecordReversal(long tick)
            {
                ReversalTicks[ReversalIndex] = tick;
                ReversalIndex = (ReversalIndex + 1) % ReversalTicks.Length;
                if (ReversalCount < ReversalTicks.Length) ReversalCount++;
            }

            public void RecordTrail(Vector2 position)
            {
                Trail[TrailIndex] = position;
                TrailIndex = (TrailIndex + 1) % Trail.Length;
                if (TrailCount < Trail.Length) TrailCount++;
            }

            public void RecordStateTransition(float timestamp, AgentVisualState newState)
            {
                StateTransitionStates[StateTransitionIndex] = newState;
                StateTransitionTimestamps[StateTransitionIndex] = timestamp;
                StateTransitionIndex = (StateTransitionIndex + 1) % StateTransitionTimestamps.Length;
                if (StateTransitionCount < StateTransitionTimestamps.Length) StateTransitionCount++;
            }
        }

        private readonly IWorldRegistry _worldRegistry;
        private readonly ISimulationClock _clock;
        private readonly ISpatialPartition _spatialPartition;
        private readonly IWorldMap _worldMap;

        private readonly Dictionary<GUID, AgentTelemetryHistory> _histories = new Dictionary<GUID, AgentTelemetryHistory>(128);
        private readonly List<VisualAnomalyRecord> _recentAnomalies = new List<VisualAnomalyRecord>(128);
        private readonly List<GUID> _cleanupBuffer = new List<GUID>(32);
        private readonly Collider2D[] _overlapBuffer = new Collider2D[16];

        private int _frameCounter;
        private ContactFilter2D _obstacleFilter;

        // Metric Counters
        public int TotalAnomalies { get; private set; }
        public int JitterCount { get; private set; }
        public int StackingCount { get; private set; }
        public int PenetrationCount { get; private set; }
        public int ThrashingCount { get; private set; }
        public int BoundaryBreachCount { get; private set; }

        public event Action<VisualAnomalyRecord> OnAnomalyDetected;

        [Inject]
        public VisualAnomalyDetector(
            IWorldRegistry worldRegistry = null,
            ISimulationClock clock = null,
            ISpatialPartition spatialPartition = null,
            IWorldMap worldMap = null,
            ISessionSwitcher sessionSwitcher = null)
        {
            _worldRegistry = worldRegistry;
            _clock = clock;
            _spatialPartition = spatialPartition;
            _worldMap = worldMap;

            // A respawn reissues every entity id at new positions: per-agent motion histories must not span it
            // (anomaly counts are kept; scenarios reset those themselves)
            if (sessionSwitcher != null) sessionSwitcher.SessionChanged += _ => _histories.Clear();

            // Jitter is judged per simulation tick, so a test at 10x sees what the player sees at 1x
            if (_clock != null) _clock.OnSimulationTick += EvaluateJitterTick;

            _obstacleFilter = ContactFilter2D.noFilter;
            _obstacleFilter.useTriggers = false;
        }

        public void ResetMetrics()
        {
            TotalAnomalies = 0;
            JitterCount = 0;
            StackingCount = 0;
            PenetrationCount = 0;
            ThrashingCount = 0;
            BoundaryBreachCount = 0;
            _recentAnomalies.Clear();
            _histories.Clear();
        }

        public IReadOnlyList<VisualAnomalyRecord> GetRecentAnomalies(int count)
        {
            if (count <= 0 || _recentAnomalies.Count == 0)
                return Array.Empty<VisualAnomalyRecord>();

            int returnCount = Mathf.Min(count, _recentAnomalies.Count);
            var result = new VisualAnomalyRecord[returnCount];
            int startIndex = _recentAnomalies.Count - returnCount;
            for (int i = 0; i < returnCount; i++)
            {
                result[i] = _recentAnomalies[startIndex + i];
            }
            return result;
        }

        public void Tick()
        {
            Tick(-1f, -1f);
        }

        public void Tick(float customDeltaTime, float customTime)
        {
            if (_worldRegistry == null) return;

            var activeNodes = _worldRegistry.ActiveNodes;
            if (activeNodes == null || activeNodes.Count == 0) return;

            _frameCounter++;
            float currentTime = customTime >= 0f ? customTime : Time.time;
            float dt = customDeltaTime >= 0f ? customDeltaTime : (Time.deltaTime > 0f ? Time.deltaTime : 0.02f);
            long currentTick = _clock != null ? _clock.CurrentTick : 0;

            // 1. Per-frame Evaluation: Visual State Thrashing (jitter is per simulation tick: EvaluateJitterTick)
            for (int i = 0; i < activeNodes.Count; i++)
            {
                var node = activeNodes[i];
                if (node == null || node.Type != EntityType.Pawn) continue;

                var history = GetOrCreateHistory(node.Id);
                EvaluateStateThrashing(node, history, currentTick, currentTime);
            }

            // 2. Throttled Evaluation (Every 5 frames): Spatial Overlap & Stacking
            if (_frameCounter % 5 == 0)
            {
                EvaluateStacking(activeNodes, currentTick, currentTime, dt);
            }

            // 3. Throttled Evaluation (Every 10 frames): Obstacle Penetration & Boundary Breaches
            if (_frameCounter % 10 == 0)
            {
                EvaluatePenetrationAndBounds(activeNodes, currentTick, currentTime);
                PruneInactiveHistories(activeNodes);
            }
        }

        private AgentTelemetryHistory GetOrCreateHistory(GUID id)
        {
            if (!_histories.TryGetValue(id, out var history))
            {
                history = new AgentTelemetryHistory { LastVisualState = AgentVisualState.Idle };
                _histories[id] = history;
            }
            return history;
        }

        /// <summary>
        /// Jitter, judged once per simulation tick from each pawn's logical position: <see cref="JitterReversals"/>
        /// heading reversals of more than 120° within <see cref="JitterWindowTicks"/> ticks. Subscribed to the clock;
        /// validators call it directly.
        /// </summary>
        public void EvaluateJitterTick(long tick)
        {
            if (_worldRegistry == null) return;
            var activeNodes = _worldRegistry.ActiveNodes;
            if (activeNodes == null) return;

            for (int i = 0; i < activeNodes.Count; i++)
            {
                var node = activeNodes[i];
                if (node == null || node.Type != EntityType.Pawn) continue;
                var history = GetOrCreateHistory(node.Id);

                Vector2 position = node.Position;
                history.RecordTrail(position);
                if (!history.HasTickPosition)
                {
                    history.LastTickPosition = position;
                    history.HasTickPosition = true;
                    continue;
                }

                Vector2 delta = position - history.LastTickPosition;
                if (delta.sqrMagnitude <= 0.0001f) continue; // headings only from real movement
                Vector2 heading = delta.normalized;
                history.LastTickPosition = position;

                if (history.HasTickHeading && Vector2.Dot(heading, history.LastTickHeading) < -0.5f)
                {
                    history.RecordReversal(tick);
                    if (history.ReversalCount >= JitterReversals)
                    {
                        int oldest = (history.ReversalIndex - JitterReversals + history.ReversalTicks.Length) % history.ReversalTicks.Length;
                        long window = tick - history.ReversalTicks[oldest];
                        if (window <= JitterWindowTicks && tick - history.LastJitterAlertTick >= JitterAlertCooldownTicks)
                        {
                            history.LastJitterAlertTick = tick;
                            JitterCount++;
                            float tickSeconds = _clock != null && _clock.TickDeltaTime > 0f ? _clock.TickDeltaTime : 0.1f;
                            EmitAnomaly(new VisualAnomalyRecord(
                                tick,
                                tick * tickSeconds,
                                VisualAnomalyType.OscillationJitter,
                                node.Id,
                                default,
                                position,
                                $"Rapid directional reversal ({JitterReversals} flips in {window} ticks); last positions {DescribeTrail(history)}"
                            ));
                        }
                    }
                }
                history.LastTickHeading = heading;
                history.HasTickHeading = true;
            }
        }

        /// <summary>The pawn's last logical positions, oldest first (built only when an anomaly fires).</summary>
        private static string DescribeTrail(AgentTelemetryHistory history)
        {
            var sb = new System.Text.StringBuilder();
            int len = history.Trail.Length;
            for (int k = history.TrailCount; k >= 1; k--)
            {
                var p = history.Trail[(history.TrailIndex - k + len) % len];
                if (sb.Length > 0) sb.Append(' ');
                sb.Append('(').Append(p.x.ToString("F2")).Append(',').Append(p.y.ToString("F2")).Append(')');
            }
            return sb.ToString();
        }

        private void EvaluateStateThrashing(NodeController node, AgentTelemetryHistory history, long tick, float currentTime)
        {
            var agent = node.Agent;
            if (agent == null) return;

            // Measure what the player sees: the view's displayed state (with its display dwell) when rendered,
            // otherwise the raw simulation state (headless)
            if (!history.ViewResolved)
            {
                history.View = node.GetComponent<AgentView>();
                history.ViewResolved = true;
            }
            var currentState = history.View != null ? history.View.DisplayedState : agent.VisualState;
            if (currentState != history.LastVisualState)
            {
                history.RecordStateTransition(currentTime, currentState);

                // Check if 4 state transitions occurred in <= 0.5s simulation window
                float maxWindow = 0.5f / (_clock != null && _clock.TimeMultiplier > 0f ? _clock.TimeMultiplier : 1f);
                if (history.StateTransitionCount >= 4)
                {
                    int oldestIndex = (history.StateTransitionIndex - 4 + history.StateTransitionTimestamps.Length) % history.StateTransitionTimestamps.Length;
                    float windowDuration = currentTime - history.StateTransitionTimestamps[oldestIndex];

                    if (windowDuration <= maxWindow && (currentTime - history.LastThrashingAlertTime >= 1.0f))
                    {
                        history.LastThrashingAlertTime = currentTime;
                        ThrashingCount++;
                        EmitAnomaly(new VisualAnomalyRecord(
                            tick,
                            currentTime,
                            VisualAnomalyType.VisualStateThrashing,
                            node.Id,
                            default,
                            node.Position,
                            $"Rapid visual state thrashing (4 switches in {windowDuration:F2}s, current: {currentState}; sequence: {DescribeTransitions(history)})"
                        ));
                    }
                }

                history.LastVisualState = currentState;
            }
        }

        /// <summary>The last four transitions, oldest first (diagnostic text, only built when an anomaly fires).</summary>
        private static string DescribeTransitions(AgentTelemetryHistory history)
        {
            int len = history.StateTransitionStates.Length;
            var sb = new System.Text.StringBuilder();
            for (int k = 4; k >= 1; k--)
            {
                int idx = (history.StateTransitionIndex - k + len) % len;
                if (sb.Length > 0) sb.Append(" > ");
                sb.Append(history.StateTransitionStates[idx]);
            }
            return sb.ToString();
        }

        private void EvaluateStacking(IReadOnlyList<NodeController> activeNodes, long tick, float currentTime, float dt)
        {
            float dtStep = dt * 5f;

            for (int i = 0; i < activeNodes.Count; i++)
            {
                var nodeA = activeNodes[i];
                if (nodeA == null || nodeA.Type != EntityType.Pawn) continue;

                if (!_histories.TryGetValue(nodeA.Id, out var historyA)) continue;

                bool isStackedWithAny = false;

                for (int j = i + 1; j < activeNodes.Count; j++)
                {
                    var nodeB = activeNodes[j];
                    if (nodeB == null || nodeB.Type != EntityType.Pawn) continue;

                    // Expose social interaction exemption: Gossiping & Sharing naturally happen close together
                    bool isSocialInteraction = (nodeA.Agent != null && (nodeA.Agent.IsGossiping || nodeA.Agent.IsSharing)) ||
                                              (nodeB.Agent != null && (nodeB.Agent.IsGossiping || nodeB.Agent.IsSharing));

                    if (isSocialInteraction) continue;

                    float dist = Vector2.Distance(nodeA.Position, nodeB.Position);
                    if (dist < 0.35f)
                    {
                        isStackedWithAny = true;
                        historyA.StackingDuration += dtStep;
                        historyA.StackingPartner = nodeB.Id;

                        if (historyA.StackingDuration >= 1.0f && (currentTime - historyA.LastStackingAlertTime >= 1.0f))
                        {
                            historyA.LastStackingAlertTime = currentTime;
                            StackingCount++;
                            EmitAnomaly(new VisualAnomalyRecord(
                                tick,
                                currentTime,
                                VisualAnomalyType.PawnStacking,
                                nodeA.Id,
                                nodeB.Id,
                                nodeA.Position,
                                $"Pawn stacking detected with Agent {nodeB.Id} (dist: {dist:F2}m for {historyA.StackingDuration:F1}s)"
                            ));
                        }
                        break;
                    }
                }

                if (!isStackedWithAny)
                {
                    historyA.StackingDuration = Mathf.Max(0f, historyA.StackingDuration - dtStep * 2f);
                }
            }
        }

        private void EvaluatePenetrationAndBounds(IReadOnlyList<NodeController> activeNodes, long tick, float currentTime)
        {
            for (int i = 0; i < activeNodes.Count; i++)
            {
                var node = activeNodes[i];
                if (node == null || node.Type != EntityType.Pawn) continue;

                Vector2 pos = node.Position;

                // 1. Boundary breach check: the loaded map's playable bounds (±20 before any map is loaded)
                if (IsOutsidePlayArea(pos))
                {
                    if (_histories.TryGetValue(node.Id, out var h) && (currentTime - h.LastBreachAlertTime >= 1.0f))
                    {
                        h.LastBreachAlertTime = currentTime;
                        BoundaryBreachCount++;
                        EmitAnomaly(new VisualAnomalyRecord(
                            tick,
                            currentTime,
                            VisualAnomalyType.BoundaryBreach,
                            node.Id,
                            default,
                            pos,
                            $"Agent breached play area boundary (pos: {pos})"
                        ));
                    }
                }

                // 2. Obstacle penetration: static map walls by exact geometry; injected dynamic walls (wall entities
                // with colliders) via a non-allocating physics overlap
                float probeRadius = node.Radius * 0.7f;
                var map = _worldMap?.Current;
                if (map != null && WallRasterizer.CircleOverlapsWall(map, pos, probeRadius))
                {
                    ReportPenetration(node, pos, tick, currentTime, "static map wall");
                    continue;
                }

                int hitCount = Physics2D.OverlapCircle(pos, probeRadius, _obstacleFilter, _overlapBuffer);
                for (int h = 0; h < hitCount; h++)
                {
                    var col = _overlapBuffer[h];
                    if (col == null || col.gameObject == node.gameObject || col.isTrigger) continue;

                    var hitNode = col.GetComponent<NodeController>();
                    if (hitNode != null && hitNode.Type == EntityType.Wall)
                    {
                        ReportPenetration(node, pos, tick, currentTime, col.gameObject.name);
                        break;
                    }
                }
            }
        }

        private bool IsOutsidePlayArea(Vector2 pos)
        {
            var grid = _spatialPartition?.StaticGrid;
            if (grid != null && grid.HasPlayableBounds)
            {
                var b = grid.PlayableBounds;
                return pos.x < b.xMin || pos.x > b.xMax || pos.y < b.yMin || pos.y > b.yMax;
            }
            var mapBounds = _worldMap?.Current;
            if (mapBounds != null)
            {
                var b = mapBounds.PlayableBounds;
                return pos.x < b.xMin || pos.x > b.xMax || pos.y < b.yMin || pos.y > b.yMax;
            }
            return Mathf.Abs(pos.x) > NavConstants.PlayAreaHalfExtent || Mathf.Abs(pos.y) > NavConstants.PlayAreaHalfExtent;
        }

        private void ReportPenetration(NodeController node, Vector2 pos, long tick, float currentTime, string obstacle)
        {
            if (_histories.TryGetValue(node.Id, out var hist) && (currentTime - hist.LastPenetrationAlertTime >= 1.0f))
            {
                hist.LastPenetrationAlertTime = currentTime;
                PenetrationCount++;
                EmitAnomaly(new VisualAnomalyRecord(
                    tick,
                    currentTime,
                    VisualAnomalyType.ObstaclePenetration,
                    node.Id,
                    default,
                    pos,
                    $"Agent penetrated obstacle geometry '{obstacle}'"
                ));
            }
        }

        private void PruneInactiveHistories(IReadOnlyList<NodeController> activeNodes)
        {
            _cleanupBuffer.Clear();

            foreach (var kvp in _histories)
            {
                bool found = false;
                for (int i = 0; i < activeNodes.Count; i++)
                {
                    if (activeNodes[i] != null && activeNodes[i].Type == EntityType.Pawn && activeNodes[i].Id == kvp.Key)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) _cleanupBuffer.Add(kvp.Key);
            }

            for (int i = 0; i < _cleanupBuffer.Count; i++)
            {
                _histories.Remove(_cleanupBuffer[i]);
            }
        }

        private void EmitAnomaly(VisualAnomalyRecord record)
        {
            TotalAnomalies++;
            if (_recentAnomalies.Count >= 128)
            {
                _recentAnomalies.RemoveAt(0);
            }
            _recentAnomalies.Add(record);

            Debug.LogWarning(record.ToString());
            OnAnomalyDetected?.Invoke(record);
        }
    }
}
