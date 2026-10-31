using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Purchases provisions (Ration) from an outpost trade stall.
    /// Preconditions: AtTradeStall = true.
    /// Effects: HasFoodInInventory = true, HasProvisions = true.
    /// </summary>
    public class BuyProvisionsAction : GoapAction
    {
        public BuyProvisionsAction(float cost = 1.0f) : base("BuyProvisions", cost)
        {
            Preconditions = WorldState.Empty
                .With(StateFact.AtTradeStall, true);

            Effects = WorldState.Empty
                .With(StateFact.HasFoodInInventory, true)
                .With(StateFact.HasProvisions, true);
        }

        /// <summary>
        /// Only plannable when a reachable stall has stock and the agent can afford a ration, so SatiateHunger
        /// falls back to foraging instead of looping on a purchase that will fail.
        /// </summary>
        public override bool IsValid(AgentContext context)
        {
            return ResourceTargeting.CanBuyRation(context);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            EntityId stallId = default;

            if (context.Snapshot != null && context.Snapshot.ActiveStallCount > 0)
            {
                // Nearest stall the agent can actually walk to
                if (ResourceTargeting.TryGetNearestStall(context, out var reachable))
                {
                    stallId = reachable.StallEntityId;
                }
            }
            else if (context.Memory != null && context.Memory.SpatialMemoryMap != null)
            {
                foreach (var kvp in context.Memory.SpatialMemoryMap)
                {
                    if (kvp.Value.Type == EntityType.Stall)
                    {
                        stallId = kvp.Key;
                        break;
                    }
                }
            }

            // The stall re-checks this at execution, so a price raised after planning is refused, not paid
            // The food the reachable stall sells (a ration); without one in sight, the first food there is
            var food = ResourceTargeting.TryGetNearestStall(context, out var seller) ? ResourceTargeting.FoodForSale(seller) : ItemType.None;
            if (food == ItemType.None) food = Simpiens.Simulation.Items.ItemCatalog.InCategory(Simpiens.Simulation.Items.ItemCategory.Food)[0];
            // Stocking up for an expedition (Epic 10 G5): the trip's rations, as many as it can afford, at the stall's price
            int rations = Simpiens.Cognition.Planning.Goals.PurchaseProvisionsGoal.RationsToStockUp(context);
            if (rations > 0 && seller.SellPriceOf(food) > 0)
            {
                int affordable = context.Inventory.Gold / (int)seller.SellPriceOf(food);
                return new TradeIntent(context.AgentId, stallId, TradeActionType.BuyItem, food, quantity: (ushort)SimMath.Max(1, SimMath.Min(rations, affordable)),
                    maxExpectedPricePerUnit: seller.SellPriceOf(food));
            }
            return new TradeIntent(context.AgentId, stallId, TradeActionType.BuyItem, food, quantity: 1,
                maxExpectedPricePerUnit: Considerations.RationReservationPrice(context));
        }
    }
}
