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
                // Register as the current active intent for this agent
                _activeIntents[intent.AgentId] = new ActiveIntentState { Intent = intent, CurrentWaypointIndex = 0 };
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
                // We iterate over a copy of the values, or carefully manage removal to avoid collection modified exceptions.
                var keysToRemove = new List<UnityEngine.GUID>();

                foreach (var kvp in _activeIntents)
                {
                    var agentId = kvp.Key;
                    var state = kvp.Value;

                    var node = _worldRegistry.GetNode(agentId);
                    if (node == null)
                    {
                        // Agent node no longer exists
                        keysToRemove.Add(agentId);
                        CompleteIntent(state.Intent, IntentResult.Aborted);
                        continue;
                    }

                    var result = ExecuteIntent(node, state);
                    if (result != IntentResult.InProgress)
                    {
                        keysToRemove.Add(agentId);
                        CompleteIntent(state.Intent, result);
                    }
                }

                foreach (var id in keysToRemove)
                {
                    _activeIntents.Remove(id);
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
                    return ExecutePathMovement(node, wander.Path, ref state.CurrentWaypointIndex);

                case PanicIntent panic:
                    if (agent != null) agent.VisualState = Simpiens.Entities.AgentVisualState.Panicking;
                    return ExecutePanicMovement(node, panic.Path, ref state.CurrentWaypointIndex);

                case HarvestResourceIntent harvest:
                    var reachResult = ExecutePathMovement(node, harvest.Path, ref state.CurrentWaypointIndex);
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

        private IntentResult ExecutePathMovement(Simpiens.Entities.NodeController node, PathResponse path, ref int currentIndex)
        {
            if (currentIndex >= path.Length)
            {
                return IntentResult.Success; // Reached end
            }

            Vector2 targetPos = path.Waypoints[currentIndex];

            // Path Invalidation: Check if next step is blocked
            if (IsPositionBlocked(targetPos, node.Id))
            {
                return IntentResult.PathBlocked; // Abort path, force recalculation
            }

            // Move the pawn visually and logically
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

        private bool IsPositionBlocked(Vector2 targetPos, UnityEngine.GUID ignoreAgentId)
        {
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
                        // Dynamic validation: if another pawn is currently standing exactly there.
                        if (Vector2.Distance(entity.Position, targetPos) < 0.4f) return true;
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
