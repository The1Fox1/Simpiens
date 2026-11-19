using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using Simpiens.Simulation;
using Simpiens.Cognition.Inventory;
using Simpiens.Simulation.Items;
using ZLogger;

namespace Simpiens.Cognition.Planning.Handlers
{
    /// <summary>
    /// Reads the first manual carried, in id order (Epic 11, S3.3, Q10, D11; main thread, single writer): the reading takes
    /// the session's time (<c>ActionTuning.ReadSeconds</c>, two game hours), and only at its end is the manual used up and its
    /// whole experience practised at source Manual, to the manual's ceiling. Interrupted, the reader keeps the book and gains
    /// nothing, so no reading counts twice.
    /// </summary>
    public class ReadManualIntentHandler : IntentHandlerBase<ReadManualIntent>
    {
        protected override IntentResult Execute(WorldEntity node, ReadManualIntent read, ActiveIntentState state, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            var manual = agent?.Inventory != null ? agent.Inventory.FirstHeld(ItemCategory.Manual) : Simpiens.Simulation.Economy.ItemType.None;
            var block = ItemCatalog.Get(manual)?.Manual;
            if (block == null) return IntentResult.TargetMissing;
            if (StillWorking(node, state, context, context.Economy.Day.Actions.ReadSeconds)) return IntentResult.InProgress;
            if (!agent.Inventory.TryRemoveItem(manual, 1)) return IntentResult.TargetMissing;
            context.Practise(agent, block.Work, block.Experience, new Simpiens.Simulation.Skills.PracticeSource(Simpiens.Simulation.Skills.PracticeKind.Manual, block.Ceiling));
            SimLog.Simulation.ZLogInformation($"{SimLog.Agent(node.Id)} read {ItemCatalog.NameOf(manual)}");
            return IntentResult.Success;
        }
    }
}
