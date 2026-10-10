using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal: Purchase provisions from the outpost stall when hungry and carrying gold.
    /// DesiredState: HasProvisions = true.
    /// </summary>
    public class PurchaseProvisionsGoal : GoapGoal
    {
        public PurchaseProvisionsGoal() : base("PurchaseProvisions", WorldState.Empty.With(StateFact.HasProvisions, true))
        {
        }

        public override float CalculatePriority(AgentContext context)
        {
            // Elevated priority driven by biological hunger
            return context.Hunger * 1.1f;
        }

        public override bool IsValid(AgentContext context)
        {
            // Valid if agent is hungry, can afford a ration at the nearest stall (which has stock),
            // and does not already hold food. Unreachable stalls invalidate the goal; without any stall, fall back to the 5g floor.
            bool isHungry = context.Hunger >= 50f;
            bool hasFood = context.Inventory != null && context.Inventory.HasItem(ItemType.Ration);
            if (!isHungry || hasFood || context.Inventory == null) return false;

            switch (ResourceTargeting.GetStallAccess(context, out var stall))
            {
                case ResourceTargeting.StallAccess.Reachable:
                    return stall.RationStock > 0 && context.Inventory.Gold >= stall.RationSellPrice;
                case ResourceTargeting.StallAccess.Unreachable:
                    return false; // Walled off from every stall
                default:
                    return context.Inventory.Gold >= 5;
            }
        }
    }
}
