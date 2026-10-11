using UnityEngine;
using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using ZLogger;
using Simpiens.Cognition.Memory;
using Simpiens.Entities;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Cognition.Planning.Handlers
{
    public class HarvestResourceIntentHandler : IntentHandlerBase<HarvestResourceIntent>
    {
        protected override IntentResult Execute(NodeController node, HarvestResourceIntent harvest, ActiveIntentState state, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            uint currentTick = context.CurrentTick;

            // 1. Check if resource is already depleted
            bool isDepleted = !context.TryGetResource(harvest.TargetEntityId, out var resourceData) || resourceData.IsDormant || resourceData.RemainingYield <= 0;
            if (isDepleted && harvest.UnitsHarvested > 0)
            {
                // Multi-unit gather ran the node dry after at least one unit: keep what was gathered
                return IntentResult.Success;
            }
            if (isDepleted)
            {
                // An agent only suffers a contention defeat if:
                // 1. A head-to-head rivalry was already established (state.ContestedCompetitorId != default), OR
                // 2. The agent had already reached the bush and was actively harvesting when it depleted (state.ElapsedTime > 0f)
                if (state.ContestedCompetitorId == default && state.ElapsedTime > 0f && context.WorldRegistry != null)
                {
                    var activeNodes = context.WorldRegistry.ActiveNodes;
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
                    agent.VisualState = AgentVisualState.Panicking;
                    agent.TriggerTantrumVisual();
                    agent.Memory?.BlacklistEntity(harvest.TargetEntityId, currentTick + 150);

                    agent.Memory?.EventLedger?.AddEvent(new MemoryEvent(
                        MemoryEventType.ContentionFaced,
                        state.ContestedCompetitorId,
                        Vector2Int.RoundToInt(node.Position),
                        currentTick));

                    agent.Memory?.RecordInteraction(state.ContestedCompetitorId, deltaWarmth: -15, deltaTrust: -10, deltaFear: 5, deltaRespect: -10, currentTick);

                    SimLog.Social.ZLogInformation($"Contention defeat: {SimLog.Agent(node.Id)} lost {SimLog.Agent(harvest.TargetEntityId)} to {SimLog.Agent(state.ContestedCompetitorId)}; +25 frustration, tantrum");
                }
                return IntentResult.TargetMissing;
            }

            var targetResourceNode = context.WorldRegistry?.GetNode(harvest.TargetEntityId);
            bool withinHarvestReach = state.ElapsedTime > 0f || harvest.UnitsHarvested > 0;
            if (!withinHarvestReach && targetResourceNode != null)
            {
                float distToResource = Vector2.Distance(node.Position, targetResourceNode.Position);
                if (distToResource <= 0.8f)
                {
                    withinHarvestReach = true;
                }
            }

            var reachResult = withinHarvestReach ? IntentResult.Success : context.ExecutePathMovement(node, harvest.Path, state);
            if (reachResult == IntentResult.Success)
            {
                // Ensure the agent is genuinely within physical harvest proximity of the target resource
                if (targetResourceNode != null && Vector2.Distance(node.Position, targetResourceNode.Position) > 1.2f)
                {
                    if (SimLog.Traces(SimLog.Navigation, Microsoft.Extensions.Logging.LogLevel.Debug, node.Id))
                    {
                        SimLog.Navigation.ZLogDebug($"{SimLog.Agent(node.Id)} path ended {Vector2.Distance(node.Position, targetResourceNode.Position):F2}m from {SimLog.Agent(harvest.TargetEntityId)} (needs <= 1.2m); harvest PathBlocked");
                    }
                    return IntentResult.PathBlocked;
                }

                // Social Yielding Protocol: Check if any other pawn is also targeting/harvesting this node
                NodeController competitorNode = null;
                if (context.WorldRegistry != null && context.ActiveIntents != null)
                {
                    foreach (var kvp in context.ActiveIntents)
                    {
                        if (kvp.Key != node.Id && kvp.Value.Intent is HarvestResourceIntent otherHarvest)
                        {
                            if (otherHarvest.TargetEntityId == harvest.TargetEntityId)
                            {
                                // Lowest id wins ties so the outcome never depends on Dictionary order
                                var otherNode = context.WorldRegistry.GetNode(kvp.Key);
                                if (otherNode != null && Vector2.Distance(otherNode.Position, node.Position) <= 2.5f &&
                                    (competitorNode == null || otherNode.Id.CompareTo(competitorNode.Id) < 0))
                                {
                                    competitorNode = otherNode;
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
                        agent?.TriggerYieldVisual(currentTick);

                        // Step back 0.5 units away from competitor
                        Vector2 awayDir = ((Vector2)node.Position - (Vector2)competitorNode.Position).normalized;
                        if (awayDir == Vector2.zero) awayDir = Vector2.down;
                        context.TryDisplacePawn(node, (Vector2)node.Position + awayDir * 0.5f);

                        // Blacklist node for 150 ticks
                        agent?.Memory?.BlacklistEntity(harvest.TargetEntityId, currentTick + 150);

                        // Log memory events
                        agent?.Memory?.EventLedger?.AddEvent(new MemoryEvent(
                            MemoryEventType.ResourceYielded,
                            competitorNode.Id,
                            Vector2Int.RoundToInt(node.Position),
                            currentTick));
                        competitorNode.Agent?.Memory?.EventLedger?.AddEvent(new MemoryEvent(
                            MemoryEventType.ResourceClaimed,
                            node.Id,
                            Vector2Int.RoundToInt(competitorNode.Position),
                            currentTick));

                        context.ChronicleService?.RecordEvent(new ChronicleEvent(
                            ChronicleEventType.ResourceYielded,
                            node.Id,
                            competitorNode.Id,
                            context.GetSectorForPosition(node.Position),
                            currentTick,
                            ChronicleIntensity.Notable));

                        SimLog.Social.ZLogInformation($"Social yielding: {SimLog.Agent(node.Id)} yielded {SimLog.Agent(harvest.TargetEntityId)} to dominant {SimLog.Agent(competitorNode.Id)} (submission {subA:F0} vs {subB:F0}); stepped back 0.5m");

                        return IntentResult.PathBlocked;
                    }

                    // Otherwise, Symmetrical Rivalry: mark contested competitor on both pawns
                    state.ContestedCompetitorId = competitorNode.Id;
                    if (context.ActiveIntents != null && context.ActiveIntents.TryGetValue(competitorNode.Id, out var compState))
                    {
                        compState.ContestedCompetitorId = node.Id;
                    }
                }

                if (agent != null) agent.VisualState = AgentVisualState.Harvesting;

                state.ElapsedTime += context.FixedTickDeltaTime;
                if (state.ElapsedTime < harvest.GatherDuration)
                {
                    return IntentResult.InProgress;
                }

                // Typed yield: salvage goes into the inventory (single writer: main thread). A full inventory
                // fails the harvest without consuming yield. Food keeps its hunger-relief path in the consequence service.
                ItemType yieldItem = resourceData.YieldItem;
                if (yieldItem.IsSalvage())
                {
                    var inventory = agent != null ? agent.Inventory : null;
                    if (inventory == null || !inventory.TryAddItem(new ItemInstance((ushort)yieldItem, yieldItem, 1, yieldItem.BaseValue()), out _))
                    {
                        SimLog.Economy.ZLogDebug($"{SimLog.Agent(node.Id)} cannot store {yieldItem} from {SimLog.Agent(harvest.TargetEntityId)}: inventory full ({harvest.UnitsHarvested} units kept)");
                        return harvest.UnitsHarvested > 0 ? IntentResult.Success : IntentResult.Failed;
                    }
                }

                // State Mutation: Harvesting completed
                resourceData.RemainingYield--;
                harvest.YieldedItem = yieldItem == ItemType.None ? ItemType.Ration : yieldItem;
                harvest.UnitsHarvested++;
                if (SimLog.Traces(SimLog.Economy, Microsoft.Extensions.Logging.LogLevel.Debug, node.Id))
                {
                    SimLog.Economy.ZLogDebug($"{SimLog.Agent(node.Id)} harvested {harvest.YieldedItem} from {SimLog.Agent(harvest.TargetEntityId)} (unit {harvest.UnitsHarvested}/{harvest.TargetUnits}, {resourceData.RemainingYield} left)");
                }

                // If this was a contested victory, log ContentionFaced and dyadic hit
                if (state.ContestedCompetitorId != default)
                {
                    agent?.Memory?.EventLedger?.AddEvent(new MemoryEvent(
                        MemoryEventType.ContentionFaced,
                        state.ContestedCompetitorId,
                        Vector2Int.RoundToInt(node.Position),
                        currentTick));
                    agent?.Memory?.RecordInteraction(state.ContestedCompetitorId, deltaWarmth: -15, deltaTrust: -10, deltaFear: 0, deltaRespect: -10, currentTick);

                    context.ChronicleService?.RecordEvent(new ChronicleEvent(
                        ChronicleEventType.ResourceClaimed,
                        node.Id,
                        state.ContestedCompetitorId,
                        context.GetSectorForPosition(node.Position),
                        currentTick,
                        ChronicleIntensity.Notable));

                    SimLog.Social.ZLogInformation($"Contention victory: {SimLog.Agent(node.Id)} won {SimLog.Agent(harvest.TargetEntityId)} against {SimLog.Agent(state.ContestedCompetitorId)}");

                    // Rivalry resolved; further units of a multi-unit gather are uncontested
                    state.ContestedCompetitorId = default;
                }

                // Depletion: the simulation decides dormancy (regrowable) vs. permanent removal and owns the view
                if (resourceData.RemainingYield <= 0)
                {
                    context.DepleteResource(harvest.TargetEntityId);
                    return IntentResult.Success;
                }

                // Multi-unit gather: keep harvesting in place until the requested units are collected
                if (harvest.UnitsHarvested < harvest.TargetUnits)
                {
                    state.ElapsedTime = 0f;
                    return IntentResult.InProgress;
                }
                return IntentResult.Success;
            }
            else if (reachResult == IntentResult.InProgress)
            {
                if (agent != null) agent.VisualState = AgentVisualState.Walking;
            }
            return reachResult;
        }
    }
}
