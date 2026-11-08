using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic 10 G3): buys a piece of protection at the stall, which the crawler then wears: hazard gear, or the piece
    /// made against a hazard it couldn't brave alone (Epic 10 Part 2, D51).
    /// Preconditions: AtTradeStall = true. Effects: HasHazardGear = true.
    /// </summary>
    public class BuyHazardGearAction : GoapAction
    {
        public BuyHazardGearAction(float cost = 1.0f) : base("BuyHazardGear", cost)
        {
            Preconditions = WorldState.Empty.With(StateFact.AtTradeStall, true);
            Effects = WorldState.Empty.With(StateFact.HasHazardGear, true);
        }

        /// <summary>Only plannable when a reachable stall has protection the crawler will pay for, can afford and can use.</summary>
        public override bool IsValid(AgentContext context) => ResourceTargeting.TryChooseProtection(context, out _);

        public override AgentIntent CreateIntent(AgentContext context)
        {
            // The stall re-checks the price at execution, so a price raised after planning is refused, not paid
            ResourceTargeting.TryGetNearestStall(context, out var stall);
            ResourceTargeting.TryChooseProtection(context, out var protection);
            return new TradeIntent(context.AgentId, stall.StallEntityId, TradeActionType.BuyItem, protection, quantity: 1,
                maxExpectedPricePerUnit: Considerations.GearReservationPrice(context, protection));
        }
    }
}
