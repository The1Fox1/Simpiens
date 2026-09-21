using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Spatial;
using UnityEngine;

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
        public const float ThreatFrustrationThreshold = 60f;

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
            state = state.With(StateFact.HasFoodInInventory, hasFoodInInventory);
            state = state.With(StateFact.HasTool, hasTool);

            // 3. Psychological / Threat
            bool isThreatened = context.Frustration > ThreatFrustrationThreshold;
            state = state.With(StateFact.IsThreatened, isThreatened);

            // 4. Memory & Spatial Queries
            if (context.Memory != null)
            {
                bool knowsResource = false;
                bool atResource = false;
                bool isNearPeer = false;

                float reachDistSqr = ReachResourceDistance * ReachResourceDistance;
                float peerDistSqr = PeerProximityDistance * PeerProximityDistance;

                foreach (var kvp in context.Memory.SpatialMemoryMap)
                {
                    var record = kvp.Value;
                    if (record.Type == EntityType.Resource && !context.Memory.IsBlacklisted(record.EntityId, context.CurrentTick))
                    {
                        knowsResource = true;
                        Vector2 loc = record.LastKnownLocation;
                        float distSqr = (loc - context.Position).sqrMagnitude;
                        if (distSqr <= reachDistSqr)
                        {
                            atResource = true;
                        }
                    }
                    else if (record.Type == EntityType.Pawn && record.EntityId != context.AgentId)
                    {
                        Vector2 loc = record.LastKnownLocation;
                        float distSqr = (loc - context.Position).sqrMagnitude;
                        if (distSqr <= peerDistSqr)
                        {
                            isNearPeer = true;
                        }
                    }
                }

                state = state.With(StateFact.KnowsResourceLocation, knowsResource);
                state = state.With(StateFact.AtResourceLocation, atResource);
                state = state.With(StateFact.IsNearPeer, isNearPeer);
            }

            return state;
        }
    }
}
