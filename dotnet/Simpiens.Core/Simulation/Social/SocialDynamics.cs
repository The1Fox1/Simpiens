using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;
using Simpiens.Diagnostics;
using Simpiens.Narrative;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using ZLogger;

namespace Simpiens.Simulation.Social
{
    /// <summary>
    /// The outpost's social life on the main thread: gossip between crawlers who meet, the hearth's comfort and bonding,
    /// settlements emerging around it, and leadership succession (Epic 8). Main thread.
    /// </summary>
    internal sealed class SocialDynamics
    {
        private readonly Dictionary<EntityId, AnchorData> _anchors = new Dictionary<EntityId, AnchorData>();
        private readonly List<WorldEntity> _hearthPawnsBuffer = new List<WorldEntity>(32);
        private readonly List<ISocialAgent> _hearthAgentsBuffer = new List<ISocialAgent>(32);
        private readonly List<AnchorData> _activeAnchorsBuffer = new List<AnchorData>(16);
        private readonly SuccessionEngine _successionEngine = new SuccessionEngine();

        private const float GossipRadius = 2.5f;

        private readonly IWorldRegistry _worldRegistry;
        private readonly IGroupRegistry _groupRegistry;
        private readonly ISectorRegistry _sectors;
        private readonly INarrativeChronicleService _chronicleService;
        private readonly ISimulationClock _clock;

        public SocialDynamics(
            IWorldRegistry worldRegistry = null,
            IGroupRegistry groupRegistry = null,
            ISectorRegistry sectors = null,
            INarrativeChronicleService chronicleService = null,
            ISimulationClock clock = null)
        {
            _worldRegistry = worldRegistry;
            _groupRegistry = groupRegistry;
            _sectors = sectors;
            _chronicleService = chronicleService;
            _clock = clock;
        }

        public void RegisterAnchor(EntityId id, Vec2 position, float radius = 3.0f)
        {
            _anchors[id] = new AnchorData(id, position, radius);
        }

        public void CheckGossipOpportunity(WorldEntity node)
        {
            var agentA = node.Agent;
            if (agentA == null || agentA.Memory == null || agentA.IsExhaustionCollapsed) return;
            if (_worldRegistry == null) return;

            uint currentTick = _clock != null ? (uint)_clock.CurrentTick : 0;

            var activeNodes = _worldRegistry.ActiveEntities;
            int count = activeNodes.Count;
            for (int i = 0; i < count; i++)
            {
                var otherNode = activeNodes[i];
                if (otherNode == node || otherNode.Type != EntityType.Pawn) continue;

                float distSqr = (otherNode.Position - node.Position).SqrMagnitude;
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
                                agentA.Social = SimMath.Min(100f, agentA.Social + 25f);
                                agentB.Social = SimMath.Min(100f, agentB.Social + 25f);

                                agentA.TriggerGossipVisual(currentTick);
                                agentB.TriggerGossipVisual(currentTick);

                                _chronicleService?.RecordEvent(new ChronicleEvent(
                                    ChronicleEventType.CampfireGossip,
                                    agentA.AgentId,
                                    agentB.AgentId,
                                    GetSectorForPosition(node.Position),
                                    currentTick,
                                    ChronicleIntensity.Notable));

                                SimLog.Social.ZLogDebug($"Gossip: {SimLog.Agent(agentA.AgentId)} and {SimLog.Agent(agentB.AgentId)} exchanged knowledge");
                                break;
                            }
                        }
                    }
                }
            }
        }

        public void EvaluateHearthProximity(uint currentTick)
        {
            if (_worldRegistry == null) return;

            // Collect all anchors (from registered anchors and active world nodes)
            _activeAnchorsBuffer.Clear();
            foreach (var kvp in _anchors)
            {
                _activeAnchorsBuffer.Add(kvp.Value);
            }
            StableSort.Sort(_activeAnchorsBuffer, (a, b) => a.EntityId.CompareTo(b.EntityId));

            var activeNodes = _worldRegistry.ActiveEntities;
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
                        if (Vec2.Distance(node.Position, anchor.Position) <= anchor.Radius)
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
                        agent.Social = SimMath.Min(100f, agent.Social + 1.0f);
                        if (agent != null) agent.Activity.RecordHearthGathering(currentTick);
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
                                agentA.Social = SimMath.Min(100f, agentA.Social + 25f);
                                agentB.Social = SimMath.Min(100f, agentB.Social + 25f);
                                agentA.TriggerGossipVisual(currentTick);
                                agentB.TriggerGossipVisual(currentTick);

                                _chronicleService?.RecordEvent(new ChronicleEvent(
                                    ChronicleEventType.CampfireGossip,
                                    agentA.AgentId,
                                    agentB.AgentId,
                                    GetSectorForPosition(nodeA.Position),
                                    currentTick,
                                    ChronicleIntensity.Notable));

                                SimLog.Social.ZLogDebug($"Campfire gossip: {SimLog.Agent(agentA.AgentId)} and {SimLog.Agent(agentB.AgentId)} at the hearth");
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
                                GridPoint.RoundToInt(nodeA.Position),
                                currentTick));

                            agentB.Memory.EventLedger?.AddEvent(new Simpiens.Cognition.Memory.MemoryEvent(
                                Simpiens.Cognition.Memory.MemoryEventType.CampfireCompanionship,
                                agentA.AgentId,
                                GridPoint.RoundToInt(nodeB.Position),
                                currentTick));

                            if (currentTick % 200 == 0)
                            {
                                _chronicleService?.RecordEvent(new ChronicleEvent(
                                    ChronicleEventType.CampfireCompanionship,
                                    agentA.AgentId,
                                    agentB.AgentId,
                                    GetSectorForPosition(nodeA.Position),
                                    currentTick,
                                    ChronicleIntensity.Trivial));
                            }
                        }
                    }
                }

                // 3. Emergent Group Formation: Check for unaligned clustering every 100 ticks
                if (_groupRegistry != null && pawnCount >= 3 && currentTick % 100 == 0)
                {
                    _hearthAgentsBuffer.Clear();
                    for (int p = 0; p < pawnCount; p++)
                    {
                        var pawn = _hearthPawnsBuffer[p];
                        ISocialAgent ag = pawn.Agent != null ? (ISocialAgent)pawn.Agent : (ISocialAgent)pawn.Agent;
                        if (ag != null && !ag.Memberships.Has(GroupType.Settlement)) // no settlement: unaligned (Epic 10 G4, D1)
                        {
                            _hearthAgentsBuffer.Add(ag);
                        }
                    }

                    if (_hearthAgentsBuffer.Count >= 3)
                    {
                        if (_successionEngine.TryFormEmergentGroup(anchor.Position, _hearthAgentsBuffer, _groupRegistry, currentTick, out byte newGroupId))
                        {
                            _chronicleService?.RecordEvent(new ChronicleEvent(
                                ChronicleEventType.GroupFormed,
                                _hearthAgentsBuffer[0].AgentId,
                                _hearthAgentsBuffer.Count > 1 ? _hearthAgentsBuffer[1].AgentId : default,
                                GetSectorForPosition(anchor.Position),
                                currentTick,
                                ChronicleIntensity.Dramatic));

                            SimLog.Groups.ZLogInformation($"Group {newGroupId} (settlement) formed around hearth at {anchor.Position} with {_hearthAgentsBuffer.Count} members");
                        }
                    }
                }
            }
        }

        public void EvaluateLeadershipSuccession(uint currentTick)
        {
            if (_groupRegistry == null || _worldRegistry == null) return;

            for (byte g = 1; g <= Simpiens.Simulation.GroupRegistry.MaxGroups; g++)
            {
                if (!_groupRegistry.TryGetGroup(g, out var groupData)) continue;
                if (groupData.IsPatronLed) continue; // the player leads it: nothing succeeds a patron (Epic 10 G4)

                if (SuccessionEngine.IsLeaderIncapacitatedOrDiscredited(groupData.LeaderId, g, _worldRegistry, _groupRegistry, currentTick))
                {
                    var result = _successionEngine.ResolveSuccession(g, _groupRegistry, _worldRegistry, currentTick);
                    if (result.ChangesLeadership)
                    {
                        var eventType = result.Mode == SuccessionMode.FactionalSchism 
                            ? ChronicleEventType.GroupSchism 
                            : ChronicleEventType.LeadershipAssumed;

                        _chronicleService?.RecordEvent(new ChronicleEvent(
                            eventType,
                            result.NewLeaderId,
                            result.PreviousLeaderId,
                            GetSectorForPosition(groupData.AnchorPosition),
                            currentTick,
                            ChronicleIntensity.Crisis));

                        SimLog.Groups.ZLogInformation($"Group {g} succession via {result.Mode}: {SimLog.Agent(result.PreviousLeaderId)} -> {SimLog.Agent(result.NewLeaderId)}");
                    }
                    else if (result.Succeeded)
                    {
                        // Re-confirming the sitting leader is not news; chronicling it every 50 ticks flooded the buffer
                        SimLog.Groups.ZLogDebug($"Group {g} succession check via {result.Mode} re-confirmed leader {SimLog.Agent(result.NewLeaderId)}");
                    }
                }
            }
        }

        public void Reset()
        {
            _anchors.Clear();
            _hearthPawnsBuffer.Clear();
            _hearthAgentsBuffer.Clear();
            _activeAnchorsBuffer.Clear();
        }

        private int GetSectorForPosition(Vec2 position)
        {
            return _sectors != null ? _sectors.GetSectorAt(position).Id : 0;
        }
    }
}
