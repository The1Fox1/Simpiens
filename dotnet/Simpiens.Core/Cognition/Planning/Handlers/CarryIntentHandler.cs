using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using Simpiens.Narrative;
using Simpiens.Simulation;
using ZLogger;

namespace Simpiens.Cognition.Planning.Handlers
{
    /// <summary>
    /// Executes a CarryIntent (Epic 10 Part 3, K5.2, A7): the walk to the fallen on its path, the lift (<c>ActionTuning.LiftSeconds</c>),
    /// which couples the two (<see cref="AgentState.Carrying"/>, <see cref="AgentState.CarriedBy"/>), then the carry on the path
    /// out at half pace, the fallen's place the rescuer's each tick, until both are out of danger: there it is set down, downed
    /// in safety (it limps on to the bunks itself). A fallen that is gone, back on its feet or carried by another is a missing
    /// target; a rescuer downed itself lets go and fails. However the carry ends early (the rescuer struck, downed or turned
    /// to something else), the simulation's pass over the fallen uncouples them where they are.
    /// </summary>
    public class CarryIntentHandler : IntentHandlerBase<CarryIntent>
    {
        /// <summary>How close the rescuer stands to lift the fallen.</summary>
        public const float Reach = 0.8f;

        protected override IntentResult Execute(WorldEntity node, CarryIntent carry, ActiveIntentState state, IIntentExecutionContext context)
        {
            var rescuer = node.Agent;
            var fallenNode = context.WorldRegistry?.GetEntity(carry.FallenId);
            var fallen = fallenNode?.Agent;
            if (rescuer != null && rescuer.IsDowned)
            {
                Release(rescuer, fallen); // downed itself (struck while it carried, under orders): it lets go where it falls (K7.2c)
                return IntentResult.Failed;
            }
            if (rescuer == null || fallen == null || fallen.HasLeft || !fallen.IsDowned)
            {
                Release(rescuer, fallen);
                return fallen != null && !fallen.IsDowned ? IntentResult.Success : IntentResult.TargetMissing;
            }

            if (rescuer.Carrying != carry.FallenId)
            {
                if (fallen.CarriedBy != default && fallen.CarriedBy != node.Id) return IntentResult.TargetMissing; // another has it
                // The walk there first (nothing done yet), and only as far as its reach
                float reach = Reach * Reach;
                if (state.ElapsedTime <= 0f && (fallenNode.Position - node.Position).SqrMagnitude > reach)
                {
                    if (carry.PathToFallen == null) return IntentResult.PathBlocked;
                    var walked = context.ExecutePathMovement(node, carry.PathToFallen, state);
                    if (walked != IntentResult.Success) return walked;
                    if ((fallenNode.Position - node.Position).SqrMagnitude > reach * 2.25f) return IntentResult.PathBlocked;
                }
                if (StillWorking(node, state, context, context.Economy.Day.Actions.LiftSeconds)) return IntentResult.InProgress;
                // Lifted: the two move as one from here, on the path out from where it lay
                rescuer.Carrying = fallen.AgentId;
                fallen.CarriedBy = node.Id;
                fallenNode.SetPosition(node.Position);
                // The way out starts where the fallen lay, beside the rescuer: it walks on from the next waypoint, not back
                var outPath = carry.PathOut;
                state.CurrentWaypointIndex = outPath != null && outPath.Length > 1 && (outPath.Waypoints[0] - node.Position).SqrMagnitude <= reach * 2.25f ? 1 : 0;
                state.LastSampledPosition = node.Position;
                state.TimeSinceLastProgress = 0f;
                state.StallCount = 0;
                SimLog.Drives.ZLogInformation($"{SimLog.Agent(node.Id)} lifted {SimLog.Agent(fallen.AgentId)} at {node.Position}");
                return IntentResult.InProgress;
            }

            // Carrying: a step on the path out, the fallen on its back
            var result = context.ExecutePathMovement(node, carry.PathOut, state);
            fallenNode.SetPosition(node.Position);
            if (!context.InDanger(node.Position) || result == IntentResult.Success)
            {
                Release(rescuer, fallen);
                SimLog.Drives.ZLogInformation($"{SimLog.Agent(node.Id)} set {SimLog.Agent(fallen.AgentId)} down out of danger at {node.Position}");
                context.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.CrawlerRescued, node.Id, fallen.AgentId,
                    context.GetSectorForPosition(node.Position), context.CurrentTick, ChronicleIntensity.Dramatic, 1f));
                Rescue.Remember(node.Id, rescuer, fallen, context.CurrentTick);
                return IntentResult.Success;
            }
            if (result != IntentResult.InProgress)
            {
                Release(rescuer, fallen);
                return result;
            }
            return IntentResult.InProgress;
        }

        private static void Release(AgentState rescuer, AgentState fallen)
        {
            if (rescuer != null && fallen != null && rescuer.Carrying == fallen.AgentId) rescuer.Carrying = default;
            if (fallen != null && rescuer != null && fallen.CarriedBy == rescuer.AgentId) fallen.CarriedBy = default;
        }
    }

    /// <summary>
    /// Executes a GiveMedicineIntent (Epic 10 Part 3, K5.3): the walk to the fallen, the hand-over (<c>ActionTuning.GiveSeconds</c>),
    /// then the fallen takes the medicine where it lies (a draught stands it up). A fallen gone or on its feet, or a medicine no
    /// longer carried, is a missing target.
    /// </summary>
    public class GiveMedicineIntentHandler : IntentHandlerBase<GiveMedicineIntent>
    {
        protected override IntentResult Execute(WorldEntity node, GiveMedicineIntent give, ActiveIntentState state, IIntentExecutionContext context)
        {
            var rescuer = node.Agent;
            var fallenNode = context.WorldRegistry?.GetEntity(give.FallenId);
            var fallen = fallenNode?.Agent;
            if (rescuer != null && rescuer.IsDowned) return IntentResult.Failed; // downed itself on the way (K7.2c)
            if (rescuer?.Inventory == null || fallen == null || fallen.HasLeft || !fallen.IsDowned) return IntentResult.TargetMissing;
            if (!rescuer.Inventory.HasItem(give.Medicine)) return IntentResult.TargetMissing;
            float reach = CarryIntentHandler.Reach * CarryIntentHandler.Reach;
            if (state.ElapsedTime <= 0f && (fallenNode.Position - node.Position).SqrMagnitude > reach)
            {
                if (give.Path == null) return IntentResult.PathBlocked;
                var walked = context.ExecutePathMovement(node, give.Path, state);
                if (walked != IntentResult.Success) return walked;
                if ((fallenNode.Position - node.Position).SqrMagnitude > reach * 2.25f) return IntentResult.PathBlocked;
            }
            if (StillWorking(node, state, context, context.Economy.Day.Actions.GiveSeconds)) return IntentResult.InProgress;
            var definition = Simpiens.Simulation.Items.ItemCatalog.Get(give.Medicine);
            if (definition == null || !rescuer.Inventory.TryRemoveItem(give.Medicine, 1)) return IntentResult.TargetMissing;
            float before = fallen.Health;
            ItemUse.Apply(fallen, definition);
            SimLog.Drives.ZLogInformation($"{SimLog.Agent(node.Id)} gave {SimLog.Agent(fallen.AgentId)} {definition.Name}: health {before:F0} -> {fallen.Health:F0}");
            context.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.CrawlerRescued, node.Id, fallen.AgentId,
                context.GetSectorForPosition(node.Position), context.CurrentTick, ChronicleIntensity.Dramatic, 2f));
            Rescue.Remember(node.Id, rescuer, fallen, context.CurrentTick);
            return IntentResult.Success;
        }
    }
}
