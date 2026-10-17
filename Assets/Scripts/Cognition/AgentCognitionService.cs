using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
using ZLogger;

namespace Simpiens.Cognition
{
    /// <summary>
    /// Pure C# implementation of IAgentCognitionService.
    /// Prepares an agent on the main thread, evaluates it off the main thread, and hands the decision to the
    /// <see cref="CognitionScheduler"/>, which applies it on the main thread at a fixed tick (ROADMAP §1A.2 #6, #15).
    /// </summary>
    public class AgentCognitionService : IAgentCognitionService, ICognitionJobHandler
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

        public void BeginEvaluation(
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

            // The session this decision belongs to, and the tick it is dispatched on (main thread)
            uint sessionEpoch = simulationManager.SessionEpoch;
            long dispatchTick = clock != null ? clock.CurrentTick : 0;
            uint tick = (uint)dispatchTick;

            AgentContext context;
            bool collapsed;
            bool hadPlan;

            // 1. Main-thread preparation. The flag is set inside the guarded block, so a failure here can't leave the
            // agent marked as evaluating forever (§1A.2 #18)
            agent.IsEvaluating = true;
            try
            {
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

                GridPoint currentPosInt = GridPoint.RoundToInt(agent.Position);

                agent.Memory.UpdateMemory(snapshot, currentPosInt, visionRadius: DefaultVisionRadius, currentTick: tick,
                    surveySectorId: BountyQueries.SurveyingSector(snapshot, agent.AgentId));

                EvaluateGroupDirective(agent, snapshot, simulationManager, currentPosInt, tick);

                context = agent.CreateContext(snapshot, tick);

                // Main thread: whether a ration could be bought right now (a stall is food a crawler knows about too)
                agent.CanBuyFoodAtStall = ResourceTargeting.HasStallFood(context);

                // What the evaluation reads of the agent itself, captured before it leaves the main thread
                collapsed = agent.IsExhaustionCollapsed;
                hadPlan = agent.HasActivePlan;
            }
            catch (Exception ex)
            {
                agent.IsEvaluating = false;
                SimLog.Cognition.ZLogError(ex, $"Cognition preparation failed for {SimLog.Agent(agent.AgentId)}");
                return;
            }

            // Retain the snapshot on the main thread before the evaluation leaves it; the job releases it
            snapshot.Retain();
            var job = new CognitionJob
            {
                Agent = agent,
                DispatchTick = dispatchTick,
                DueTick = dispatchTick + CognitionScheduler.LatencyTicks,
                SessionEpoch = sessionEpoch,
                Snapshot = snapshot,
                Clock = clock,
                Simulation = simulationManager,
                Handler = this
            };

            var scheduler = simulationManager.Cognition;
            if (scheduler == null)
            {
                // No scheduler (an isolated test double): decide and apply right here, on the main thread
                try
                {
                    var now = EvaluateAsync(agent, context, collapsed, hadPlan, planner, actionGraph, goals, evaluators, ct).AsTask().GetAwaiter().GetResult();
                    Apply(job, now);
                }
                catch (Exception ex)
                {
                    SimLog.Cognition.ZLogError(ex, $"Cognition evaluation failed for {SimLog.Agent(agent.AgentId)}");
                }
                finally
                {
                    Release(job);
                }
                return;
            }

            job.Work = Task.Run(() => EvaluateAsync(agent, context, collapsed, hadPlan, planner, actionGraph, goals, evaluators, ct).AsTask(), ct);
            scheduler.Submit(job);
        }

        // Group directives & compliance evaluation (main thread)
        private static void EvaluateGroupDirective(AgentState agent, SharedWorldSnapshot snapshot, ISimulationManager simulationManager, GridPoint currentPosInt, uint tick)
        {
            if (!agent.Membership.HasGroup || agent.Membership.Role == RoleRank.Leader) return;
            if (!snapshot.TryGetGroup(agent.Membership.GroupId, out var groupData) || !groupData.ActiveDirective.IsActive(tick)) return;
            if (groupData.ActiveDirective.IssuedTick == agent.LastEvaluatedDirectiveTick) return;

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
                        (byte)SimMath.Max(0, agent.Membership.Allegiance - 5),
                        agent.Membership.JoinTick);
                }
                agent.TriggerDefiantVisual(1.5f);
            }
        }

        /// <summary>
        /// The decision itself, off the main thread. Reads the context (snapshots), the agent's active plan (which nothing
        /// writes while the agent is evaluating) and the values captured at dispatch; writes only the agent's
        /// <see cref="AgentState.ScratchPlan"/>. Everything else goes into the result.
        /// </summary>
        private async ValueTask<CognitionResult> EvaluateAsync(
            AgentState agent,
            AgentContext context,
            bool collapsed,
            bool hadPlan,
            IGoapPlanner planner,
            ActionGraph actionGraph,
            IReadOnlyList<GoapGoal> goals,
            ICognitiveEvaluator[] evaluators,
            CancellationToken ct)
        {
            var result = new CognitionResult();
            bool hasPlan = hadPlan;

            AgentIntent intent = null;
            string source = null;
            // Top three goals considered this evaluation ("Why?" groundwork); names are cached strings, no allocation
            string topGoal1 = null, topGoal2 = null, topGoal3 = null;
            float topPriority1 = -1f, topPriority2 = -1f, topPriority3 = -1f;
            bool trace = SimLog.Traces(context.AgentId);

            // Emergency rest if in exhaustion collapse
            if (collapsed)
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
                        if (trace && hasPlan)
                        {
                            SimLog.Planning.ZLogDebug($"{SimLog.Agent(context.AgentId)} plan for {agent.ActiveGoal?.Name} pre-empted by reflex {evaluators[i].GetType().Name}");
                        }
                        result.AbortPlan = true;
                        hasPlan = false;
                        source = evaluators[i].GetType().Name;
                        break;
                    }
                }
            }

            // Tier 2: Deliberative GOAP Plan Execution & Generation
            if (intent == null && planner != null && actionGraph != null)
            {
                // A. Step active plan
                if (hasPlan)
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
                        result.AbortPlan = true;
                        hasPlan = false;
                    }
                }

                // B. Formulate new plan if none active
                if (intent == null && !hasPlan && goals != null && goals.Count > 0)
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
                        var plan = agent.ScratchPlan;
                        bool planFound = planner.Plan(currentState, bestGoal, actionGraph, context, plan);
                        if (planFound && !plan.IsEmpty)
                        {
                            result.AdoptScratchPlan = true;
                            result.Goal = bestGoal;

                            if (trace)
                            {
                                SimLog.Planning.ZLogDebug($"{SimLog.Agent(context.AgentId)} chose {bestGoal.Name} (priority {highestPriority:F1}, hunger {context.Hunger:F0}, energy {context.Energy:F0}, social {context.Social:F0}, gold {(context.Inventory != null ? context.Inventory.Gold : 0)}): {plan}");
                            }

                            var firstAction = plan.CurrentAction;
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

            result.Intent = intent;
            return result;
        }

        /// <summary>Applies a decision on the main thread, at its due tick (single writer, §1A.1 rule 6).</summary>
        public void Apply(CognitionJob job, in CognitionResult result)
        {
            var agent = job.Agent;
            if (result.AbortPlan)
            {
                agent.AbortActivePlan(resetVisual: false);
            }
            if (result.AdoptScratchPlan)
            {
                agent.ActivePlan.CopyFrom(agent.ScratchPlan);
                agent.ActiveGoal = result.Goal;
            }

            var intent = result.Intent;
            if (intent == null) return;

            // Set intent tracking so agent waits until it completes
            agent.CurrentIntent = intent;
            agent.HasActiveIntent = true;
            var clock = job.Clock;
            intent.OnComplete = (outcome) =>
            {
                if (agent.CurrentIntent == intent)
                {
                    uint completeTick = clock != null ? (uint)clock.CurrentTick : 0;
                    _consequenceService.ApplyConsequences(agent, intent, outcome, completeTick);
                }
            };

            // Stamped with its session: a respawn drops it
            intent.SessionEpoch = job.SessionEpoch;
            job.Simulation.EnqueueIntent(intent);
        }

        public void Release(CognitionJob job)
        {
            job.Snapshot?.Release();
            job.Snapshot = null;
            job.Agent.IsEvaluating = false;
        }
    }
}
