using System.Collections.Concurrent;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer.Unity;

namespace Simpiens.Simulation
{
    public class ActiveIntentState
    {
        public AgentIntent Intent;
        public int CurrentWaypointIndex;
        public float ElapsedTime;

        // Motor Watchdog Tracking
        public Vector2 LastSampledPosition;
        public float TimeSinceLastProgress;
        public int StallCount;
        public bool UseRelaxedClearance;
    }

    /// <summary>
    /// Handles the real-time Unity tick, keeping it decoupled from slow agent cognition.
    /// Implements ITickable from VContainer to run on Unity's Update loop without being a MonoBehaviour.
    /// </summary>
    public class SimulationManager : ISimulationManager, ITickable
    {
        private readonly IWorldRegistry _worldRegistry;
        private readonly ISpatialPartition _spatialPartition;
        private readonly ISimulationClock _clock;

        private readonly ConcurrentQueue<AgentIntent> _intentQueue = new ConcurrentQueue<AgentIntent>();
        
        // Main thread tracking stores
        private readonly Dictionary<UnityEngine.GUID, ResourceData> _resources = new Dictionary<UnityEngine.GUID, ResourceData>();
        private readonly Dictionary<UnityEngine.GUID, ActiveIntentState> _activeIntents = new Dictionary<UnityEngine.GUID, ActiveIntentState>();
        private readonly List<UnityEngine.GUID> _keysToRemove = new List<UnityEngine.GUID>(64);

        public bool IsPaused { get; private set; }

        public SimulationManager(IWorldRegistry worldRegistry, ISpatialPartition spatialPartition, ISimulationClock clock = null)
        {
            _worldRegistry = worldRegistry;
            _spatialPartition = spatialPartition;
            _clock = clock;
        }

        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;

        public void EnqueueIntent(AgentIntent intent)
        {
            _intentQueue.Enqueue(intent);
        }

        public void ProcessQueuedIntents()
        {
            while (_intentQueue.TryDequeue(out var intent))
            {
                Vector2 initialPos = Vector2.zero;
                if (_worldRegistry != null)
                {
                    var node = _worldRegistry.GetNode(intent.AgentId);
                    if (node != null) initialPos = node.Position;
                }

                // Register as the current active intent for this agent
                _activeIntents[intent.AgentId] = new ActiveIntentState
                {
                    Intent = intent,
                    CurrentWaypointIndex = 0,
                    LastSampledPosition = initialPos
                };
            }
        }

        public bool AbortIntent(UnityEngine.GUID agentId)
        {
            ProcessQueuedIntents();

            if (_activeIntents.TryGetValue(agentId, out var state))
            {
                _activeIntents.Remove(agentId);
                CompleteIntent(state.Intent, IntentResult.Aborted);
                return true;
            }
            return false;
        }

        public void RegisterResource(ResourceData data)
        {
            _resources[data.EntityId] = data;
        }

        public void Tick()
        {
            if (IsPaused) return;

            // Process queued intents from background cognitive threads
            ProcessQueuedIntents();

            // Execute active intents if registry is present
            if (_worldRegistry != null)
            {
                _keysToRemove.Clear();

                foreach (var kvp in _activeIntents)
                {
                    var agentId = kvp.Key;
                    var state = kvp.Value;

                    var node = _worldRegistry.GetNode(agentId);
                    if (node == null)
                    {
                        // Agent node no longer exists
                        _keysToRemove.Add(agentId);
                        CompleteIntent(state.Intent, IntentResult.Aborted);
                        continue;
                    }

                    var result = ExecuteIntent(node, state);
                    if (result != IntentResult.InProgress)
                    {
                        _keysToRemove.Add(agentId);
                        CompleteIntent(state.Intent, result);
                    }
                }

                int removeCount = _keysToRemove.Count;
                for (int i = 0; i < removeCount; i++)
                {
                    _activeIntents.Remove(_keysToRemove[i]);
                }
            }

            // Generate the thread-safe global snapshot for any cognitive agents that poll this frame
            if (_spatialPartition != null && _worldRegistry != null)
            {
                _spatialPartition.UpdateFromRegistry(_worldRegistry);
            }
        }

        private IntentResult ExecuteIntent(Simpiens.Entities.NodeController node, ActiveIntentState state)
        {
            var agent = node.Agent;

            switch (state.Intent)
            {
                case WanderIntent wander:
                    if (agent != null) agent.VisualState = Simpiens.Entities.AgentVisualState.Walking;
                    return ExecutePathMovement(node, wander.Path, state);

                case PanicIntent panic:
                    if (agent != null) agent.VisualState = Simpiens.Entities.AgentVisualState.Panicking;
                    return ExecutePanicMovement(node, panic.Path, ref state.CurrentWaypointIndex);

                case HarvestResourceIntent harvest:
                    var reachResult = ExecutePathMovement(node, harvest.Path, state);
                    if (reachResult == IntentResult.Success)
                    {
                        // Check if resource still exists before gathering
                        if (!_resources.TryGetValue(harvest.TargetEntityId, out var resourceData))
                        {
                            return IntentResult.TargetMissing;
                        }

                        if (agent != null) agent.VisualState = Simpiens.Entities.AgentVisualState.Harvesting;

                        state.ElapsedTime += Time.deltaTime;
                        if (state.ElapsedTime < harvest.GatherDuration)
                        {
                            return IntentResult.InProgress;
                        }

                        // State Mutation: Harvesting completed
                        resourceData.RemainingYield--;

                        // Replenish agent hunger
                        if (agent != null)
                        {
                            agent.Hunger = Mathf.Max(0f, agent.Hunger - 50f);
                        }

                        // Destroy resource if empty
                        if (resourceData.RemainingYield <= 0)
                        {
                            _resources.Remove(harvest.TargetEntityId);
                            var resourceNode = _worldRegistry.GetNode(harvest.TargetEntityId);
                            if (resourceNode != null)
                            {
                                _worldRegistry.UnregisterNode(resourceNode);
                                resourceNode.gameObject.SetActive(false);
                            }
                        }
                        return IntentResult.Success;
                    }
                    else if (reachResult == IntentResult.InProgress)
                    {
                        if (agent != null) agent.VisualState = Simpiens.Entities.AgentVisualState.Walking;
                    }
                    return reachResult;

                case IdleIntent idle:
                    state.ElapsedTime += Time.deltaTime;

                    // Social interaction: Gossip when idling near other agents
                    CheckGossipOpportunity(node);

                    if (agent != null && Time.time >= agent.LastGossipTimestamp + 1.5f)
                    {
                        if (agent.HasActivePlan && agent.ActivePlan?.CurrentAction is Simpiens.Cognition.Planning.Actions.HarvestResourceAction)
                        {
                            agent.VisualState = Simpiens.Entities.AgentVisualState.Harvesting;
                        }
                        else
                        {
                            agent.VisualState = Simpiens.Entities.AgentVisualState.Idle;
                        }
                    }

                    if (state.ElapsedTime >= idle.Duration)
                    {
                        return IntentResult.Success;
                    }
                    return IntentResult.InProgress;
            }

            return IntentResult.Success;
        }

        private const float GossipRadius = 2.5f;

        private void CheckGossipOpportunity(Simpiens.Entities.NodeController node)
        {
            var agentA = node.Agent;
            if (agentA == null || agentA.Memory == null) return;

            uint currentTick = _clock != null ? (uint)_clock.CurrentTick : 0;

            var activeNodes = _worldRegistry.ActiveNodes;
            int count = activeNodes.Count;
            for (int i = 0; i < count; i++)
            {
                var otherNode = activeNodes[i];
                if (otherNode == node || otherNode.Type != EntityType.Pawn) continue;

                float distSqr = (otherNode.Position - node.Position).sqrMagnitude;
                if (distSqr <= GossipRadius * GossipRadius)
                {
                    var agentB = otherNode.Agent;
                    if (agentB != null && agentB.Memory != null)
                    {
                        if (agentA.Memory.CanGossipWith(agentB.AgentId, currentTick))
                        {
                            if (agentA.Memory.TryGossip(agentB.Memory, agentB.AgentId, agentA.AgentId, currentTick))
                            {
                                agentA.RelieveFrustration(15f);
                                agentB.RelieveFrustration(15f);

                                // Fulfill biological social need
                                agentA.Social = Mathf.Min(100f, agentA.Social + 25f);
                                agentB.Social = Mathf.Min(100f, agentB.Social + 25f);

                                agentA.TriggerGossipVisual(Time.time);
                                agentB.TriggerGossipVisual(Time.time);

                                Debug.Log($"[Gossip] Agents {agentA.AgentId.ToString().Substring(0, 6)} and {agentB.AgentId.ToString().Substring(0, 6)} exchanged knowledge at tick {currentTick}!");
                                break;
                            }
                        }
                    }
                }
            }
        }

        private IntentResult ExecutePanicMovement(Simpiens.Entities.NodeController node, PathResponse path, ref int currentIndex)
        {
            if (currentIndex >= path.Length)
            {
                return IntentResult.Success; // Reached end
            }

            Vector2 targetPos = path.Waypoints[currentIndex];

            // Ignore IsPositionBlocked entirely to physically break free from any deadlock!
            float step = 2f * Time.deltaTime; // Ideally, fetch from node configuration
            node.transform.position = Vector2.MoveTowards(node.Position, targetPos, step);

            // Check if reached the current waypoint
            if (Vector2.Distance(node.Position, targetPos) < 0.05f)
            {
                currentIndex++;
                // If this was the last waypoint, return true indicating completion
                if (currentIndex >= path.Length)
                {
                    return IntentResult.Success;
                }
            }

            return IntentResult.InProgress;
        }

        internal IntentResult ExecutePathMovement(Simpiens.Entities.NodeController node, PathResponse path, ActiveIntentState state)
        {
            if (state.CurrentWaypointIndex >= path.Length)
            {
                return IntentResult.Success; // Reached end
            }

            Vector2 targetPos = path.Waypoints[state.CurrentWaypointIndex];

            // 1. Motor Watchdog Progress Tracking
            float distMoved = Vector2.Distance(node.Position, state.LastSampledPosition);
            if (distMoved >= 0.05f)
            {
                state.LastSampledPosition = node.Position;
                state.TimeSinceLastProgress = 0f;
                state.StallCount = 0;
                state.UseRelaxedClearance = false;
            }
            else
            {
                state.TimeSinceLastProgress += Time.deltaTime;
                if (state.TimeSinceLastProgress >= 1.0f)
                {
                    state.StallCount++;
                    state.TimeSinceLastProgress = 0f;

                    var reflexResult = ApplyProgressiveUnstuckReflex(node, targetPos, state);
                    if (reflexResult != IntentResult.InProgress)
                    {
                        return reflexResult;
                    }
                }
            }

            // 2. Path Invalidation: Check if next step is blocked
            float clearance = state.UseRelaxedClearance ? 0.2f : 0.4f;
            if (IsPositionBlocked(targetPos, node.Id, clearance))
            {
                state.StallCount++;
                var reflexResult = ApplyProgressiveUnstuckReflex(node, targetPos, state);
                if (reflexResult != IntentResult.InProgress)
                {
                    return reflexResult; // Abort path, force recalculation
                }
            }

            // 3. Move the pawn visually and logically
            float step = 2f * Time.deltaTime; // Ideally, fetch from node configuration
            node.transform.position = Vector2.MoveTowards(node.Position, targetPos, step);

            // Check if reached the current waypoint
            if (Vector2.Distance(node.Position, targetPos) < 0.05f)
            {
                state.CurrentWaypointIndex++;
                state.LastSampledPosition = node.Position;
                state.TimeSinceLastProgress = 0f;
                state.StallCount = 0;
                state.UseRelaxedClearance = false;

                // If this was the last waypoint, return true indicating completion
                if (state.CurrentWaypointIndex >= path.Length)
                {
                    return IntentResult.Success;
                }
            }

            return IntentResult.InProgress;
        }

        internal IntentResult ApplyProgressiveUnstuckReflex(Simpiens.Entities.NodeController node, Vector2 targetPos, ActiveIntentState state)
        {
            Vector2 toTarget = targetPos - (Vector2)node.Position;
            float dist = toTarget.magnitude;
            Vector2 dir = dist > 0.001f ? toTarget / dist : Vector2.up;

            if (state.StallCount <= 2)
            {
                // Tier 1: Local Clearance Nudge (Orthogonal shift to break symmetrical collider face-offs)
                float sign = (state.StallCount == 1) ? 1.0f : -1.0f;
                Vector2 perpendicular = new Vector2(-dir.y, dir.x) * (0.2f * sign);
                node.transform.position = (Vector2)node.transform.position + perpendicular;
                state.LastSampledPosition = node.Position;
                return IntentResult.InProgress;
            }
            else if (state.StallCount == 3)
            {
                // Tier 2: Relaxed Clearance Repath
                state.UseRelaxedClearance = true;
                return IntentResult.InProgress;
            }
            else
            {
                // Tier 3: Target Disengage & Blacklist Back-Off (StallCount >= 4)
                node.transform.position = (Vector2)node.transform.position - (dir * 0.5f);

                if (state.Intent is HarvestResourceIntent harvest)
                {
                    if (node.Agent != null && node.Agent.Memory != null)
                    {
                        uint currentTick = _clock != null ? (uint)_clock.CurrentTick : 0;
                        node.Agent.Memory.BlacklistEntity(harvest.TargetEntityId, currentTick + 300);
                    }
                }

                return IntentResult.PathBlocked;
            }
        }

        internal bool IsPositionBlocked(Vector2 targetPos, UnityEngine.GUID ignoreAgentId, float pawnClearanceRadius = 0.4f)
        {
            if (_spatialPartition == null) return false;
            var snapshot = _spatialPartition.GetActiveSnapshot();
            if (snapshot == null) return false;

            int cellX = Mathf.FloorToInt((targetPos.x + snapshot.Width * snapshot.CellSize * 0.5f) / snapshot.CellSize);
            int cellY = Mathf.FloorToInt((targetPos.y + snapshot.Height * snapshot.CellSize * 0.5f) / snapshot.CellSize);

            if (cellX < 0 || cellX >= snapshot.Width || cellY < 0 || cellY >= snapshot.Height) return true; // Out of bounds

            int index = cellY * snapshot.Width + cellX;
            int count = snapshot.CellCounts[index];
            var cellArray = snapshot.Grid[index];

            for (int i = 0; i < count; i++)
            {
                var entity = cellArray[i];
                if (entity.Id != ignoreAgentId)
                {
                    if (entity.Type == EntityType.Wall) return true;
                    if (entity.Type == EntityType.Pawn)
                    {
                        // Dynamic validation: if another pawn is currently standing within clearance radius
                        if (Vector2.Distance(entity.Position, targetPos) < pawnClearanceRadius) return true;
                    }
                }
            }
            return false;
        }

        private void CompleteIntent(AgentIntent intent, IntentResult result)
        {
            if (intent is HarvestResourceIntent hri)
            {
                hri.Path.ReturnToPool();
            }
            else if (intent is WanderIntent wi)
            {
                wi.Path.ReturnToPool();
            }
            else if (intent is PanicIntent pi)
            {
                pi.Path.ReturnToPool();
            }

            intent.OnComplete?.Invoke(result);
        }
    }
}
