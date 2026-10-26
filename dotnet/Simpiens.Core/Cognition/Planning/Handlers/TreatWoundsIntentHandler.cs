using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using ZLogger;

namespace Simpiens.Cognition.Planning.Handlers
{
    /// <summary>Eats one medicine (a medicinal herb) and heals by its definition's effect (Epic 10 G2; main thread, single writer).</summary>
    public class TreatWoundsIntentHandler : IntentHandlerBase<TreatWoundsIntent>
    {
        protected override IntentResult Execute(WorldEntity node, TreatWoundsIntent treat, ActiveIntentState state, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            var medicine = agent?.Inventory != null ? agent.Inventory.FirstHeld(Simpiens.Simulation.Items.ItemCategory.Medicine) : ItemType.None;
            float before = agent?.Health ?? 0f;
            if (!ItemUse.Consume(agent, medicine)) return IntentResult.TargetMissing;
            agent.Activity.HerbsEaten++;
            SimLog.Drives.ZLogInformation($"{SimLog.Agent(node.Id)} ate {Simpiens.Simulation.Items.ItemCatalog.NameOf(medicine)}: health {before:F0} -> {agent.Health:F0}");
            return IntentResult.Success;
        }
    }
}
