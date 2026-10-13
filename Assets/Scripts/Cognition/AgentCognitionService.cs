using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Evaluators;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Goals;
using Simpiens.Diagnostics;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Microsoft.Extensions.Logging;
using UnityEngine;
using ZLogger;

namespace Simpiens.Cognition
{
    /// <summary>
    /// Pure C# implementation of IAgentCognitionService.
    /// Processes agent reasoning asynchronously off the main Unity game loop.
    /// </summary>
    public class AgentCognitionService : IAgentCognitionService
    {
        public const int DefaultVisionRadius = 6;
        private readonly IAgentConsequenceService _consequenceService;
        private readonly ICognitiveEvaluator _fallbackEvaluator;

        /// <param name="fallbackEvaluator">Optional Tier-3 evaluator (e.g. AmbientWanderEvaluator) consulted
        /// before the bare IdleIntent when neither reflexes nor GOAP produced an intent.</param>
        public AgentCognitionService(IAgentConsequenceService consequenceService = null, ICognitiveEvaluator fallbackEvaluator = null)
        {
            _consequenceService = consequenceService ?? new AgentConsequenceService();
            _fallbackEvaluator = fallbackEvaluator;
        }

        public async UniTask EvaluateAgentCognitionAsync(
            AgentState agent,
            ISpatialPartition spatialPartition,
            ISimulationManager simulationManager,
            ISimulationClock clock,
            IGoapPlanner planner,
            ActionGraph actionGraph,
            IReadOnlyList<GoapGoal> goals,
            ICognitiveEvaluator[] evaluators,
            CancellationToken ct = default)
        {
            if (agent == null || spatialPartition == null || simulationManager == null) return;
            if (agent.IsEvaluating || agent.HasActiveIntent) return;

            var snapshot = spatialPartition.GetActiveSnapshot();
            if (snapshot == null) return;

            // The session this decision belongs to (main thread, before any await)
            uint sessionEpoch = simulationManager.SessionEpoch;
            agent.IsEvaluating = true;
            if (agent.IsExhaustionCollapsed)
            {
                agent.VisualState = AgentVisualState.Resting;
            }
            else if (agent.IsStarvationPanicking || agent.TantrumTimer > 0f)
            {
                agent.VisualState = AgentVisualState.Panicking;
            }
            else if (!agent.HasActivePlan)
            {
                agent.VisualState = AgentVisualState.Thinking;
            }

            Vector2Int currentPosInt = Vector2Int.RoundToInt(agent.Position);
            uint tick = clock != null ? (uint)clock.CurrentTick : 0;

            // 1. Pre-cognition sync on main thread
            agent.Memory.UpdateMemory(snapshot, currentPosInt, visionRadius: DefaultVisionRadius, currentTick: tick,
                surveySectorId: BountyQueries.SurveyingSector(snapshot, agent.AgentId));

            // Group directives & compliance evaluation
            if (agent.Membership.HasGroup && agent.Membership.Role != RoleRank.Leader)
            {
                if (snapshot.TryGetGroup(agent.Membership.GroupId, out var groupData) && groupData.ActiveDirective.IsActive(tick))
                {
                    if (groupData.ActiveDirective.IssuedTick != agent.LastEvaluatedDirectiveTick)
                    {
                        agent.LastEvaluatedDirectiveTick = groupData.ActiveDirective.IssuedTick;
                        agent.Memory.RecordEvent(MemoryEventType.DirectiveReceived, groupData.LeaderId, currentPosInt, tick);

                        var evalContext = agent.CreateContext(snapshot, tick);
                        var decision = ComplianceUtility.Evaluate(evalContext, groupData);

                        SimLog.Groups.ZLogDebug($"{SimLog.Agent(agent.AgentId)} evaluated directive {groupData.ActiveDirective.Type} from group {groupData.GroupId}: {decision}");

                        if (decision == ComplianceDecision.ActiveDesertion)
                        {
                            byte oldGroupId = agent.Membership.GroupId;
                            agent.Membership = GroupMembership.None;
                            if (simulationManager.Groups != null)
                            {
                                simulationManager.Groups.RemoveMember(oldGroupId, agent.AgentId);
                            }
                            agent.Memory.RecordEvent(MemoryEventType.DesertedGroup, groupData.LeaderId, currentPosInt, tick);
                            agent.TriggerDefiantVisual(2.5f);
                        }
                        else if (decision == ComplianceDecision.PassiveDefiance)
                        {
                            if (simulationManager.Groups != null)
                            {
                                simulationManager.Groups.UpdateAllegiance(agent.AgentId, -5);
                                if (simulationManager.Groups.TryGetMembership(agent.AgentId, out var updatedMem))
                                {
                                    agent.Membership = updatedMem;
                                }
                            }
                            else
                            {
                                agent.Membership = new GroupMembership(
                                    agent.Membership.GroupId,
                                    agent.Membership.Role,
                                    (byte)Mathf.Max(0, agent.Membership.Allegiance - 5),
                                    agent.Membership.JoinTick);
                            }
                            agent.TriggerDefiantVisual(1.5f);
                        }
                    }
                }
            }

            var context = agent.CreateContext(snapshot, tick);

            // Main thread: whether a ration could be bought right now (a stall is food a crawler knows about too)
            agent.CanBuyFoodAtStall = ResourceTargeting.HasStallFood(context);

            // Retain snapshot immediately on main thread before switching threads
            snapshot.Retain();

            try
            {
                // Explicitly jump off the main thread for cognitive evaluation
                await UniTask.SwitchToThreadPool();

                AgentIntent intent = null;
                string source = null;
                // Top three goals considered this evaluation ("Why?" groundwork); names are cached strings, no allocation
                string topGoal1 = null, topGoal2 = null, topGoal3 = null;
                float topPriority1 = -1f, topPriority2 = -1f, topPriority3 = -1f;
                bool trace = SimLog.Traces(context.AgentId);

                // Emergency rest if in exhaustion collapse
                if (agent.IsExhaustionCollapsed)
                {
                    intent = new IdleIntent(context.AgentId, 2.0f);
                    source = "ExhaustionCollapse";
                }

                // Tier 1: Check Reflexive Evaluators (Panic, Emergency Drives)
                if (intent == null && evaluators != null)
                {
                    for (int i = 0; i < evaluators.Length; i++)
                    {
                        intent = await evaluators[i].EvaluateAsync(context, ct);
                        if (intent != null)
                        {
                            if (trace && agent.HasActivePlan)
                            {
                                SimLog.Planning.ZLogDebug($"{SimLog.Agent(context.AgentId)} plan for {agent.ActiveGoal?.Name} pre-empted by reflex {evaluators[i].GetType().Name}");
                            }
                            agent.AbortActivePlan(resetVisual: false);
                            source = evaluators[i].GetType().Name;
                            break;
                        }
                    }
                }

                // Tier 2: Deliberative GOAP Plan Execution & Generation
                if (intent == null && planner != null && actionGraph != null)
                {
                    // A. Step active plan
                    if (agent.HasActivePlan)
                    {
                        var currentAction = agent.ActivePlan.CurrentAction;
                        if (currentAction != null && currentAction.IsValid(context))
                        {
                            intent = await currentAction.CreateIntentAsync(context, ct);
                            source = "GOAP step";
                            if (trace)
                            {
                                SimLog.Planning.ZLogDebug($"{SimLog.Agent(context.AgentId)} step {agent.ActivePlan.CurrentStepIndex + 1}/{agent.ActivePlan.Count} {currentAction.Name} for {agent.ActiveGoal?.Name}");
                            }
                        }
                        else
                        {
                            if (trace)
                            {
                                SimLog.Planning.ZLogDebug($"{SimLog.Agent(context.AgentId)} plan for {agent.ActiveGoal?.Name} aborted: step {currentAction?.Name ?? "none"} no longer valid");
                            }
                            agent.AbortActivePlan(resetVisual: false);
                        }
                    }

                    // B. Formulate new plan if none active
                    if (intent == null && !agent.HasActivePlan && goals != null && goals.Count > 0)
                    {
                        var currentState = WorldStateBuilder.BuildWorldState(context, context.Inventory);
                        GoapGoal bestGoal = null;
                        float highestPriority = -1f;

                        int goalCount = goals.Count;
                        for (int i = 0; i < goalCount; i++)
                        {
                            var goal = goals[i];
                            if (!goal.IsValid(context))
                            {
                                if (trace) SimLog.Planning.ZLogTrace($"{SimLog.Agent(context.AgentId)} goal {goal.Name}: invalid");
                                continue;
                            }
                            if (goal.IsSatisfied(currentState))
                            {
                                if (trace) SimLog.Planning.ZLogTrace($"{SimLog.Agent(context.AgentId)} goal {goal.Name}: already satisfied");
                                continue;
                            }

                            float priority = goal.CalculatePriority(context);
                            if (trace) SimLog.Planning.ZLogTrace($"{SimLog.Agent(context.AgentId)} goal {goal.Name}: priority {priority:F1}");
                            if (priority > topPriority1)
                            {
                                topGoal3 = topGoal2; topPriority3 = topPriority2;
                                topGoal2 = topGoal1; topPriority2 = topPriority1;
                                topGoal1 = goal.Name; topPriority1 = priority;
                            }
                            else if (priority > topPriority2)
                            {
                                topGoal3 = topGoal2; topPriority3 = topPriority2;
                                topGoal2 = goal.Name; topPriority2 = priority;
                            }
                            else if (priority > topPriority3)
                            {
                                topGoal3 = goal.Name; topPriority3 = priority;
                            }
                            if (priority > highestPriority)
                            {
                                highestPriority = priority;
                                bestGoal = goal;
                            }
                        }

                        if (bestGoal != null)
                        {
                            bool planFound = planner.Plan(currentState, bestGoal, actionGraph, context, agent.ScratchPlan);
                            if (planFound && !agent.ScratchPlan.IsEmpty)
                            {
                                agent.ActivePlan.CopyFrom(agent.ScratchPlan);
                                agent.ActiveGoal = bestGoal;

                                if (trace)
                                {
                                    SimLog.Planning.ZLogDebug($"{SimLog.Agent(context.AgentId)} chose {bestGoal.Name} (priority {highestPriority:F1}, hunger {context.Hunger:F0}, energy {context.Energy:F0}, social {context.Social:F0}, gold {(context.Inventory != null ? context.Inventory.Gold : 0)}): {agent.ActivePlan}");
                                }

                                var firstAction = agent.ActivePlan.CurrentAction;
                                if (firstAction != null && firstAction.IsValid(context))
                                {
                                    intent = await firstAction.CreateIntentAsync(context, ct);
                                    source = "GOAP new plan";
                                }
                            }
                            else if (trace)
                            {
                                SimLog.Planning.ZLogDebug($"{SimLog.Agent(context.AgentId)} found NO plan for {bestGoal.Name} (priority {highestPriority:F1}) from {currentState}");
                            }
                        }
                    }
                }

                // Tier 3: Ambient fallback, then bare Idle
                if (intent == null && _fallbackEvaluator != null)
                {
                    intent = await _fallbackEvaluator.EvaluateAsync(context, ct);
                    source = "Tier-3 fallback";
                }

                if (intent == null)
                {
                    intent = new IdleIntent(context.AgentId);
                    source = "Tier-3 idle";
                }

                if (trace && SimLog.Cognition.IsEnabled(LogLevel.Debug))
                {
                    SimLog.Cognition.ZLogDebug($"{SimLog.Agent(context.AgentId)} -> {intent.GetType().Name} from {source} at {context.Position}");
                }

                intent.Decision = new DecisionSummary(source, context.CurrentTick,
                    topGoal1, topPriority1, topGoal2, topPriority2, topGoal3, topPriority3);

                // Set intent tracking so agent waits until it completes
                agent.CurrentIntent = intent;
                agent.HasActiveIntent = true;
                intent.OnComplete = (result) =>
                {
                    if (agent.CurrentIntent == intent)
                    {
                        uint completeTick = clock != null ? (uint)clock.CurrentTick : 0;
                        _consequenceService.ApplyConsequences(agent, intent, result, completeTick);
                    }
                };

                // Enqueue intent to simulation manager, stamped with its session (a respawn may have happened meanwhile)
                intent.SessionEpoch = sessionEpoch;
                simulationManager.EnqueueIntent(intent);
            }
            catch (OperationCanceledException)
            {
                // Clean cancellation
            }
            catch (Exception ex)
            {
                // Fire-and-forget evaluation: surface failures instead of losing them to the unobserved-task handler
                SimLog.Cognition.ZLogError(ex, $"Cognition evaluation failed for {SimLog.Agent(agent.AgentId)}");
            }
            finally
            {
                snapshot.Release();
                agent.IsEvaluating = false;
            }
        }
    }
}
