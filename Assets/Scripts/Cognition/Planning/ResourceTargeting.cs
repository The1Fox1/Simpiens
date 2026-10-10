using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Shared, allocation-free resource selection over an agent's spatial memory.
    /// All selection is deterministic: equal scores break ties by entity GUID, never by dictionary order.
    /// Used by the Tier-1 survival reflex and by the GOAP harvest/gather/search actions.
    /// </summary>
    public static class ResourceTargeting
    {
        /// <summary>A path must end within this distance of the node to count as reachable.</summary>
        public const float HarvestReach = 1.0f;

        /// <summary>Default search radius for food when no tighter engagement limit applies.</summary>
        public const float DefaultFoodSearchDistance = 30f;

        /// <summary>
        /// Selects the nearest remembered, non-blacklisted food node within maxDistance.
        /// </summary>
        public static bool TrySelectFood(in AgentContext context, float maxDistance, out SpatialMemoryRecord best)
        {
            best = default;
            if (context.Memory == null || context.Memory.SpatialMemoryMap == null) return false;

            bool found = false;
            float bestDistSqr = float.MaxValue;
            float maxDistSqr = maxDistance * maxDistance;

            foreach (var kvp in context.Memory.SpatialMemoryMap)
            {
                var record = kvp.Value;
                if (record.Type != EntityType.Resource || !record.ResourceKind.IsFoodYield()) continue;
                if (context.Memory.IsBlacklisted(record.EntityId, context.CurrentTick)) continue;

                float distSqr = ((Vector2)record.LastKnownLocation - context.Position).sqrMagnitude;
                if (distSqr > maxDistSqr) continue;

                if (!found || distSqr < bestDistSqr || (distSqr == bestDistSqr && record.EntityId.CompareTo(best.EntityId) < 0))
                {
                    found = true;
                    bestDistSqr = distSqr;
                    best = record;
                }
            }

            return found;
        }

        /// <summary>
        /// Returns true if the agent remembers any non-blacklisted salvage node (optionally of a specific type).
        /// </summary>
        public static bool KnowsSalvage(in AgentContext context, ItemType requiredType = ItemType.None)
        {
            if (context.Memory == null || context.Memory.SpatialMemoryMap == null) return false;

            foreach (var kvp in context.Memory.SpatialMemoryMap)
            {
                var record = kvp.Value;
                if (record.Type != EntityType.Resource || !record.ResourceKind.IsSalvage()) continue;
                if (requiredType != ItemType.None && record.ResourceKind != requiredType) continue;
                if (context.Memory.IsBlacklisted(record.EntityId, context.CurrentTick)) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Selects the best remembered salvage node.
        /// With a required type (e.g. a Gather bounty's TargetItemType) only that type is considered and the nearest wins.
        /// Otherwise nodes are scored by the nearest stall's buy price divided by distance.
        /// </summary>
        public static bool TrySelectSalvage(in AgentContext context, ItemType requiredType, out SpatialMemoryRecord best)
        {
            best = default;
            if (context.Memory == null || context.Memory.SpatialMemoryMap == null) return false;

            ushort orePrice = 1;
            ushort herbPrice = 1;
            uint treasury = uint.MaxValue;
            if (context.Snapshot != null && TryGetNearestStall(context, out var stall))
            {
                orePrice = stall.OreBuyPrice > 0 ? stall.OreBuyPrice : (ushort)1;
                herbPrice = stall.HerbBuyPrice > 0 ? stall.HerbBuyPrice : (ushort)1;
                treasury = stall.TreasuryGold;
            }

            bool found = false;
            float bestScore = float.MinValue;

            foreach (var kvp in context.Memory.SpatialMemoryMap)
            {
                var record = kvp.Value;
                if (record.Type != EntityType.Resource || !record.ResourceKind.IsSalvage()) continue;
                if (requiredType != ItemType.None && record.ResourceKind != requiredType) continue;
                if (context.Memory.IsBlacklisted(record.EntityId, context.CurrentTick)) continue;

                float dist = Mathf.Max(1f, Vector2.Distance(record.LastKnownLocation, context.Position));
                float score;
                if (requiredType != ItemType.None)
                {
                    score = -dist;
                }
                else
                {
                    float price = record.ResourceKind == ItemType.IronOre ? orePrice : herbPrice;
                    // Speculative gathering only targets salvage the stall can currently pay for
                    if (price > treasury) continue;
                    score = price / dist;
                }

                if (!found || score > bestScore || (score == bestScore && record.EntityId.CompareTo(best.EntityId) < 0))
                {
                    found = true;
                    bestScore = score;
                    best = record;
                }
            }

            return found;
        }

        /// <summary>
        /// Paths to the target node and wraps it in a HarvestResourceIntent.
        /// When the node is unreachable the intent carries no path, so the harvest handler reports PathBlocked
        /// and the consequence service blacklists the node — closing the feedback loop on the main thread.
        /// </summary>
        public static async UniTask<AgentIntent> BuildHarvestIntentAsync(AgentContext context, IAsyncPathfinder pathfinder, SpatialMemoryRecord target, int targetUnits = 1)
        {
            if (pathfinder == null)
            {
                return new HarvestResourceIntent(context.AgentId, target.EntityId, default, targetUnits: targetUnits);
            }

            Vector2 targetPos = target.LastKnownLocation;
            var request = new PathRequest
            {
                StartPosition = context.Position,
                TargetPosition = targetPos,
                RequesterId = context.AgentId
            };

            var pathResponse = await pathfinder.CalculatePathAsync(request, context.Snapshot);
            if (IsReachable(pathResponse, targetPos))
            {
                return new HarvestResourceIntent(context.AgentId, target.EntityId, pathResponse, targetUnits: targetUnits);
            }

            pathResponse?.ReturnToPool();
            return new HarvestResourceIntent(context.AgentId, target.EntityId, null, targetUnits: targetUnits);
        }

        /// <summary>
        /// Intent used when an action finds no valid target at execution time. The harvest handler reports
        /// TargetMissing, which aborts the active plan so the agent replans (e.g. falls back to searching).
        /// </summary>
        public static AgentIntent MissingTargetIntent(in AgentContext context)
        {
            return new HarvestResourceIntent(context.AgentId, default, null);
        }

        /// <summary>
        /// Deterministic exploration waypoint: a point 5–9m from the agent whose bearing is derived from the
        /// agent id and a 50-tick time bucket (no RNG), so the same inputs always produce the same waypoint.
        /// </summary>
        public static Vector2 PickExplorationPoint(in AgentContext context, float minDistance = 5f, float maxDistance = 9f)
        {
            uint h = unchecked((uint)context.AgentId.GetHashCode() * 2654435761u ^ (context.CurrentTick / 50u) * 2246822519u);
            h ^= h >> 15;
            h = unchecked(h * 2654435761u);
            h ^= h >> 13;

            float angle = (h % 3600u) / 3600f * Mathf.PI * 2f;
            float distance = minDistance + ((h >> 12) % 1000u) / 1000f * (maxDistance - minDistance);
            return context.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }

        /// <summary>
        /// Paths toward a destination and wraps it in a WanderIntent; falls back to a short Idle when unroutable.
        /// </summary>
        public static async UniTask<AgentIntent> BuildTravelIntentAsync(AgentContext context, IAsyncPathfinder pathfinder, Vector2 destination)
        {
            if (pathfinder == null)
            {
                return new WanderIntent(context.AgentId, new PathResponse());
            }

            var request = new PathRequest
            {
                StartPosition = context.Position,
                TargetPosition = destination,
                RequesterId = context.AgentId
            };

            var pathResponse = await pathfinder.CalculatePathAsync(request, context.Snapshot);
            if (pathResponse != null && pathResponse.IsValid)
            {
                return new WanderIntent(context.AgentId, pathResponse);
            }

            pathResponse?.ReturnToPool();
            return new IdleIntent(context.AgentId, duration: 1.0f);
        }

        /// <summary>
        /// A path is reachable when it is valid and its final waypoint lies within harvest reach of the target.
        /// </summary>
        public static bool IsReachable(PathResponse path, Vector2 targetPos)
        {
            if (path == null || !path.IsValid || path.Length <= 0) return false;
            Vector2 finalWaypoint = path.Waypoints[path.Length - 1];
            return Vector2.Distance(finalWaypoint, targetPos) <= HarvestReach;
        }

        public enum StallAccess
        {
            /// <summary>No stalls in the snapshot (e.g. headless validators).</summary>
            None,
            /// <summary>Stalls exist but none can be walked to from here.</summary>
            Unreachable,
            Reachable
        }

        /// <summary>
        /// Classifies stall access and returns the nearest stall the agent can actually walk to.
        /// </summary>
        public static StallAccess GetStallAccess(in AgentContext context, out TradeStallData nearest)
        {
            nearest = default;
            if (context.Snapshot == null || context.Snapshot.ActiveStallCount == 0) return StallAccess.None;

            bool found = false;
            float bestDistSqr = float.MaxValue;
            int count = context.Snapshot.ActiveStallCount;
            for (int i = 0; i < count; i++)
            {
                var stall = context.Snapshot.StallBuffer[i];
                if (!Reachability.IsReachable(context, stall.Position)) continue;

                float distSqr = (stall.Position - context.Position).sqrMagnitude;
                if (!found || distSqr < bestDistSqr || (distSqr == bestDistSqr && stall.StallEntityId.CompareTo(nearest.StallEntityId) < 0))
                {
                    found = true;
                    bestDistSqr = distSqr;
                    nearest = stall;
                }
            }
            return found ? StallAccess.Reachable : StallAccess.Unreachable;
        }

        /// <summary>
        /// True when the nearest reachable stall's treasury can pay for at least one unit of salvage, so a sale won't
        /// fail on arrival. Without any visible stall this returns true (headless contexts); behind walls, false.
        /// </summary>
        public static bool NearestStallCanBuySalvage(in AgentContext context)
        {
            var access = GetStallAccess(context, out var stall);
            if (access == StallAccess.None) return true;
            if (access == StallAccess.Unreachable) return false;
            uint cheapest = stall.OreBuyPrice < stall.HerbBuyPrice ? stall.OreBuyPrice : stall.HerbBuyPrice;
            return stall.TreasuryGold >= cheapest;
        }

        /// <summary>
        /// The held salvage type the stall can pay for (highest price first), or None if nothing held is affordable.
        /// </summary>
        public static ItemType AffordableHeldSalvage(in AgentContext context, in TradeStallData stall)
        {
            if (context.Inventory == null) return ItemType.None;
            bool herb = context.Inventory.HasItem(ItemType.MedicinalHerb) && stall.TreasuryGold >= stall.HerbBuyPrice;
            bool ore = context.Inventory.HasItem(ItemType.IronOre) && stall.TreasuryGold >= stall.OreBuyPrice;
            if (herb && ore) return stall.HerbBuyPrice >= stall.OreBuyPrice ? ItemType.MedicinalHerb : ItemType.IronOre;
            if (herb) return ItemType.MedicinalHerb;
            if (ore) return ItemType.IronOre;
            return ItemType.None;
        }

        /// <summary>
        /// True when the agent can buy a ration right now: reachable stall with stock and an affordable price.
        /// Without any visible stall (headless contexts) this returns true; behind walls, false.
        /// </summary>
        public static bool CanBuyRation(in AgentContext context)
        {
            var access = GetStallAccess(context, out var stall);
            if (access == StallAccess.None) return true;
            if (access == StallAccess.Unreachable) return false;
            int gold = context.Inventory != null ? context.Inventory.Gold : 0;
            return stall.RationStock > 0 && gold >= stall.RationSellPrice;
        }

        /// <summary>Nearest stall the agent can walk to.</summary>
        public static bool TryGetNearestStall(in AgentContext context, out TradeStallData nearest)
        {
            return GetStallAccess(context, out nearest) == StallAccess.Reachable;
        }
    }
}
