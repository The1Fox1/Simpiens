using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Diagnostics;
using Simpiens.Entities;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Simpiens.Cognition
{
    /// <summary>
    /// Authoritative owner of action consequences for AgentState.
    /// Eliminates split consequence ownership between SimulationManager and agent views.
    /// </summary>
    public class AgentConsequenceService : IAgentConsequenceService
    {
        private readonly IGroupRegistry _groupRegistry;
        private readonly ISpatialPartition _spatialPartition;
        private readonly INarrativeChronicleService _chronicleService;

        public AgentConsequenceService(
            IGroupRegistry groupRegistry = null,
            ISpatialPartition spatialPartition = null,
            INarrativeChronicleService chronicleService = null)
        {
            _groupRegistry = groupRegistry;
            _spatialPartition = spatialPartition;
            _chronicleService = chronicleService;
        }

        public void ApplyConsequences(AgentState agent, AgentIntent intent, IntentResult result, uint currentTick)
        {
            if (agent == null) return;

            bool trace = SimLog.Traces(SimLog.Cognition, LogLevel.Debug, agent.AgentId);
            if (trace)
            {
                SimLog.Cognition.ZLogDebug($"{SimLog.Agent(agent.AgentId)} {intent?.GetType().Name} -> {result}{(intent is HarvestResourceIntent h && h.UnitsHarvested > 0 ? $" (yielded {h.UnitsHarvested}x {h.YieldedItem})" : "")} at {agent.Position}");
            }

            agent.CurrentIntent = null;
            agent.HasActiveIntent = false;
            if (!agent.HasActivePlan)
            {
                agent.VisualState = AgentVisualState.Idle;
            }

            if (intent is PanicIntent)
            {
                agent.Frustration = 0f;
                agent.Memory?.PruneStaleResourceMemories(currentTick);
            }
            else if (result == IntentResult.Success)
            {
                agent.NavigationalStalls = 0;

                // Eating carried food, whoever chose it: a plan step or a hunger reflex
                if (intent is EatCarriedFoodIntent && ItemUse.Consume(agent, agent.Inventory.FirstHeld(Simpiens.Simulation.Items.ItemCategory.Food)))
                {
                    agent.Activity.RecordMeal(currentTick);
                }

                if (agent.HasActivePlan)
                {
                    var completedAction = agent.ActivePlan.CurrentAction;
                    if (completedAction is HarvestResourceAction && YieldedFood(intent))
                    {
                        agent.Hunger = SimMath.Max(0f, agent.Hunger - 50f);
                        agent.HasFoodInInventory = true;
                        agent.Activity.RecordMeal(currentTick);
                    }
                    else if (completedAction is RestAction)
                    {
                        agent.Energy = SimMath.Min(100f, agent.Energy + 30f);
                    }
                    else if (completedAction is SleepAtBunkAction && intent is SleepAtBunkIntent)
                    {
                        agent.Energy = SimMath.Min(100f, agent.Energy + Simpiens.Cognition.Planning.Considerations.BunkSleepBonus);
                    }
                    else if (completedAction is TravelToPeerAction)
                    {
                        agent.Social = SimMath.Min(100f, agent.Social + 25f);
                    }
                    else if (completedAction is RelaxAtAnchorAction)
                    {
                        agent.Social = SimMath.Min(100f, agent.Social + 20f);
                        agent.Energy = SimMath.Min(100f, agent.Energy + 10f);
                    }
                    else if (completedAction is TravelToDirectiveAction)
                    {
                        agent.TriggerRallyVisual(1.5f);
                    }
                    else if (completedAction is ExecuteDirectiveAction)
                    {
                        agent.TriggerRallyVisual(2.0f);
                        if (_groupRegistry != null)
                        {
                            _groupRegistry.UpdateAllegiance(agent.AgentId, 3);
                            if (_groupRegistry.TryGetMembership(agent.AgentId, out var updatedMem))
                            {
                                agent.Membership = updatedMem;
                            }
                        }
                        else
                        {
                            agent.Membership = new GroupMembership(
                                agent.Membership.GroupId,
                                agent.Membership.Role,
                                (byte)SimMath.Min(100, agent.Membership.Allegiance + 3),
                                agent.Membership.JoinTick);
                        }

                        GridPoint agentPos = GridPoint.RoundToInt(agent.Position);
                        EntityId leaderId = default;
                        if (_spatialPartition != null)
                        {
                            var snap = _spatialPartition.GetActiveSnapshot();
                            if (snap != null && snap.TryGetGroup(agent.Membership.GroupId, out var gData) && gData.LeaderId != default)
                            {
                                leaderId = gData.LeaderId;
                            }
                        }

                        if (leaderId != default)
                        {
                            var aff = agent.Memory.GetAffinity(leaderId, currentTick).WithInteraction(1, 2, 0, 2, currentTick);
                            agent.Memory.SetAffinity(leaderId, aff);
                        }

                        agent.Memory.RecordEvent(MemoryEventType.DirectiveComplied, leaderId, agentPos, currentTick);
                        _chronicleService?.RecordEvent(new ChronicleEvent(
                            ChronicleEventType.DirectiveComplied,
                            agent.AgentId,
                            leaderId,
                            0,
                            currentTick,
                            ChronicleIntensity.Notable));
                    }

                    agent.ActivePlan.AdvanceStep();
                    if (agent.ActivePlan.IsFinished)
                    {
                        if (trace) SimLog.Planning.ZLogDebug($"{SimLog.Agent(agent.AgentId)} completed plan for {agent.ActiveGoal?.Name}");
                        agent.AbortActivePlan();
                    }
                }
                else if (intent is HarvestResourceIntent && YieldedFood(intent))
                {
                    agent.Hunger = SimMath.Max(0f, agent.Hunger - 50f);
                    agent.HasFoodInInventory = true;
                    agent.Activity.RecordMeal(currentTick);
                }
            }
            else if (result == IntentResult.TargetMissing || result == IntentResult.PathBlocked || result == IntentResult.Aborted)
            {
                if (trace && agent.HasActivePlan) SimLog.Planning.ZLogDebug($"{SimLog.Agent(agent.AgentId)} plan for {agent.ActiveGoal?.Name} aborted at {agent.ActivePlan.CurrentAction?.Name}: {result}");
                agent.NavigationalStalls++;
                agent.AbortActivePlan();
            }
            else if (result == IntentResult.Failed)
            {
                // Transaction/precondition failure (bounty taken, stall broke, inventory full): replan rather
                // than retrying the same step forever
                if (trace && agent.HasActivePlan) SimLog.Planning.ZLogDebug($"{SimLog.Agent(agent.AgentId)} plan for {agent.ActiveGoal?.Name} aborted at {agent.ActivePlan.CurrentAction?.Name}: Failed");
                agent.AbortActivePlan();
            }

            if (intent is HarvestResourceIntent harvestIntent && harvestIntent.TargetEntityId != default)
            {
                if (result == IntentResult.TargetMissing)
                {
                    agent.Memory?.RemoveMemory(harvestIntent.TargetEntityId);
                }
                else if (result == IntentResult.PathBlocked || result == IntentResult.Aborted)
                {
                    agent.Memory?.BlacklistEntity(harvestIntent.TargetEntityId, currentTick + 100);
                }
            }
        }

        /// <summary>
        /// Hunger relief applies only to food harvests; salvage (ore/herb) harvests leave hunger unchanged.
        /// Non-harvest intents (headless validators completing a plan step) keep the legacy food semantics.
        /// </summary>
        private static bool YieldedFood(AgentIntent intent)
        {
            return !(intent is HarvestResourceIntent harvest) || harvest.YieldedItem.IsFoodYield();
        }
    }
}
