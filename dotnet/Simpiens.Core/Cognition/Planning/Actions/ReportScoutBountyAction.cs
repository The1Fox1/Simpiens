using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Reports a completed sector scout at the outpost trade stall to fulfil a Scout bounty.
    /// Preconditions: HasScoutBounty = true, HasScoutReport = true, AtTradeStall = true.
    /// Effects: BountyDelivered = true, HasActiveBounty = false, HasScoutBounty = false.
    /// Emits a DeliverBountyIntent; the delivery handler branches on the bounty type.
    /// </summary>
    public class ReportScoutBountyAction : GoapAction
    {
        public ReportScoutBountyAction(float cost = 1.0f) : base("ReportScoutBounty", cost)
        {
            Preconditions = WorldState.Empty
                .With(StateFact.HasScoutBounty, true)
                .With(StateFact.HasScoutReport, true)
                .With(StateFact.AtTradeStall, true);

            Effects = WorldState.Empty
                .With(StateFact.BountyDelivered, true)
                .With(StateFact.HasActiveBounty, false)
                .With(StateFact.HasScoutBounty, false);
        }

        public override bool IsValid(AgentContext context)
        {
            return BountyQueries.TryGetClaimedBounty(context, out var claimed)
                ? claimed.Type == BountyType.ScoutSector
                : BountyQueries.TryGetOpenBounty(context, BountyType.ScoutSector, out _);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            ushort bountyId = BountyQueries.TryGetClaimedBounty(context, out var claimed) ? claimed.BountyId : (ushort)0;
            var stallId = ResourceTargeting.TryGetNearestStall(context, out var stall) ? stall.StallEntityId : default;
            return new DeliverBountyIntent(context.AgentId, bountyId, stallId);
        }
    }
}
