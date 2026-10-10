using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
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
        private class AgentTelemetryHistory
        {
            public Vector2 LastPosition;
            public readonly Vector2[] HeadingBuffer = new Vector2[8];
            public readonly float[] HeadingTimestamps = new float[8];
            public int HeadingIndex;
            public int HeadingCount;

            public readonly float[] ReversalTimestamps = new float[4];
            public int ReversalIndex;
            public int ReversalCount;

            public AgentVisualState LastVisualState;
            public AgentView View;
            public bool ViewResolved;
            public readonly float[] StateTransitionTimestamps = new float[5];
            public readonly AgentVisualState[] StateTransitionStates = new AgentVisualState[5];
            public int StateTransitionIndex;
            public int StateTransitionCount;

            public float StackingDuration;
            public GUID StackingPartner;

            public float LastJitterAlertTime = -10f;
            public float LastStackingAlertTime = -10f;
            public float LastPenetrationAlertTime = -10f;
            public float LastThrashingAlertTime = -10f;
            public float LastBreachAlertTime = -10f;

            public void RecordHeading(Vector2 currentDir, float timestamp)
            {
                HeadingBuffer[HeadingIndex] = currentDir;
                HeadingTimestamps[HeadingIndex] = timestamp;
                HeadingIndex = (HeadingIndex + 1) % HeadingBuffer.Length;
                if (HeadingCount < HeadingBuffer.Length) HeadingCount++;
            }

            public void RecordReversal(float timestamp)
            {
                ReversalTimestamps[ReversalIndex] = timestamp;
                ReversalIndex = (ReversalIndex + 1) % ReversalTimestamps.Length;
                if (ReversalCount < ReversalTimestamps.Length) ReversalCount++;
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
            ISpatialPartition spatialPartition = null)
        {
            _worldRegistry = worldRegistry;
            _clock = clock;
            _spatialPartition = spatialPartition;

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

            // 1. Per-frame Evaluation: Jitter, Heading Changes, and Visual State Thrashing
            for (int i = 0; i < activeNodes.Count; i++)
            {
                var node = activeNodes[i];
                if (node == null || node.Type != EntityType.Pawn) continue;

                var history = GetOrCreateHistory(node.Id, node.Position);
                EvaluateJitter(node, history, currentTick, currentTime);
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

        private AgentTelemetryHistory GetOrCreateHistory(GUID id, Vector2 currentPos)
        {
            if (!_histories.TryGetValue(id, out var history))
            {
                history = new AgentTelemetryHistory
                {
                    LastPosition = currentPos,
                    LastVisualState = AgentVisualState.Idle
                };
                _histories[id] = history;
            }
            return history;
        }

        private void EvaluateJitter(NodeController node, AgentTelemetryHistory history, long tick, float currentTime)
        {
            Vector2 currentPos = node.Position;
            Vector2 delta = currentPos - history.LastPosition;
            float sqrDist = delta.sqrMagnitude;

            // Only track headings when actual translation occurred
            if (sqrDist > 0.0001f)
            {
                Vector2 currentHeading = delta.normalized;

                if (history.HeadingCount > 0)
                {
                    int prevIndex = (history.HeadingIndex - 1 + history.HeadingBuffer.Length) % history.HeadingBuffer.Length;
                    Vector2 prevHeading = history.HeadingBuffer[prevIndex];

                    // Angle > 120 degrees indicates directional reversal
                    float dot = Vector2.Dot(currentHeading, prevHeading);
                    if (dot < -0.5f)
                    {
                        history.RecordReversal(currentTime);

                        // Check if 3 reversals occurred in <= 0.5s window
                        if (history.ReversalCount >= 3)
                        {
                            int oldestIndex = (history.ReversalIndex - 3 + history.ReversalTimestamps.Length) % history.ReversalTimestamps.Length;
                            float windowDuration = currentTime - history.ReversalTimestamps[oldestIndex];

                            if (windowDuration <= 0.5f && (currentTime - history.LastJitterAlertTime >= 1.0f))
                            {
                                history.LastJitterAlertTime = currentTime;
                                JitterCount++;
                                EmitAnomaly(new VisualAnomalyRecord(
                                    tick,
                                    currentTime,
                                    VisualAnomalyType.OscillationJitter,
                                    node.Id,
                                    default,
                                    currentPos,
                                    $"Rapid directional reversal ({history.ReversalCount} flips in {windowDuration:F2}s, dot: {dot:F2})"
                                ));
                            }
                        }
                    }
                }

                history.RecordHeading(currentHeading, currentTime);
                history.LastPosition = currentPos;
            }
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

                // 1. Boundary breach check (+- 20 units)
                if (Mathf.Abs(pos.x) > NavConstants.PlayAreaHalfExtent || Mathf.Abs(pos.y) > NavConstants.PlayAreaHalfExtent)
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

                // 2. Obstacle penetration check via Physics2D non-allocating circle cast
                int hitCount = Physics2D.OverlapCircle(pos, node.Radius * 0.7f, _obstacleFilter, _overlapBuffer);
                for (int h = 0; h < hitCount; h++)
                {
                    var col = _overlapBuffer[h];
                    if (col != null && col.gameObject != node.gameObject && !col.isTrigger)
                    {
                        bool isWall = false;
                        var hitNode = col.GetComponent<NodeController>();
                        if (hitNode != null && hitNode.Type == EntityType.Wall)
                        {
                            isWall = true;
                        }
                        else if (col.gameObject.name.Contains("Wall") || col.gameObject.name.Contains("Bounds") || col.gameObject.name.Contains("Obstacle"))
                        {
                            isWall = true;
                        }

                        if (isWall)
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
                                    $"Agent penetrated obstacle geometry '{col.gameObject.name}'"
                                ));
                            }
                            break;
                        }
                    }
                }
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
