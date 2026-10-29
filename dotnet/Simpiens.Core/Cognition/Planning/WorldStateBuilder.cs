using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Cognition.Inventory;
using Simpiens.Simulation.Spatial;
namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Converts agent context and localized spatial memory into a zero-allocation
    /// 16-byte WorldState bitmask for GOAP planning and goal evaluation.
    /// </summary>
    public static class WorldStateBuilder
    {
        public const float HungerThreshold = 50f;
        public const float RestThreshold = 80f;
        public const float ReachResourceDistance = 1.5f;
        public const float PeerProximityDistance = 2.5f;
        public const float AnchorProximityDistance = 3.0f;
        public const float ThreatFrustrationThreshold = 60f;

        /// <summary>
        /// Builds the current WorldState bitmask from the agent context and inventory.
        /// </summary>
        public static WorldState BuildWorldState(in AgentContext context, Simpiens.Cognition.Inventory.IAgentInventory inventory)
        {
            var inv = inventory ?? context.Inventory;
            // Groups, not items (the item catalog): food, a tool, salvage to sell, and worn protection (Epic 10 G3)
            bool hasFood = inv.HasAny(ItemCategory.Food);
            bool hasTool = inv.HasAny(ItemCategory.Tool);
            bool hasSalvage = inv.HasAny(ItemCategory.Resource) && !ResourceTargeting.StallRefusesHeldSalvage(context);
            bool hasHazardGear = inv.IsWearing(ItemCategory.Protection);

            var state = BuildWorldState(context, hasFood, hasTool);
            state = state.With(StateFact.HasProvisions, hasFood);
            state = state.With(StateFact.HasSalvageInInventory, hasSalvage);
            state = state.With(StateFact.HasHazardGear, hasHazardGear);
            return state;
        }

        /// <summary>
        /// Builds the current WorldState bitmask from the agent context.
        /// </summary>
        public static WorldState BuildWorldState(in AgentContext context, bool hasFoodInInventory = false, bool hasTool = false)
        {
            var state = WorldState.Empty;

            // 1. Biological Drives
            bool isHungry = context.Hunger > HungerThreshold;
            state = state.With(StateFact.IsHungry, isHungry);

            bool isResting = context.Energy >= RestThreshold;
            state = state.With(StateFact.IsResting, isResting);

            // 2. Inventory / Equipment
            if (context.Inventory != null)
            {
                if (!hasFoodInInventory && context.Inventory.HasAny(ItemCategory.Food))
                {
                    hasFoodInInventory = true;
                }
                if (!hasTool && context.Inventory.HasAny(ItemCategory.Tool))
                {
                    hasTool = true;
                }
                bool hasSalvage = context.Inventory.HasAny(ItemCategory.Resource) && !ResourceTargeting.StallRefusesHeldSalvage(context);
                bool hasHazardGear = context.Inventory.IsWearing(ItemCategory.Protection);
                state = state.With(StateFact.HasSalvageInInventory, hasSalvage);
                state = state.With(StateFact.HasHazardGear, hasHazardGear);
            }

            state = state.With(StateFact.HasFoodInInventory, hasFoodInInventory);
            state = state.With(StateFact.HasProvisions, hasFoodInInventory);
            state = state.With(StateFact.HasTool, hasTool);

            // 3. Psychological / Threat
            bool isThreatened = context.Frustration > ThreatFrustrationThreshold;
            state = state.With(StateFact.IsThreatened, isThreatened);

            // 4. Memory & Spatial Queries
            if (context.Memory != null)
            {
                bool knowsResource = false;
                bool atResource = false;
                bool knowsSalvage = false;
                bool isNearPeer = false;
                bool knowsAnchor = false;
                bool atAnchor = false;

                float reachDistSqr = ReachResourceDistance * ReachResourceDistance;
                float peerDistSqr = PeerProximityDistance * PeerProximityDistance;
                float anchorDistSqr = AnchorProximityDistance * AnchorProximityDistance;

                foreach (var kvp in context.Memory.SpatialMemoryMap)
                {
                    var record = kvp.Value;
                    if (record.Type == EntityType.Resource && record.ResourceKind.IsSalvage())
                    {
                        // Salvage nodes never satisfy the food facts below
                        if (!context.Memory.IsBlacklisted(record.EntityId, context.CurrentTick))
                        {
                            knowsSalvage = true;
                        }
                    }
                    else if (record.Type == EntityType.Resource && !context.Memory.IsBlacklisted(record.EntityId, context.CurrentTick))
                    {
                        knowsResource = true;
                        Vec2 loc = record.LastKnownLocation;
                        float distSqr = (loc - context.Position).SqrMagnitude;
                        if (distSqr <= reachDistSqr)
                        {
                            atResource = true;
                        }
                    }
                    else if (record.Type == EntityType.Pawn && record.EntityId != context.AgentId)
                    {
                        Vec2 loc = record.LastKnownLocation;
                        float distSqr = (loc - context.Position).SqrMagnitude;
                        if (distSqr <= peerDistSqr)
                        {
                            isNearPeer = true;
                        }
                    }
                    else if (record.Type == EntityType.Anchor && !context.Memory.IsBlacklisted(record.EntityId, context.CurrentTick))
                    {
                        knowsAnchor = true;
                        Vec2 loc = record.LastKnownLocation;
                        float distSqr = (loc - context.Position).SqrMagnitude;
                        if (distSqr <= anchorDistSqr)
                        {
                            atAnchor = true;
                        }
                    }
                }

                state = state.With(StateFact.KnowsResourceLocation, knowsResource);
                state = state.With(StateFact.AtResourceLocation, atResource);
                state = state.With(StateFact.KnowsSalvageLocation, knowsSalvage);
                state = state.With(StateFact.IsNearPeer, isNearPeer);
                state = state.With(StateFact.KnowsAnchorLocation, knowsAnchor);
                state = state.With(StateFact.AtAnchorLocation, atAnchor);

                // Rest anchor: a remembered bunk that is free right now
                bool knowsBunk = BunkTargeting.TryGetNearestFreeBunk(context, out _, out var bunkPosition);
                state = state.With(StateFact.KnowsBunkLocation, knowsBunk);
                state = state.With(StateFact.AtBunk, knowsBunk && (bunkPosition - context.Position).SqrMagnitude <= BunkTargeting.ReachDistance * BunkTargeting.ReachDistance);

                // 5. Relational / Social Matrix
                bool hasWarmCompanion = false;
                bool hasTrustedPeer = false;
                bool hasFearedThreat = false;
                bool hasRespectedLeader = false;

                if (context.Memory.AffinityMap != null)
                {
                    foreach (var kvp in context.Memory.AffinityMap)
                    {
                        var affinity = context.Memory.GetAffinity(kvp.Key, context.CurrentTick);
                        if (affinity.Warmth >= 30) hasWarmCompanion = true;
                        if (affinity.Trust >= 30) hasTrustedPeer = true;
                        if (affinity.Fear >= 50) hasFearedThreat = true;
                        if (affinity.Respect >= 30) hasRespectedLeader = true;
                    }
                }

                state = state.With(StateFact.HasWarmCompanion, hasWarmCompanion);
                state = state.With(StateFact.HasTrustedPeer, hasTrustedPeer);
                state = state.With(StateFact.HasFearedThreat, hasFearedThreat);
                state = state.With(StateFact.HasRespectedLeader, hasRespectedLeader);
            }

            // 6. Sociopolitical & Group Directives (Epic 8)
            bool hasActiveDirective = false;
            bool atDirectiveLocation = false;
            bool isGroupLeader = false;

            if (context.Membership.HasGroup)
            {
                isGroupLeader = context.Membership.Role == RoleRank.Leader;

                if (context.Snapshot != null && context.Snapshot.TryGetGroup(context.Membership.GroupId, out var groupData))
                {
                    if (groupData.ActiveDirective.IsActive(context.CurrentTick))
                    {
                        hasActiveDirective = true;
                        float radius = groupData.ActiveDirective.TargetRadius > 0.1f ? groupData.ActiveDirective.TargetRadius : 2.5f;
                        float distSqr = (groupData.ActiveDirective.TargetLocation - context.Position).SqrMagnitude;
                        if (distSqr <= radius * radius)
                        {
                            atDirectiveLocation = true;
                        }
                    }
                }
            }

            state = state.With(StateFact.HasActiveDirective, hasActiveDirective);
            state = state.With(StateFact.AtDirectiveLocation, atDirectiveLocation);
            state = state.With(StateFact.IsGroupLeader, isGroupLeader);

            // 7. Outpost Economy & Trade Stall State (Epic 9 Slice 2)
            bool atTradeStall = false;
            if (context.Snapshot != null && context.Snapshot.ActiveStallCount > 0)
            {
                int stallCount = context.Snapshot.ActiveStallCount;
                for (int i = 0; i < stallCount; i++)
                {
                    var stall = context.Snapshot.StallBuffer[i];
                    float r = stall.InteractionRadius > 0.1f ? stall.InteractionRadius : 2.5f;
                    if ((stall.Position - context.Position).SqrMagnitude <= r * r)
                    {
                        atTradeStall = true;
                        break;
                    }
                }
            }
            else if (context.Memory != null && context.Memory.SpatialMemoryMap != null)
            {
                foreach (var kvp in context.Memory.SpatialMemoryMap)
                {
                    if (kvp.Value.Type == EntityType.Stall)
                    {
                        if (((Vec2)kvp.Value.LastKnownLocation - context.Position).SqrMagnitude <= 2.5f * 2.5f)
                        {
                            atTradeStall = true;
                            break;
                        }
                    }
                }
            }

            state = state.With(StateFact.AtTradeStall, atTradeStall);

            // 8. Public Bounties & Contracts (Epic 9 Slice 3, §3.1 typed bounties)
            bool hasActiveBounty = BountyQueries.TryGetClaimedBounty(context, out var claimed);
            bool hasGatherBounty = hasActiveBounty && claimed.Type == BountyType.GatherResource;
            bool hasScoutBounty = hasActiveBounty && claimed.Type == BountyType.ScoutSector;
            bool hasBountyItem = hasGatherBounty && BountyQueries.HasBountyItems(context, claimed);
            bool hasScoutReport = hasScoutBounty && context.ScoutReportBountyId == claimed.BountyId;

            state = state.With(StateFact.HasActiveBounty, hasActiveBounty);
            state = state.With(StateFact.HasGatherBounty, hasGatherBounty);
            state = state.With(StateFact.HasScoutBounty, hasScoutBounty);
            state = state.With(StateFact.HasBountyItem, hasBountyItem);
            state = state.With(StateFact.HasScoutReport, hasScoutReport);
            state = state.With(StateFact.HasSoldSalvage, false);

            // A claimed Gather bounty narrows salvage knowledge to its target item, so an agent that only
            // knows herb patches still plans a search when the contract asks for iron ore.
            if (hasGatherBounty && claimed.TargetItemType.IsSalvage())
            {
                state = state.With(StateFact.KnowsSalvageLocation, ResourceTargeting.KnowsSalvage(context, claimed.TargetItemType));
            }

            return state;
        }
    }
}
