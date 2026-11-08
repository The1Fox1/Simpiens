using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic 10 Part 2, W2.3, D45): buys the medicine the stall sells (a draught), carried in the pack until the
    /// herb reflex drinks it under the treat line. Preconditions: AtTradeStall = true. Effects: HasMedicine = true.
    /// </summary>
    public class BuyDraughtAction : GoapAction
    {
        public BuyDraughtAction(float cost = 1.0f) : base("BuyDraught", cost)
        {
            Preconditions = WorldState.Empty.With(StateFact.AtTradeStall, true);
            Effects = WorldState.Empty.With(StateFact.HasMedicine, true);
        }

        /// <summary>Only plannable when a reachable stall has a draught in stock the crawler will pay for and can afford.</summary>
        public override bool IsValid(AgentContext context) => ResourceTargeting.CanBuyMedicine(context);

        public override AgentIntent CreateIntent(AgentContext context)
        {
            ResourceTargeting.TryGetNearestStall(context, out var stall);
            // The stall re-checks the price at execution, so a price raised after planning is refused, not paid
            var medicine = ResourceTargeting.MedicineForSale(stall);
            return new TradeIntent(context.AgentId, stall.StallEntityId, TradeActionType.BuyItem, medicine, quantity: 1,
                maxExpectedPricePerUnit: Considerations.GearReservationPrice(context, medicine));
        }
    }
}
