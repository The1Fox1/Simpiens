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
    }

    /// <summary>
    /// Handles the real-time Unity tick, keeping it decoupled from slow agent cognition.
    /// Implements ITickable from VContainer to run on Unity's Update loop without being a MonoBehaviour.
    /// </summary>
    public class SimulationManager : ISimulationManager, ITickable
    {
        private readonly IWorldRegistry _worldRegistry;
        private readonly ISpatialPartition _spatialPartition;

        private readonly ConcurrentQueue<AgentIntent> _intentQueue = new ConcurrentQueue<AgentIntent>();
        
        // Main thread tracking stores
        private readonly Dictionary<UnityEngine.GUID, ResourceData> _resources = new Dictionary<UnityEngine.GUID, ResourceData>();
        private readonly Dictionary<UnityEngine.GUID, ActiveIntentState> _activeIntents = new Dictionary<UnityEngine.GUID, ActiveIntentState>();

        public bool IsPaused { get; private set; }

        public SimulationManager(IWorldRegistry worldRegistry, ISpatialPartition spatialPartition)
        {
            _worldRegistry = worldRegistry;
            _spatialPartition = spatialPartition;
        }

        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;

        public void EnqueueIntent(AgentIntent intent)
        {
            _intentQueue.Enqueue(intent);
        }

        public void RegisterResource(ResourceData data)
        {
            _resources[data.EntityId] = data;
        }

        public void Tick()
        {
            if (IsPaused) return;

            // Process queued intents from background cognitive threads
            while (_intentQueue.TryDequeue(out var intent))
            {
                // Register as the current active intent for this agent
                _activeIntents[intent.AgentId] = new ActiveIntentState { Intent = intent, CurrentWaypointIndex = 0 };
            }

            // Execute active intents
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
                    CompleteIntent(state.Intent);
                    continue;
                }

                bool isComplete = ExecuteIntent(node, state);
                if (isComplete)
                {
                    keysToRemove.Add(agentId);
                    CompleteIntent(state.Intent);
                }
            }

            foreach (var id in keysToRemove)
            {
                _activeIntents.Remove(id);
            }

            // Generate the thread-safe global snapshot for any cognitive agents that poll this frame
            _spatialPartition.UpdateFromRegistry(_worldRegistry);
        }

        private bool ExecuteIntent(Simpiens.Entities.NodeController node, ActiveIntentState state)
        {
            switch (state.Intent)
            {
                case WanderIntent wander:
                    return ExecutePathMovement(node, wander.Path, ref state.CurrentWaypointIndex);

                case HarvestResourceIntent harvest:
                    bool reached = ExecutePathMovement(node, harvest.Path, ref state.CurrentWaypointIndex);
                    if (reached)
                    {
                        // State Mutation: Harvesting
                        if (_resources.TryGetValue(harvest.TargetEntityId, out var resourceData))
                        {
                            resourceData.RemainingYield--;

                            // Increase agent hunger
                            var agent = node.GetComponent<Simpiens.Cognition.AutonomousAgent>();
                            if (agent != null)
                            {
                                agent.Hunger = Mathf.Min(100f, agent.Hunger + 50f);
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
                        }
                        return true;
                    }
                    return false;

                case IdleIntent idle:
                    // Agent is explicitly doing nothing, complete immediately so they can re-evaluate later.
                    return true;
            }

            return true;
        }

        private bool ExecutePathMovement(Simpiens.Entities.NodeController node, PathResponse path, ref int currentIndex)
        {
            if (currentIndex >= path.Length)
            {
                return true; // Reached end
            }

            Vector2 targetPos = path.Waypoints[currentIndex];

            // Path Invalidation: Check if next step is blocked
            if (IsPositionBlocked(targetPos, node.Id))
            {
                return true; // Abort path, force recalculation
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
                    return true;
                }
            }

            return false;
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

        private void CompleteIntent(AgentIntent intent)
        {
            if (intent is HarvestResourceIntent hri)
            {
                hri.Path.ReturnToPool();
            }
            else if (intent is WanderIntent wi)
            {
                wi.Path.ReturnToPool();
            }

            intent.OnComplete?.Invoke();
        }
    }
}
