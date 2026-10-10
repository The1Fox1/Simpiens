using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Base action: claims the lowest-id open public bounty of a specific type from the outpost board.
    /// Preconditions: HasActiveBounty = false.
    /// Effects: HasActiveBounty = true plus the type-specific fact supplied by the subclass.
    /// Only valid while an open bounty of that type exists, so the planner never commits to a chain
    /// that cannot be fulfilled (e.g. claiming a Scout bounty for a gathering plan).
    /// </summary>
    public abstract class ClaimBountyAction : GoapAction
    {
        private readonly BountyType _bountyType;

        protected ClaimBountyAction(string name, BountyType bountyType, StateFact typeFact, float cost) : base(name, cost)
        {
            _bountyType = bountyType;

            Preconditions = WorldState.Empty
                .With(StateFact.HasActiveBounty, false);

            Effects = WorldState.Empty
                .With(StateFact.HasActiveBounty, true)
                .With(typeFact, true);
        }

        public BountyType BountyType => _bountyType;

        public override bool IsValid(AgentContext context)
        {
            return BountyQueries.TryGetOpenBounty(context, _bountyType, out _);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            ushort bountyId = BountyQueries.TryGetOpenBounty(context, _bountyType, out var open) ? open.BountyId : (ushort)0;
            return new ClaimBountyIntent(context.AgentId, bountyId);
        }
    }

    /// <summary>
    /// Action: claims an open GatherResource bounty. Effects add HasGatherBounty = true.
    /// </summary>
    public class ClaimGatherBountyAction : ClaimBountyAction
    {
        public ClaimGatherBountyAction(float cost = 1.0f)
            : base("ClaimGatherBounty", BountyType.GatherResource, StateFact.HasGatherBounty, cost)
        {
        }
    }

    /// <summary>
    /// Action: claims an open ScoutSector bounty. Effects add HasScoutBounty = true.
    /// </summary>
    public class ClaimScoutBountyAction : ClaimBountyAction
    {
        public ClaimScoutBountyAction(float cost = 1.0f)
            : base("ClaimScoutBounty", BountyType.ScoutSector, StateFact.HasScoutBounty, cost)
        {
        }
    }
}
