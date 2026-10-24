using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Delivers the claimed Gather bounty's items to the outpost trade stall.
    /// Preconditions: HasGatherBounty = true, HasBountyItem = true, AtTradeStall = true.
    /// Effects: BountyDelivered = true, HasActiveBounty = false, HasGatherBounty = false.
    /// </summary>
    public class DeliverBountyAction : GoapAction
    {
        public DeliverBountyAction(float cost = 1.0f) : base("DeliverBounty", cost)
        {
            Preconditions = WorldState.Empty
                .With(StateFact.HasGatherBounty, true)
                .With(StateFact.HasBountyItem, true)
                .With(StateFact.AtTradeStall, true);

            Effects = WorldState.Empty
                .With(StateFact.BountyDelivered, true)
                .With(StateFact.HasActiveBounty, false)
                .With(StateFact.HasGatherBounty, false);
        }

        public override bool IsValid(AgentContext context)
        {
            // Evaluated at plan time (bounty may not be claimed yet) and at execution time
            return BountyQueries.TryGetClaimedBounty(context, out var claimed)
                ? claimed.Type == BountyType.GatherResource
                : BountyQueries.TryGetOpenBounty(context, BountyType.GatherResource, out _);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            ushort bountyId = BountyQueries.TryGetClaimedBounty(context, out var claimed) ? claimed.BountyId : (ushort)0;
            var stallId = ResourceTargeting.TryGetNearestStall(context, out var stall) ? stall.StallEntityId : default;
            return new DeliverBountyIntent(context.AgentId, bountyId, stallId);
        }
    }
}
