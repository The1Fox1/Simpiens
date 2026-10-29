using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Shared, allocation-free resource selection over an agent's spatial memory.
    /// All selection is deterministic: equal scores break ties by entity EntityId, never by dictionary order.
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

                float distSqr = ((Vec2)record.LastKnownLocation - context.Position).SqrMagnitude;
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
                // As TrySelectSalvage: a hazard sector's nodes count only for a crawler who would go in (Epic 10 G2)
                if (!Vitality.WouldVenture(context) && Vitality.HazardAt(context.Snapshot?.Map, record.LastKnownLocation).IsHazardous) continue;
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

            uint treasury = uint.MaxValue;
            TradeStallData stall = default;
            bool stallKnown = context.Snapshot != null && TryGetNearestStall(context, out stall);
            var map = context.Snapshot?.Map;
            bool venture = Vitality.WouldVenture(context);
            var economy = Considerations.Economy(context);
            if (stallKnown)
            {
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
                // Danger is for the bold and healthy (Epic 10 G2): a hazard sector's nodes are left to them
                if (!venture && Vitality.HazardAt(map, record.LastKnownLocation).IsHazardous) continue;

                float dist = SimMath.Max(1f, Vec2.Distance(record.LastKnownLocation, context.Position));
                float score;
                if (requiredType != ItemType.None)
                {
                    score = -dist;
                }
                else
                {
                    // What the stall pays for this kind, 1 when it pays nothing or isn't known
                    ushort stallPrice = stallKnown ? stall.BuyPriceOf(record.ResourceKind) : (ushort)0;
                    float price = stallPrice > 0 ? stallPrice : (ushort)1;
                    // Speculative gathering only targets salvage the stall can currently pay for, and has room for: with
                    // the ore shelf full, a crawler goes for herbs (Epic 10 G1; drifters sell too)
                    if (price > treasury) continue;
                    if (stallKnown && !economy.StallHasRoom(stall, record.ResourceKind)) continue;
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
        public static async ValueTask<AgentIntent> BuildHarvestIntentAsync(AgentContext context, IAsyncPathfinder pathfinder, SpatialMemoryRecord target, int targetUnits = 1)
        {
            if (pathfinder == null)
            {
                return new HarvestResourceIntent(context.AgentId, target.EntityId, default, targetUnits: targetUnits);
            }

            Vec2 targetPos = target.LastKnownLocation;
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
        /// Deterministic exploration waypoint (no RNG; derived from the agent id and a time bucket). With a map, a
        /// point in another sector (TryPickSectorWaypoint); without one, a point 5–9m away on a hashed bearing.
        /// </summary>
        public static Vec2 PickExplorationPoint(in AgentContext context, float minDistance = 5f, float maxDistance = 9f)
        {
            // On a map with rooms, head for another sector: a random point a few units away mostly lands in a wall
            // or the same room, so finding a door would be luck (Epic MAP Phase 3)
            var map = context.Snapshot?.Map;
            if (map != null && TryPickSectorWaypoint(context, map, out var waypoint))
            {
                return waypoint;
            }

            uint h = ExplorationHash(context, 50u);

            float angle = (h % 3600u) / 3600f * SimMath.PI * 2f;
            float distance = minDistance + ((h >> 12) % 1000u) / 1000f * (maxDistance - minDistance);
            return context.Position + new Vec2(SimMath.Cos(angle), SimMath.Sin(angle)) * distance;
        }

        /// <summary>
        /// The nearest food plot of the map in a charted sector this agent has stood in (it saw the plots there). A starving
        /// crawler that knows no food it can reach right now goes back to one and waits for it to regrow, instead of
        /// exploring away from the only free food it knows (review §3.3 D6; the Epic MAP close-out starvation). Map data
        /// only: the plot may be dormant, which is the point. False when the agent has seen no food sector.
        /// </summary>
        public static bool TryPickFoodPlotWaypoint(in AgentContext context, Simpiens.Simulation.World.MapDefinition map, out Vec2 plot)
        {
            plot = default;
            if (map == null || context.Memory == null) return false;
            var snapshot = context.Snapshot;
            var nodes = map.ResourceNodes;
            float bestSqr = float.MaxValue;
            bool found = false;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!node.Item.IsFoodYield()) continue;
                byte sector = map.FindSector(node.Position).Id;
                if (snapshot != null && !snapshot.IsSectorCharted(sector)) continue;
                if (context.Memory.LastSectorVisit(sector) == 0u) continue;
                float sqr = (node.Position - context.Position).SqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    plot = node.Position;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Ticks per exploration choice: long enough to cross the map, so a choice isn't abandoned midway.</summary>
        public const uint SectorChoiceTicks = 200;

        /// <summary>
        /// Picks a charted sector other than the one the agent stands in, the one it visited least recently (ties are
        /// deterministic per agent and SectorChoiceTicks window), and a point inside it, kept two units clear of the
        /// sector's walls. Arriving there changes the
        /// candidate set, so the next search moves on. False when the map offers no other sector.
        /// </summary>
        public static bool TryPickSectorWaypoint(in AgentContext context, Simpiens.Simulation.World.MapDefinition map, out Vec2 waypoint)
        {
            waypoint = default;
            var sectors = map.Sectors;
            byte current = map.FindSector(context.Position).Id;

            var snapshot = context.Snapshot;
            bool venture = Vitality.WouldVenture(context);
            int candidates = 0;
            for (int i = 0; i < sectors.Count; i++)
            {
                if (IsExplorable(snapshot, sectors[i], current, venture)) candidates++;
            }
            if (candidates == 0) return false;

            // Systematic search: the candidate sector this agent stood in least recently (never first); ties go to a
            // deterministic per-agent pick
            uint oldest = uint.MaxValue;
            int tied = 0;
            for (int i = 0; i < sectors.Count; i++)
            {
                if (!IsExplorable(snapshot, sectors[i], current, venture)) continue;
                uint visit = context.Memory != null ? context.Memory.LastSectorVisit(sectors[i].Id) : 0u;
                if (visit < oldest) { oldest = visit; tied = 1; }
                else if (visit == oldest) tied++;
            }

            uint h = ExplorationHash(context, SectorChoiceTicks);
            int pick = (int)(h % (uint)tied);
            for (int i = 0; i < sectors.Count; i++)
            {
                var sector = sectors[i];
                if (!IsExplorable(snapshot, sector, current, venture)) continue;
                uint lastVisit = context.Memory != null ? context.Memory.LastSectorVisit(sector.Id) : 0u;
                if (lastVisit != oldest) continue;
                if (pick-- > 0) continue;

                var bounds = sector.Bounds;
                float jitterX = SimMath.Max(0f, bounds.Width * 0.5f - 2f);
                float jitterY = SimMath.Max(0f, bounds.Height * 0.5f - 2f);
                float fx = ((h >> 8) % 1000u) / 1000f * 2f - 1f;
                float fy = ((h >> 18) % 1000u) / 1000f * 2f - 1f;
                waypoint = bounds.Center + new Vec2(fx * jitterX, fy * jitterY);
                return true;
            }
            return false;
        }

        /// <summary>Another sector, and a charted one: uncharted sectors are a scout's job (fog of war).</summary>
        // A hazard sector is explored only by a crawler who would go into danger anyway (Epic 10 G2)
        private static bool IsExplorable(Simpiens.Simulation.Spatial.SharedWorldSnapshot snapshot, in Simpiens.Simulation.World.SectorDefinition sector, byte current, bool venture)
        {
            return sector.Id != current && (snapshot == null || snapshot.IsSectorCharted(sector.Id)) && (venture || !sector.Hazard.IsHazardous);
        }

        private static uint ExplorationHash(in AgentContext context, uint windowTicks)
        {
            uint h = unchecked((uint)context.AgentId.GetHashCode() * 2654435761u ^ (context.CurrentTick / windowTicks) * 2246822519u);
            h ^= h >> 15;
            h = unchecked(h * 2654435761u);
            h ^= h >> 13;
            return h;
        }

        /// <summary>
        /// Paths toward a destination and wraps it in a WanderIntent; falls back to a short Idle when unroutable.
        /// </summary>
        public static async ValueTask<AgentIntent> BuildTravelIntentAsync(AgentContext context, IAsyncPathfinder pathfinder, Vec2 destination)
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
        public static bool IsReachable(PathResponse path, Vec2 targetPos)
        {
            if (path == null || !path.IsValid || path.Length <= 0) return false;
            Vec2 finalWaypoint = path.Waypoints[path.Length - 1];
            return Vec2.Distance(finalWaypoint, targetPos) <= HarvestReach;
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

                float distSqr = (stall.Position - context.Position).SqrMagnitude;
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
        /// True when the nearest reachable stall has room for salvage, isn't in arrears and its treasury can pay for at least one unit, so a sale won't
        /// fail on arrival. Without any visible stall this returns true (headless contexts); behind walls, false.
        /// </summary>
        /// <summary>
        /// True when the agent holds salvage for sale (not owed to a bounty) and the reachable stall would take none of
        /// it right now: that shelf is full, the treasury can't pay, the stall is in arrears, or the price is under the
        /// agent's minimum. The planner then does not count it as salvage to sell, so the agent can gather a kind the
        /// stall still buys instead of holding out all day (Epic 10 G1).
        /// </summary>
        public static bool StallRefusesHeldSalvage(in AgentContext context)
        {
            if (!HoldsAnySellable(context)) return false;
            return GetStallAccess(context, out var stall) == StallAccess.Reachable && AffordableHeldSalvage(context, stall) == ItemType.None;
        }

        public static bool NearestStallCanBuySalvage(in AgentContext context)
        {
            var access = GetStallAccess(context, out var stall);
            if (access == StallAccess.None) return true;
            if (access == StallAccess.Unreachable) return false;
            // The cheapest price the stall pays for a resource that isn't hazardous (ore, herbs): it must be able to pay that
            uint cheapest = uint.MaxValue;
            var resources = ItemCatalog.InCategory(ItemCategory.Resource);
            for (int i = 0; i < resources.Count; i++)
            {
                if (ItemCatalog.Is(resources[i], ItemCategory.Hazardous) || !stall.TryGetGood(resources[i], out var good) || good.Role != StallRole.Buys) continue;
                if (good.Price < cheapest) cheapest = good.Price;
            }
            if (cheapest == uint.MaxValue) cheapest = 0;
            var economy = Considerations.Economy(context);
            // A hazardous kind's shelf counts only for a crawler who would go into danger for it, and only while the stall
            // pays for it (Epic 10 G2)
            bool venture = Vitality.WouldVenture(context);
            bool room = false;
            var salvage = ItemCatalog.InCategory(ItemCategory.Resource);
            for (int i = 0; i < salvage.Count && !room; i++)
            {
                var kind = salvage[i];
                if (ItemCatalog.Is(kind, ItemCategory.Hazardous) && (!venture || stall.BuyPriceOf(kind) == 0)) continue;
                room = economy.StallHasRoom(stall, kind);
            }
            return room && !stall.InArrears && stall.TreasuryGold >= cheapest;
        }

        /// <summary>
        /// The held salvage type the stall can pay for (highest price first), or None if nothing held is affordable.
        /// </summary>
        /// <summary>
        /// True when the agent holds a unit of this salvage it may sell: more than its claimed Gather bounty still
        /// needs. Units owed to a contract are never sold at the stall (the bounty pays better and was promised).
        /// </summary>
        /// <summary>True when the agent holds any salvage it may sell (ore, herbs or Glowstone; none owed to a bounty).</summary>
        public static bool HoldsAnySellable(in AgentContext context)
        {
            var salvage = ItemCatalog.InCategory(ItemCategory.Resource);
            for (int i = 0; i < salvage.Count; i++)
            {
                if (HoldsSellable(context, salvage[i])) return true;
            }
            return false;
        }

        public static bool HoldsSellable(in AgentContext context, ItemType item)
        {
            if (context.Inventory == null) return false;
            int reserved = 0;
            if (BountyQueries.TryGetClaimedBounty(context, out var claimed) && claimed.Type == BountyType.GatherResource && claimed.TargetItemType == item)
            {
                reserved = claimed.TargetQuantity;
            }
            return context.Inventory.HasItem(item, reserved + 1);
        }

        public static ItemType AffordableHeldSalvage(in AgentContext context, in TradeStallData stall)
        {
            if (context.Inventory == null) return ItemType.None;
            // Payable by the treasury, with room in the stall, at or above the agent's personal minimum sale price, and
            // not owed to a bounty
            if (stall.InArrears) return ItemType.None; // a stall in arrears buys no salvage (review §3.3 D2)
            // The best-paid kind it will take. Kinds are tried in descending id and a later one wins only on a strictly
            // higher price, so at equal prices the higher id wins (glowstone, then herbs, then ore: the order before the
            // item catalog)
            var best = ItemType.None;
            ushort bestPrice = 0;
            var salvage = ItemCatalog.InCategory(ItemCategory.Resource);
            for (int i = salvage.Count - 1; i >= 0; i--)
            {
                Consider(context, stall, salvage[i], ref best, ref bestPrice);
            }
            return best;
        }

        private static void Consider(in AgentContext context, in TradeStallData stall, ItemType item, ref ItemType best, ref ushort bestPrice)
        {
            ushort price = stall.BuyPriceOf(item);
            if (price == 0 || (best != ItemType.None && price <= bestPrice)) return;
            if (!HoldsSellable(context, item) || stall.TreasuryGold < price) return;
            if (!Considerations.Economy(context).StallHasRoom(stall, item)) return;
            if (price < Considerations.MinimumSalePrice(context, item)) return;
            best = item;
            bestPrice = price;
        }

        /// <summary>
        /// True when the agent can buy a ration right now: reachable stall with stock and an affordable price.
        /// Without any visible stall (headless contexts) this returns true; behind walls, false.
        /// </summary>
        /// <summary>
        /// True when a reachable stall has a ration this agent can and will buy. Unlike CanBuyRation, false when no
        /// stall is visible at all (headless contexts): for the starvation reflex a missing stall is no food.
        /// </summary>
        public static bool HasStallFood(in AgentContext context)
        {
            return GetStallAccess(context, out _) == StallAccess.Reachable && CanBuyRation(context);
        }

        public static bool CanBuyRation(in AgentContext context)
        {
            var access = GetStallAccess(context, out var stall);
            if (access == StallAccess.None) return true;
            if (access == StallAccess.Unreachable) return false;
            int gold = context.Inventory != null ? context.Inventory.Gold : 0;
            var food = FoodForSale(stall);
            return stall.SellStockOf(food) > 0 && gold >= stall.SellPriceOf(food)
                && stall.SellPriceOf(food) <= Considerations.RationReservationPrice(context);
        }

        /// <summary>
        /// Epic 10 G3: the nearest reachable stall sells hazard gear at or below this crawler's price, and the crawler can
        /// pay for it and still buy a ration after (gear never costs it a meal).
        /// </summary>
        public static bool CanBuyGear(in AgentContext context)
        {
            if (GetStallAccess(context, out var stall) != StallAccess.Reachable) return false;
            int gold = context.Inventory != null ? context.Inventory.Gold : 0;
            var protection = ProtectionForSale(stall);
            ushort price = stall.SellPriceOf(protection);
            return stall.SellStockOf(protection) > 0 && price > 0
                && price <= Considerations.GearReservationPrice(context, protection)
                && gold >= price + stall.SellPriceOf(FoodForSale(stall));
        }

        /// <summary>The first food the stall sells, in id order (a ration); None when it sells none.</summary>
        public static ItemType FoodForSale(in TradeStallData stall) => FirstForSale(stall, ItemCategory.Food);

        /// <summary>The first protection the stall sells, in id order (hazard gear, Epic 10 G3); None when it sells none.</summary>
        public static ItemType ProtectionForSale(in TradeStallData stall) => FirstForSale(stall, ItemCategory.Protection);

        private static ItemType FirstForSale(in TradeStallData stall, ItemCategory group)
        {
            var members = ItemCatalog.InCategory(group);
            for (int i = 0; i < members.Count; i++)
            {
                if (stall.TryGetGood(members[i], out var good) && good.Role == StallRole.Sells) return members[i];
            }
            return ItemType.None;
        }

        /// <summary>True when the crawler knows a node of a hazardous resource (glowstone, Epic 10 G2) it would go for.</summary>
        public static bool KnowsHazardousSalvage(in AgentContext context)
        {
            var hazardous = ItemCatalog.InCategory(ItemCategory.Hazardous);
            for (int i = 0; i < hazardous.Count; i++)
            {
                if (KnowsSalvage(context, hazardous[i])) return true;
            }
            return false;
        }

        /// <summary>Nearest stall the agent can walk to.</summary>
        public static bool TryGetNearestStall(in AgentContext context, out TradeStallData nearest)
        {
            return GetStallAccess(context, out nearest) == StallAccess.Reachable;
        }
    }
}
