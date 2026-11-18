using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic 11, S3.3, D11): buys the manual <see cref="ResourceTargeting.TryChooseManual"/> picks, carried in the pack
    /// to read at rest. Preconditions: AtTradeStall = true. Effects: HasManual = true.
    /// </summary>
    public class BuyManualAction : GoapAction
    {
        public BuyManualAction(float cost = 1.0f) : base("BuyManual", cost)
        {
            Preconditions = WorldState.Empty.With(StateFact.AtTradeStall, true);
            Effects = WorldState.Empty.With(StateFact.HasManual, true);
        }

        public override bool IsValid(AgentContext context) => ResourceTargeting.TryChooseManual(context, out _);

        public override AgentIntent CreateIntent(AgentContext context)
        {
            ResourceTargeting.TryGetNearestStall(context, out var stall);
            ResourceTargeting.TryChooseManual(context, out var manual);
            return new TradeIntent(context.AgentId, stall.StallEntityId, TradeActionType.BuyItem, manual, quantity: 1,
                maxExpectedPricePerUnit: Considerations.ManualReservationPrice(context, manual));
        }
    }
}
