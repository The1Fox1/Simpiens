using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Allocation-free bounty lookups over the shared world snapshot for GOAP actions, goals and the WorldStateBuilder.
    /// Bounties are scanned in ascending BountyId order semantics (lowest id wins) so selection is deterministic.
    /// </summary>
    public static class BountyQueries
    {
        /// <summary>
        /// Finds the bounty currently claimed by this agent, if any.
        /// </summary>
        public static bool TryGetClaimedBounty(in AgentContext context, out BountyContract claimed)
        {
            claimed = default;
            if (context.Snapshot == null || context.Snapshot.ActiveBountyCount == 0) return false;

            var bounties = context.Snapshot.Bounties;
            for (int i = 0; i < bounties.Length; i++)
            {
                ref readonly var b = ref bounties[i];
                if (b.Status == BountyStatus.Claimed && b.ClaimantId == context.AgentId)
                {
                    claimed = b;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Finds the open, unexpired bounty of the given type with the lowest id that this agent can fulfil (reachable).
        /// BountyType.None matches any type.
        /// </summary>
        public static bool TryGetOpenBounty(in AgentContext context, BountyType type, out BountyContract open)
        {
            open = default;
            if (context.Snapshot == null || context.Snapshot.ActiveBountyCount == 0) return false;

            bool found = false;
            var bounties = context.Snapshot.Bounties;
            for (int i = 0; i < bounties.Length; i++)
            {
                ref readonly var b = ref bounties[i];
                if (b.Status != BountyStatus.Open || !b.IsActive(context.CurrentTick)) continue;
                if (type != BountyType.None && b.Type != type) continue;
                if (!IsFulfillableFrom(context, b)) continue;

                if (!found || b.BountyId < open.BountyId)
                {
                    open = b;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// A bounty is only worth claiming if the agent can walk to its target location (when set) and to the
        /// stall it must be redeemed at. Walled-in agents therefore never claim contracts they cannot complete.
        /// </summary>
        public static bool IsFulfillableFrom(in AgentContext context, in BountyContract bounty)
        {
            if (bounty.TargetLocation != default && !Reachability.IsReachable(context, bounty.TargetLocation)) return false;

            if (context.Snapshot != null && bounty.CreatorStallId != default && context.Snapshot.TryGetStall(bounty.CreatorStallId, out var creator))
            {
                return Reachability.IsReachable(context, creator.Position);
            }

            return ResourceTargeting.GetStallAccess(context, out _) != ResourceTargeting.StallAccess.Unreachable;
        }

        /// <summary>
        /// True when the agent's inventory satisfies the claimed Gather bounty's item and quantity.
        /// </summary>
        public static bool HasBountyItems(in AgentContext context, in BountyContract bounty)
        {
            if (bounty.Type != BountyType.GatherResource || context.Inventory == null) return false;
            if (bounty.TargetItemType == ItemType.None) return false;
            return context.Inventory.HasItem(bounty.TargetItemType, bounty.TargetQuantity);
        }
    }
}
