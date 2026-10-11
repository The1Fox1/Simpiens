using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal: Liquidate raw salvage at the outpost trade stall to earn gold currency.
    /// DesiredState: HasSalvageInInventory = false.
    /// </summary>
    public class LiquidateSalvageGoal : GoapGoal
    {
        public LiquidateSalvageGoal() : base("LiquidateSalvage", WorldState.Empty.With(StateFact.HasSalvageInInventory, false))
        {
        }

        public override float CalculatePriority(AgentContext context)
        {
            // Base priority 30, scaled higher if inventory is constrained
            int occupiedSlots = 0;
            if (context.Inventory != null)
            {
                if (context.Inventory.HasItem(ItemType.IronOre)) occupiedSlots++;
                if (context.Inventory.HasItem(ItemType.MedicinalHerb)) occupiedSlots++;
            }
            return (30f + (occupiedSlots * 15f)) * Considerations.TraitFactor(context.Personality.Greed, Considerations.GreedOnIncome);
        }

        public override bool IsValid(AgentContext context)
        {
            if (context.Inventory == null) return false;
            if (!context.Inventory.HasItem(ItemType.IronOre) && !context.Inventory.HasItem(ItemType.MedicinalHerb)) return false;

            switch (ResourceTargeting.GetStallAccess(context, out var stall))
            {
                case ResourceTargeting.StallAccess.Reachable:
                    // Only worth the trip if the treasury can pay for something actually held
                    return ResourceTargeting.AffordableHeldSalvage(context, stall) != ItemType.None;
                case ResourceTargeting.StallAccess.Unreachable:
                    return false;
                default:
                    return true;
            }
        }
    }
}
