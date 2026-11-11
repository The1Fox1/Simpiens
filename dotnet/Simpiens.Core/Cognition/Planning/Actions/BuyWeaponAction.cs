using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic 10 Part 3, K3.3, Q6): buys the piece that most raises the crawler's fighting outfit, which it then wears:
    /// a better weapon, or armour for a bare slot once armed (K4.4, D26; <see cref="ResourceTargeting.TryChooseArms"/>).
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
        public override bool IsValid(AgentContext context) => ResourceTargeting.TryChooseArms(context, out _);

        public override AgentIntent CreateIntent(AgentContext context)
        {
            ResourceTargeting.TryGetNearestStall(context, out var stall);
            // The stall re-checks the price at execution, so a price raised after planning is refused, not paid
            ResourceTargeting.TryChooseArms(context, out var arms);
            return new TradeIntent(context.AgentId, stall.StallEntityId, TradeActionType.BuyItem, arms, quantity: 1,
                maxExpectedPricePerUnit: Considerations.GearReservationPrice(context, arms));
        }
    }
}
