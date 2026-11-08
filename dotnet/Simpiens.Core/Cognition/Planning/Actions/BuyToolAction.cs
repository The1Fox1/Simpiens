using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic 10 Part 2, W1.4, D38): buys the tool the stall sells (a pickaxe), which the crawler then wears in its
    /// tool slot. Preconditions: AtTradeStall = true. Effects: HasTool = true.
    /// </summary>
    public class BuyToolAction : GoapAction
    {
        public BuyToolAction(float cost = 1.0f) : base("BuyTool", cost)
        {
            Preconditions = WorldState.Empty.With(StateFact.AtTradeStall, true);
            Effects = WorldState.Empty.With(StateFact.HasTool, true);
        }

        /// <summary>Only plannable when a reachable stall has a tool in stock the crawler will pay for and can afford.</summary>
        public override bool IsValid(AgentContext context) => ResourceTargeting.CanBuyTool(context);

        public override AgentIntent CreateIntent(AgentContext context)
        {
            ResourceTargeting.TryGetNearestStall(context, out var stall);
            // The stall re-checks the price at execution, so a price raised after planning is refused, not paid
            var tool = ResourceTargeting.ToolForSale(stall);
            return new TradeIntent(context.AgentId, stall.StallEntityId, TradeActionType.BuyItem, tool, quantity: 1,
                maxExpectedPricePerUnit: Considerations.GearReservationPrice(context, tool));
        }
    }
}
