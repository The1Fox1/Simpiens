using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using ZLogger;
using Simpiens.Cognition.Memory;
using Simpiens.Entities;
using Simpiens.Narrative;
using Simpiens.Simulation;

namespace Simpiens.Cognition.Planning.Handlers
{
    public class ShareResourceIntentHandler : IntentHandlerBase<ShareResourceIntent>
    {
        protected override IntentResult Execute(WorldEntity node, ShareResourceIntent share, ActiveIntentState state, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            uint aidTick = context.CurrentTick;
            var targetNode = context.WorldRegistry?.GetEntity(share.TargetPawnId);
            if (targetNode == null)
            {
                return IntentResult.TargetMissing;
            }

            bool withinAidReach = state.ElapsedTime > 0f;
            if (!withinAidReach)
            {
                float distToTarget = Vec2.Distance(node.Position, targetNode.Position);
                if (distToTarget <= 1.5f)
                {
                    withinAidReach = true;
                }
            }

            var aidReachResult = withinAidReach ? IntentResult.Success : context.ExecutePathMovement(node, share.Path, state);
            if (aidReachResult == IntentResult.Success)
            {
                // A missing or exhausted path must not let food be handed over at a distance
                if (!withinAidReach && Vec2.Distance(node.Position, targetNode.Position) > 2.0f)
                {
                    SimLog.Social.ZLogDebug($"{SimLog.Agent(node.Id)} could not reach {SimLog.Agent(share.TargetPawnId)} to share food ({Vec2.Distance(node.Position, targetNode.Position):F1}m away)");
                    return IntentResult.PathBlocked;
                }

                if (agent != null)
                {
                    agent.VisualState = AgentVisualState.Sharing;
                    agent.TriggerSharingVisual();
                }

                state.ElapsedTime += context.FixedTickDeltaTime;
                if (state.ElapsedTime < share.TransferDuration)
                {
                    return IntentResult.InProgress;
                }

                // State Mutation: Food transfer
                if (agent != null)
                {
                    agent.Hunger = SimMath.Min(100f, agent.Hunger + 15f);
                    agent.HasFoodInInventory = false;
                }

                var receiverAgent = targetNode.Agent;
                if (receiverAgent != null)
                {
                    receiverAgent.Hunger = SimMath.Max(0f, receiverAgent.Hunger - share.FoodAmount);
                    if (receiverAgent != null) receiverAgent.Activity.RecordMeal(context.CurrentTick);
                    if (receiverAgent.Hunger < 85f)
                    {
                        receiverAgent.TriggerReliefVisual();
                    }
                }

                // Giver logs AidGiven (+10 Warmth, +5 Respect)
                agent?.Memory?.EventLedger?.AddEvent(new MemoryEvent(
                    MemoryEventType.AidGiven,
                    share.TargetPawnId,
                    GridPoint.RoundToInt(node.Position),
                    aidTick));
                agent?.Memory?.RecordInteraction(share.TargetPawnId, deltaWarmth: 10, deltaTrust: 5, deltaFear: 0, deltaRespect: 5, aidTick);

                // Receiver logs AidReceived (+25 Warmth, +30 Trust, +15 Respect)
                receiverAgent?.Memory?.EventLedger?.AddEvent(new MemoryEvent(
                    MemoryEventType.AidReceived,
                    node.Id,
                    GridPoint.RoundToInt(targetNode.Position),
                    aidTick));
                receiverAgent?.Memory?.RecordInteraction(node.Id, deltaWarmth: 25, deltaTrust: 30, deltaFear: -10, deltaRespect: 15, aidTick);

                context.ChronicleService?.RecordEvent(new ChronicleEvent(
                    ChronicleEventType.AltruisticFoodShare,
                    node.Id,
                    share.TargetPawnId,
                    context.GetSectorForPosition(node.Position),
                    aidTick,
                    ChronicleIntensity.Dramatic,
                    extraData: share.FoodAmount));

                SimLog.Social.ZLogInformation($"Altruistic aid: {SimLog.Agent(node.Id)} shared {share.FoodAmount:F0} food with {SimLog.Agent(share.TargetPawnId)} (giver hunger {agent?.Hunger ?? 0f:F0}, receiver hunger {receiverAgent?.Hunger ?? 0f:F0})");

                return IntentResult.Success;
            }
            else if (aidReachResult == IntentResult.InProgress)
            {
                if (agent != null) agent.VisualState = AgentVisualState.Walking;
            }
            return aidReachResult;
        }
    }
}
