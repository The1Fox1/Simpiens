using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using ZLogger;

namespace Simpiens.Cognition.Planning.Handlers
{
    /// <summary>Eats one medicinal herb and heals <see cref="Vitality.HerbHeal"/> (Epic 10 G2; main thread, single writer).</summary>
    public class TreatWoundsIntentHandler : IntentHandlerBase<TreatWoundsIntent>
    {
        protected override IntentResult Execute(WorldEntity node, TreatWoundsIntent treat, ActiveIntentState state, IIntentExecutionContext context)
        {
            var agent = node.Agent;
            if (agent?.Inventory == null || !agent.Inventory.TryRemoveItem(ItemType.MedicinalHerb, 1)) return IntentResult.TargetMissing;
            float before = agent.Health;
            agent.Health = SimMath.Min(Vitality.MaxHealth, agent.Health + Vitality.HerbHeal);
            agent.Activity.HerbsEaten++;
            SimLog.Drives.ZLogInformation($"{SimLog.Agent(node.Id)} ate a medicinal herb: health {before:F0} -> {agent.Health:F0}");
            return IntentResult.Success;
        }
    }
}
