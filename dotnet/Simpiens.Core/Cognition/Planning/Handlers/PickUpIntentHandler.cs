using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using Simpiens.Simulation;
using ZLogger;

namespace Simpiens.Cognition.Planning.Handlers
{
    /// <summary>
    /// Executes a PickUpIntent (Epic 10 Part 3, K4.3d): the walk to the pile on its path, then the stoop
    /// (<c>ActionTuning.PickUpSeconds</c>), then the take, through the simulation (<see cref="IIntentExecutionContext.TakeFromPile"/>),
    /// by the loot rule. A pile gone (taken, faded) or out of reach is a missing target: the crawler decides afresh.
    /// </summary>
    public class PickUpIntentHandler : IntentHandlerBase<PickUpIntent>
    {
        protected override IntentResult Execute(WorldEntity node, PickUpIntent pickUp, ActiveIntentState state, IIntentExecutionContext context)
        {
            if (!context.PileExists(pickUp.PileId)) return IntentResult.TargetMissing;
            float reach = LootTargeting.Reach * LootTargeting.Reach;
            if (state.ElapsedTime <= 0f && (pickUp.PilePosition - node.Position).SqrMagnitude > reach)
            {
                if (pickUp.Path == null) return IntentResult.PathBlocked;
                var walked = context.ExecutePathMovement(node, pickUp.Path, state);
                if (walked != IntentResult.Success) return walked;
                if ((pickUp.PilePosition - node.Position).SqrMagnitude > reach * 2.25f) return IntentResult.PathBlocked; // within 1.2
            }
            if (StillWorking(node, state, context, context.Economy.Day.Actions.PickUpSeconds)) return IntentResult.InProgress;
            int units = context.TakeFromPile(node, pickUp.PileId);
            if (units < 0) return IntentResult.TargetMissing;
            if (SimLog.Traces(SimLog.Economy, Microsoft.Extensions.Logging.LogLevel.Debug, node.Id))
            {
                SimLog.Economy.ZLogDebug($"{SimLog.Agent(node.Id)} picked over {SimLog.Agent(pickUp.PileId)}: {units} units");
            }
            return IntentResult.Success;
        }
    }
}
