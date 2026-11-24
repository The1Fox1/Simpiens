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
        /// <summary>A meal that takes this much off hunger is a good one (Epic LIFE-1, L1.2, D7): a ration's 40, not a nibble.</summary>
        public const float GoodMeal = 30f;

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

        /// <summary>The session's pace: what sleep and rest restore (the default economy's until a session sets one).</summary>
        public DayTuning Day { get; private set; } = EconomyTuning.Default.Day;

        public void SetPace(in DayTuning day) => Day = day;

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
                agent.Memory?.ForgetResourceSightings();
            }
            else if (result == IntentResult.Success)
            {
                agent.NavigationalStalls = 0;

                // Eating carried food, whoever chose it: a plan step or a hunger reflex
                float hungerBefore = agent.Hunger;
                if (intent is EatCarriedFoodIntent && ItemUse.Consume(agent, agent.Inventory.FirstHeld(Simpiens.Simulation.Items.ItemCategory.Food)))
                {
                    agent.Activity.RecordMeal(currentTick);
                    if (hungerBefore - agent.Hunger >= GoodMeal) agent.Think(Simpiens.Simulation.Life.Thought.AGoodMeal, currentTick); // a thought (Epic LIFE-1, L1.2)
                }

                if (agent.HasActivePlan)
                {
                    var completedAction = agent.ActivePlan.CurrentAction;
                    if (completedAction is HarvestResourceAction && YieldedFood(intent))
                    {
                        agent.Hunger = SimMath.Max(0f, agent.Hunger - 50f);
                        agent.HasFoodInInventory = true;
                        agent.Activity.RecordMeal(currentTick);
                        agent.Think(Simpiens.Simulation.Life.Thought.AteAtTheGarden, currentTick); // the Garden plot's meal (Epic LIFE-1, L1.2)
                    }
                    else if (completedAction is RestAction)
                    {
                        agent.Energy = SimMath.Min(100f, agent.Energy + Day.RestBonus);
                    }
                    else if (completedAction is SleepAtBunkAction && intent is SleepAtBunkIntent)
                    {
                        agent.Energy = SimMath.Min(100f, agent.Energy + Day.SleepBonus);
                        agent.Think(Simpiens.Simulation.Life.Thought.SleptInABunk, currentTick); // woke in a bunk (Epic LIFE-1, L1.2)
                        agent.EaseVariety(Simpiens.Cognition.Planning.Leisure.EasePerDay); // a day's variety eased (plan 2, L2.7)
                        agent.JustWoke = true; // a want may arise (plan 2, W.2): the runtime takes it this tick
                    }
                    else if (completedAction is TravelToPeerAction)
                    {
                        agent.Social = SimMath.Min(100f, agent.Social + 25f);
                    }
                    else if (completedAction is RelaxAtAnchorAction)
                    {
                        agent.Social = SimMath.Min(100f, agent.Social + 20f);
                        agent.Energy = SimMath.Min(100f, agent.Energy + Day.RelaxEnergyBonus);
                    }
                    else if (completedAction is TravelToDirectiveAction)
                    {
                        agent.TriggerRallyVisual(1.5f);
                    }
                    else if (completedAction is ExecuteDirectiveAction)
                    {
                        agent.TriggerRallyVisual(2.0f);

                        // The group whose order was carried out: the first, in kind order, with an active directive (Epic 10 G4, D1)
                        var snap = _spatialPartition?.GetActiveSnapshot();
                        var obeyed = agent.Memberships.DirectiveMembership(snap, currentTick);
                        if (_groupRegistry != null)
                        {
                            _groupRegistry.UpdateAllegiance(agent.AgentId, obeyed.GroupId, 3);
                            agent.Memberships = _groupRegistry.GetMemberships(agent.AgentId);
                        }
                        else
                        {
                            var memberships = agent.Memberships;
                            memberships.Update(new GroupMembership(obeyed.GroupId, obeyed.Role, (byte)SimMath.Min(100, obeyed.Allegiance + 3), obeyed.JoinTick));
                            agent.Memberships = memberships;
                        }

                        GridPoint agentPos = GridPoint.RoundToInt(agent.Position);
                        EntityId leaderId = default;
                        if (snap != null && snap.TryGetGroup(obeyed.GroupId, out var gData) && gData.LeaderId != default)
                        {
                            leaderId = gData.LeaderId;
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
