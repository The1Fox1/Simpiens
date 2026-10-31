using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal: Purchase provisions from the outpost stall when hungry and carrying gold.
    /// DesiredState: HasProvisions = true.
    /// At an expedition's muster (Epic 10 G5, D9) a member above the muster's hunger, carrying fewer than the trip's rations,
    /// stocks up first at the stall's price: the order outranks the hearth, and the member eats on the way.
    /// </summary>
    public class PurchaseProvisionsGoal : GoapGoal
    {
        /// <summary>Stocking up for an expedition: above the hearth (up to about 95 before personality), below real hunger.</summary>
        public const float ExpeditionPriority = 100f;

        /// <summary>At an expedition's muster, above its hunger and carrying fewer than the trip's rations.</summary>
        public static bool StocksUpForExpedition(in AgentContext context)
        {
            if (context.Expedition.Stage != Simpiens.Simulation.Guild.ExpeditionStage.Muster || context.Inventory == null) return false;
            var expedition = Considerations.Economy(context).Guild.Expedition;
            return context.Hunger >= expedition.MusterHunger && context.Inventory.CountOf(Simpiens.Simulation.Items.ItemCategory.Food) < expedition.Rations;
        }

        /// <summary>Rations still to buy for an expedition, 0 when not stocking up for one.</summary>
        public static int RationsToStockUp(in AgentContext context) =>
            StocksUpForExpedition(context)
                ? Considerations.Economy(context).Guild.Expedition.Rations - context.Inventory.CountOf(Simpiens.Simulation.Items.ItemCategory.Food)
                : 0;

        public PurchaseProvisionsGoal() : base("PurchaseProvisions", WorldState.Empty.With(StateFact.HasProvisions, true))
        {
        }

        public override float CalculatePriority(AgentContext context)
        {
            // Elevated priority driven by biological hunger
            return StocksUpForExpedition(context) ? SimMath.Max(ExpeditionPriority, context.Hunger * 1.1f) : context.Hunger * 1.1f;
        }

        public override bool IsValid(AgentContext context)
        {
            // Valid if agent is hungry, can afford a ration at the nearest stall (which has stock),
            // and does not already hold food. Unreachable stalls invalidate the goal; without any stall, fall back to the 5g floor.
            bool forExpedition = StocksUpForExpedition(context);
            bool isHungry = context.Hunger >= 50f || forExpedition;
            bool hasFood = !forExpedition && context.Inventory.HasAny(Simpiens.Simulation.Items.ItemCategory.Food);
            if (!isHungry || hasFood || context.Inventory == null) return false;

            switch (ResourceTargeting.GetStallAccess(context, out var stall))
            {
                case ResourceTargeting.StallAccess.Reachable:
                    // Personal reservation price: hunger raises it, greed lowers it (review §3.2: no binary price switch)
                    var food = ResourceTargeting.FoodForSale(stall);
                    return stall.SellStockOf(food) > 0 && context.Inventory.Gold >= stall.SellPriceOf(food)
                        && (forExpedition || stall.SellPriceOf(food) <= Considerations.RationReservationPrice(context));
                case ResourceTargeting.StallAccess.Unreachable:
                    return false; // Walled off from every stall
                default:
                    return context.Inventory.Gold >= 5;
            }
        }
    }
}
