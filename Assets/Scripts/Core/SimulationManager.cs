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

        // Contention Tracking
        public UnityEngine.GUID ContestedCompetitorId;
    }

    public readonly struct AnchorData
    {
        public readonly UnityEngine.GUID EntityId;
        public readonly UnityEngine.Vector2 Position;
        public readonly float Radius;

        public AnchorData(UnityEngine.GUID entityId, UnityEngine.Vector2 position, float radius = 3.0f)
        {
            EntityId = entityId;
            Position = position;
            Radius = radius;
        }
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
        private readonly Dictionary<UnityEngine.GUID, AnchorData> _anchors = new Dictionary<UnityEngine.GUID, AnchorData>();
        private readonly Dictionary<UnityEngine.GUID, ActiveIntentState> _activeIntents = new Dictionary<UnityEngine.GUID, ActiveIntentState>();
        private readonly List<UnityEngine.GUID> _keysToRemove = new List<UnityEngine.GUID>(64);
        private readonly List<Simpiens.Entities.NodeController> _hearthPawnsBuffer = new List<Simpiens.Entities.NodeController>(32);
        private readonly List<AnchorData> _activeAnchorsBuffer = new List<AnchorData>(16);

        public bool IsPaused { get; private set; }
        public IWorldRegistry WorldRegistry => _worldRegistry;
        internal Dictionary<UnityEngine.GUID, ActiveIntentState> ActiveIntents => _activeIntents;

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

        public void RegisterAnchor(UnityEngine.GUID id, Vector2 position, float radius = 3.0f)
        {
            _anchors[id] = new AnchorData(id, position, radius);
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

            // Evaluate Hearth Dynamics & Proximity Bonding every 50 ticks
            uint currentTick = _clock != null ? (uint)_clock.CurrentTick : 0;
            if (currentTick % 50 == 0)
            {
                EvaluateHearthProximity(currentTick);
            }

            // Generate the thread-safe global snapshot for any cognitive agents that poll this frame
            if (_spatialPartition != null && _worldRegistry != null)
            {
                _spatialPartition.UpdateFromRegistry(_worldRegistry);
            }
        }

        internal IntentResult ExecuteIntent(Simpiens.Entities.NodeController node, ActiveIntentState state)
        {
            var agent = node.Agent;

            switch (state.Intent)
            {
                case WanderIntent wander:
                    if (agent != null) agent.VisualState = Simpiens.Entities.AgentVisualState.Walking;
                    return ExecutePathMovement(node, wander.Path, state);

                case PanicIntent panic:
                    if (agent != null) agent.VisualState = Simpiens.Entities.AgentVisualState.Panicking;
                    state.ElapsedTime += Time.deltaTime;
                    var panicResult = ExecutePanicMovement(node, panic.Path, ref state.CurrentWaypointIndex);
                    if (panicResult == IntentResult.Success)
                    {
                        // Enforce a minimum duration of at least 1.0 second for panic behavior to prevent sub-second visual jitter
                        if (state.ElapsedTime < 1.0f)
                        {
                            return IntentResult.InProgress;
                        }
                        return IntentResult.Success;
                    }
                    return panicResult;

                case HarvestResourceIntent harvest:
                    uint currentTick = _clock != null ? (uint)_clock.CurrentTick : 0;

                    // 1. Check if resource is already depleted
                    bool isDepleted = !_resources.TryGetValue(harvest.TargetEntityId, out var resourceData) || resourceData.RemainingYield <= 0;
                    if (isDepleted)
                    {
                        // An agent only suffers a contention defeat if:
                        // 1. A head-to-head rivalry was already established (state.ContestedCompetitorId != default), OR
                        // 2. The agent had already reached the bush and was actively harvesting when it depleted (state.ElapsedTime > 0f)
                        if (state.ContestedCompetitorId == default && state.ElapsedTime > 0f && _worldRegistry != null)
                        {
                            var activeNodes = _worldRegistry.ActiveNodes;
                            for (int i = 0; i < activeNodes.Count; i++)
                            {
                                var other = activeNodes[i];
                                if (other.Id != node.Id && other.Type == EntityType.Pawn && Vector2.Distance(other.Position, node.Position) <= 2.5f)
                                {
                                    state.ContestedCompetitorId = other.Id;
                                    break;
                                }
                            }
                        }

                        // If resource was depleted while actively contesting:
                        if (state.ContestedCompetitorId != default && agent != null)
                        {
                            // Symmetrical Rivalry Defeat: Tantrum, frustration, and negative relational hit
                            agent.Frustration = Mathf.Min(100f, agent.Frustration + 25f);
                            agent.VisualState = Simpiens.Entities.AgentVisualState.Panicking;
                            agent.TriggerTantrumVisual();
                            agent.Memory?.BlacklistEntity(harvest.TargetEntityId, currentTick + 150);

                            agent.Memory?.EventLedger?.AddEvent(new Simpiens.Cognition.Memory.MemoryEvent(
                                Simpiens.Cognition.Memory.MemoryEventType.ContentionFaced,
                                state.ContestedCompetitorId,
                                Vector2Int.RoundToInt(node.Position),
                                currentTick));

                            agent.Memory?.RecordInteraction(state.ContestedCompetitorId, deltaWarmth: -15, deltaTrust: -10, deltaFear: 5, deltaRespect: -10, currentTick);

                            string compName = _worldRegistry?.GetNode(state.ContestedCompetitorId)?.name ?? state.ContestedCompetitorId.ToString().Substring(0, 8);
                            Debug.Log($"<color=#ef4444><b>[Contention Defeat]</b> Agent '{node.name}' lost harvest to competitor '{compName}'! +25 Frustration. Logged ContentionFaced. Tantrum triggered!</color>");
                        }
                        return IntentResult.TargetMissing;
                    }

                    var targetResourceNode = _worldRegistry?.GetNode(harvest.TargetEntityId);
                    bool withinHarvestReach = state.ElapsedTime > 0f;
                    if (!withinHarvestReach && targetResourceNode != null)
                    {
                        float distToResource = Vector2.Distance(node.Position, targetResourceNode.Position);
                        if (distToResource <= 0.8f)
                        {
                            withinHarvestReach = true;
                        }
                    }

                    var reachResult = withinHarvestReach ? IntentResult.Success : ExecutePathMovement(node, harvest.Path, state);
                    if (reachResult == IntentResult.Success)
                    {
                        // Ensure the agent is genuinely within physical harvest proximity of the target resource
                        if (targetResourceNode != null && Vector2.Distance(node.Position, targetResourceNode.Position) > 1.2f)
                        {
                            return IntentResult.PathBlocked;
                        }

                        // Social Yielding Protocol: Check if any other pawn is also targeting/harvesting this node
                        Simpiens.Entities.NodeController competitorNode = null;
                        if (_worldRegistry != null)
                        {
                            foreach (var kvp in _activeIntents)
                            {
                                if (kvp.Key != node.Id && kvp.Value.Intent is HarvestResourceIntent otherHarvest)
                                {
                                    if (otherHarvest.TargetEntityId == harvest.TargetEntityId)
                                    {
                                        var otherNode = _worldRegistry.GetNode(kvp.Key);
                                        if (otherNode != null && Vector2.Distance(otherNode.Position, node.Position) <= 2.5f)
                                        {
                                            competitorNode = otherNode;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        if (competitorNode != null)
                        {
                            float subA = agent != null && agent.Memory != null ? agent.Memory.CalculateSubmissionScore(competitorNode.Id, currentTick) : 0f;
                            float subB = competitorNode.Agent != null && competitorNode.Agent.Memory != null ? competitorNode.Agent.Memory.CalculateSubmissionScore(node.Id, currentTick) : 0f;

                            // Asymmetrical Dominance: If this pawn submits to competitor
                            if (subA >= 30f && (subA - subB) >= 15f)
                            {
                                // This pawn yields to dominant competitor
                                agent?.TriggerYieldVisual(Time.time);

                                // Step back 0.5 units away from competitor
                                Vector2 awayDir = ((Vector2)node.Position - (Vector2)competitorNode.Position).normalized;
                                if (awayDir == Vector2.zero) awayDir = Vector2.down;
                                node.transform.position = (Vector2)node.transform.position + awayDir * 0.5f;

                                // Blacklist node for 150 ticks
                                agent?.Memory?.BlacklistEntity(harvest.TargetEntityId, currentTick + 150);

                                // Log memory events
                                agent?.Memory?.EventLedger?.AddEvent(new Simpiens.Cognition.Memory.MemoryEvent(
                                    Simpiens.Cognition.Memory.MemoryEventType.ResourceYielded,
                                    competitorNode.Id,
                                    Vector2Int.RoundToInt(node.Position),
                                    currentTick));
                                competitorNode.Agent?.Memory?.EventLedger?.AddEvent(new Simpiens.Cognition.Memory.MemoryEvent(
                                    Simpiens.Cognition.Memory.MemoryEventType.ResourceClaimed,
                                    node.Id,
                                    Vector2Int.RoundToInt(competitorNode.Position),
                                    currentTick));

                                Debug.Log($"<color=#f59e0b><b>[Social Yielding]</b> Agent '{node.name}' yielded to dominant peer '{competitorNode.name}'. Stepped back 0.5m.</color>");

                                return IntentResult.PathBlocked;
                            }

                            // Otherwise, Symmetrical Rivalry: mark contested competitor on both pawns
                            state.ContestedCompetitorId = competitorNode.Id;
                            if (_activeIntents.TryGetValue(competitorNode.Id, out var compState))
                            {
                                compState.ContestedCompetitorId = node.Id;
                            }
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

                        // If this was a contested victory, log ContentionFaced and dyadic hit
                        if (state.ContestedCompetitorId != default)
                        {
                            agent?.Memory?.EventLedger?.AddEvent(new Simpiens.Cognition.Memory.MemoryEvent(
                                Simpiens.Cognition.Memory.MemoryEventType.ContentionFaced,
                                state.ContestedCompetitorId,
                                Vector2Int.RoundToInt(node.Position),
                                currentTick));
                            agent?.Memory?.RecordInteraction(state.ContestedCompetitorId, deltaWarmth: -15, deltaTrust: -10, deltaFear: 0, deltaRespect: -10, currentTick);

                            string compName = _worldRegistry?.GetNode(state.ContestedCompetitorId)?.name ?? state.ContestedCompetitorId.ToString().Substring(0, 8);
                            Debug.Log($"<color=#10b981><b>[Contention Victory]</b> Agent '{node.name}' won harvest against '{compName}'. Logged ContentionFaced.</color>");
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

                case ShareResourceIntent share:
                    uint aidTick = _clock != null ? (uint)_clock.CurrentTick : 0;
                    var targetNode = _worldRegistry?.GetNode(share.TargetPawnId);
                    if (targetNode == null)
                    {
                        return IntentResult.TargetMissing;
                    }

                    bool withinAidReach = state.ElapsedTime > 0f;
                    if (!withinAidReach)
                    {
                        float distToTarget = Vector2.Distance(node.Position, targetNode.Position);
                        if (distToTarget <= 1.5f)
                        {
                            withinAidReach = true;
                        }
                    }

                    var aidReachResult = withinAidReach ? IntentResult.Success : ExecutePathMovement(node, share.Path, state);
                    if (aidReachResult == IntentResult.Success)
                    {
                        if (agent != null)
                        {
                            agent.VisualState = Simpiens.Entities.AgentVisualState.Sharing;
                            agent.TriggerSharingVisual();
                        }

                        state.ElapsedTime += Time.deltaTime;
                        if (state.ElapsedTime < share.TransferDuration)
                        {
                            return IntentResult.InProgress;
                        }

                        // State Mutation: Food transfer
                        if (agent != null)
                        {
                            agent.Hunger = Mathf.Min(100f, agent.Hunger + 15f);
                            agent.HasFoodInInventory = false;
                        }

                        var receiverAgent = targetNode.Agent;
                        if (receiverAgent != null)
                        {
                            receiverAgent.Hunger = Mathf.Max(0f, receiverAgent.Hunger - share.FoodAmount);
                            if (receiverAgent.Hunger < 85f)
                            {
                                receiverAgent.TriggerReliefVisual();
                            }
                        }

                        // Giver logs AidGiven (+10 Warmth, +5 Respect)
                        agent?.Memory?.EventLedger?.AddEvent(new Simpiens.Cognition.Memory.MemoryEvent(
                            Simpiens.Cognition.Memory.MemoryEventType.AidGiven,
                            share.TargetPawnId,
                            Vector2Int.RoundToInt(node.Position),
                            aidTick));
                        agent?.Memory?.RecordInteraction(share.TargetPawnId, deltaWarmth: 10, deltaTrust: 5, deltaFear: 0, deltaRespect: 5, aidTick);

                        // Receiver logs AidReceived (+25 Warmth, +30 Trust, +15 Respect)
                        receiverAgent?.Memory?.EventLedger?.AddEvent(new Simpiens.Cognition.Memory.MemoryEvent(
                            Simpiens.Cognition.Memory.MemoryEventType.AidReceived,
                            node.Id,
                            Vector2Int.RoundToInt(targetNode.Position),
                            aidTick));
                        receiverAgent?.Memory?.RecordInteraction(node.Id, deltaWarmth: 25, deltaTrust: 30, deltaFear: -10, deltaRespect: 15, aidTick);

                        Debug.Log($"<color=#10b981><b>[Altruistic Aid]</b> Agent '{node.name}' shared {share.FoodAmount:F0} food with '{targetNode.name}'! (Giver Hunger: {agent?.Hunger:F0}, Receiver Hunger: {receiverAgent?.Hunger:F0})</color>");

                        return IntentResult.Success;
                    }
                    else if (aidReachResult == IntentResult.InProgress)
                    {
                        if (agent != null) agent.VisualState = Simpiens.Entities.AgentVisualState.Walking;
                    }
                    return aidReachResult;

                case IdleIntent idle:
                    state.ElapsedTime += Time.deltaTime;

                    // Social interaction: Gossip when idling near other agents
                    if (agent == null || !agent.IsExhaustionCollapsed)
                    {
                        CheckGossipOpportunity(node);
                    }

                    if (agent != null && !agent.IsGossiping)
                    {
                        if (agent.IsExhaustionCollapsed || (agent.HasActivePlan && agent.ActivePlan?.CurrentAction is Simpiens.Cognition.Planning.Actions.RestAction))
                        {
                            agent.VisualState = Simpiens.Entities.AgentVisualState.Resting;
                        }
                        else if (agent.HasActivePlan && agent.ActivePlan?.CurrentAction is Simpiens.Cognition.Planning.Actions.HarvestResourceAction)
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
            if (agentA == null || agentA.Memory == null || agentA.IsExhaustionCollapsed) return;
            if (_worldRegistry == null) return;

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
                    if (agentB != null && agentB.Memory != null && !agentB.IsExhaustionCollapsed)
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

        internal void EvaluateHearthProximity(uint currentTick)
        {
            if (_worldRegistry == null) return;

            // Collect all anchors (from registered anchors and active world nodes)
            _activeAnchorsBuffer.Clear();
            foreach (var kvp in _anchors)
            {
                _activeAnchorsBuffer.Add(kvp.Value);
            }

            var activeNodes = _worldRegistry.ActiveNodes;
            int totalNodes = activeNodes.Count;

            for (int i = 0; i < totalNodes; i++)
            {
                var n = activeNodes[i];
                if (n.Type == EntityType.Anchor && !_anchors.ContainsKey(n.Id))
                {
                    _activeAnchorsBuffer.Add(new AnchorData(n.Id, n.Position, 3.0f));
                }
            }

            int anchorCount = _activeAnchorsBuffer.Count;
            if (anchorCount == 0) return;

            for (int a = 0; a < anchorCount; a++)
            {
                var anchor = _activeAnchorsBuffer[a];
                _hearthPawnsBuffer.Clear();

                for (int n = 0; n < totalNodes; n++)
                {
                    var node = activeNodes[n];
                    if (node.Type == EntityType.Pawn && node.Agent != null)
                    {
                        if (Vector2.Distance(node.Position, anchor.Position) <= anchor.Radius)
                        {
                            _hearthPawnsBuffer.Add(node);
                        }
                    }
                }

                int pawnCount = _hearthPawnsBuffer.Count;
                if (pawnCount == 0) continue;

                // 1. Environmental Comfort: Quench frustration and satisfy social drive
                for (int p = 0; p < pawnCount; p++)
                {
                    var agent = _hearthPawnsBuffer[p].Agent;
                    // Double frustration decay while relaxing near the fire (relieve 1.0 extra per 50 ticks)
                    agent.RelieveFrustration(1.0f);

                    // When 2 or more pawns are gathered, replenish social drive (+1.0/sec => ~1.0 per 50 ticks)
                    if (pawnCount >= 2)
                    {
                        agent.Social = Mathf.Min(100f, agent.Social + 1.0f);
                    }
                }

                // 2. Accelerated Gossip & Passive Propinquity Bonding between pairs
                for (int i = 0; i < pawnCount; i++)
                {
                    var nodeA = _hearthPawnsBuffer[i];
                    var agentA = nodeA.Agent;
                    if (agentA.Memory == null || agentA.IsExhaustionCollapsed) continue;

                    for (int j = i + 1; j < pawnCount; j++)
                    {
                        var nodeB = _hearthPawnsBuffer[j];
                        var agentB = nodeB.Agent;
                        if (agentB.Memory == null || agentB.IsExhaustionCollapsed) continue;

                        // A. Accelerated Gossip: cooldown 100 ticks instead of 300
                        if (agentA.Memory.CanGossipWith(agentB.AgentId, currentTick, cooldownTicks: 100))
                        {
                            if (agentA.Memory.TryGossip(agentB.Memory, agentB.AgentId, agentA.AgentId, currentTick, cooldownTicks: 100))
                            {
                                agentA.RelieveFrustration(15f);
                                agentB.RelieveFrustration(15f);
                                agentA.Social = Mathf.Min(100f, agentA.Social + 25f);
                                agentB.Social = Mathf.Min(100f, agentB.Social + 25f);
                                agentA.TriggerGossipVisual(Time.time);
                                agentB.TriggerGossipVisual(Time.time);
                                Debug.Log($"[Campfire Gossip] Agents {agentA.AgentId.ToString().Substring(0, 6)} and {agentB.AgentId.ToString().Substring(0, 6)} gossiped around hearth at tick {currentTick}!");
                            }
                        }

                        // B. Passive Hearth Bonding: Every 100 ticks co-present around hearth
                        if (currentTick % 100 == 0)
                        {
                            agentA.Memory.RecordInteraction(agentB.AgentId, deltaWarmth: 2, deltaTrust: 1, deltaFear: -2, deltaRespect: 0, currentTick);
                            agentB.Memory.RecordInteraction(agentA.AgentId, deltaWarmth: 2, deltaTrust: 1, deltaFear: -2, deltaRespect: 0, currentTick);

                            agentA.Memory.EventLedger?.AddEvent(new Simpiens.Cognition.Memory.MemoryEvent(
                                Simpiens.Cognition.Memory.MemoryEventType.CampfireCompanionship,
                                agentB.AgentId,
                                Vector2Int.RoundToInt(nodeA.Position),
                                currentTick));

                            agentB.Memory.EventLedger?.AddEvent(new Simpiens.Cognition.Memory.MemoryEvent(
                                Simpiens.Cognition.Memory.MemoryEventType.CampfireCompanionship,
                                agentA.AgentId,
                                Vector2Int.RoundToInt(nodeB.Position),
                                currentTick));
                        }
                    }
                }
            }
        }

        private IntentResult ExecutePanicMovement(Simpiens.Entities.NodeController node, PathResponse path, ref int currentIndex)
        {
            if (path == null || path.Waypoints == null || currentIndex >= path.Length)
            {
                return IntentResult.Success; // Reached end or empty
            }

            Vector2 targetPos = path.Waypoints[currentIndex];

            // Ignore IsPositionBlocked entirely to physically break free from any deadlock!
            float speed = (node != null ? node.BaseSpeed : 2f) * 1.5f;
            float step = speed * Time.deltaTime;
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
            if (path == null || path.Waypoints == null || state.CurrentWaypointIndex >= path.Length)
            {
                return IntentResult.Success; // Reached end or empty
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
                // Do not advance position into the obstructed target this frame.
                // Stationary state will accumulate TimeSinceLastProgress in Section 1.
                // If blocked continuously for >= 1.0s, the progressive watchdog reflex will handle unsticking cleanly.
                return IntentResult.InProgress;
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
                // StallCount 1: shifts +0.2f along perpendicular.
                // StallCount 2: shifts -0.4f to test the opposite flank (-0.2f relative to original centerline).
                float displacement = (state.StallCount == 1) ? 0.2f : -0.4f;
                Vector2 perpendicular = new Vector2(-dir.y, dir.x) * displacement;
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
                    if (entity.Type == EntityType.Anchor && Vector2.Distance(entity.Position, targetPos) < 0.45f) return true;
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
                hri.Path?.ReturnToPool();
            }
            else if (intent is WanderIntent wi)
            {
                wi.Path?.ReturnToPool();
            }
            else if (intent is PanicIntent pi)
            {
                pi.Path?.ReturnToPool();
            }

            intent?.OnComplete?.Invoke(result);
        }
    }
}
