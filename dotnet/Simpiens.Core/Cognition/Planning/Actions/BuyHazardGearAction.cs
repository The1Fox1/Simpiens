using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic 10 G3): buys a piece of hazard gear at the stall, which the crawler then wears.
    /// Preconditions: AtTradeStall = true. Effects: HasHazardGear = true.
    /// </summary>
    public class BuyHazardGearAction : GoapAction
    {
        public BuyHazardGearAction(float cost = 1.0f) : base("BuyHazardGear", cost)
        {
            Preconditions = WorldState.Empty.With(StateFact.AtTradeStall, true);
            Effects = WorldState.Empty.With(StateFact.HasHazardGear, true);
        }

        /// <summary>Only plannable when a reachable stall sells gear the crawler will pay for and can afford.</summary>
        public override bool IsValid(AgentContext context) => ResourceTargeting.CanBuyGear(context);

        public override AgentIntent CreateIntent(AgentContext context)
        {
            ResourceTargeting.TryGetNearestStall(context, out var stall);
            // The stall re-checks the price at execution, so a price raised after planning is refused, not paid
            return new TradeIntent(context.AgentId, stall.StallEntityId, TradeActionType.BuyItem, ItemType.HazardGear, quantity: 1,
                maxExpectedPricePerUnit: Considerations.GearReservationPrice(context));
        }
    }
}
