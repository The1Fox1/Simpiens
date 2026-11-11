using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic 10 Part 3, K3.3, Q6): buys the weapon that most raises the crawler's attack, which it then wields.
    /// Preconditions: AtTradeStall = true. Effects: HasWeapon = true.
    /// </summary>
    public class BuyWeaponAction : GoapAction
    {
        public BuyWeaponAction(float cost = 1.0f) : base("BuyWeapon", cost)
        {
            Preconditions = WorldState.Empty.With(StateFact.AtTradeStall, true);
            Effects = WorldState.Empty.With(StateFact.HasWeapon, true);
        }

        /// <summary>Only plannable when a reachable stall has a weapon in stock this crawler will pay for and can afford.</summary>
        public override bool IsValid(AgentContext context) => ResourceTargeting.TryChooseWeapon(context, out _);

        public override AgentIntent CreateIntent(AgentContext context)
        {
            ResourceTargeting.TryGetNearestStall(context, out var stall);
            // The stall re-checks the price at execution, so a price raised after planning is refused, not paid
            ResourceTargeting.TryChooseWeapon(context, out var weapon);
            return new TradeIntent(context.AgentId, stall.StallEntityId, TradeActionType.BuyItem, weapon, quantity: 1,
                maxExpectedPricePerUnit: Considerations.GearReservationPrice(context, weapon));
        }
    }
}
