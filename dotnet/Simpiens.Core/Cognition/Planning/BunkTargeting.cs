using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Spatial;
namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Allocation-free bunk lookups for the rest anchor (Epic MAP Phase 3): bunks the agent remembers, that are not
    /// blacklisted and that the snapshot shows free. Nearest wins; ties go to the lower entity id. A guild member's own
    /// bunk in the barracks comes first, wherever it is (Epic 10 Part 2, W5.2, D62); nobody else remembers the barracks'
    /// bunks as bunks, since they are another kind of entity.
    /// </summary>
    public static class BunkTargeting
    {
        /// <summary>An agent this close to a bunk is at it and may sleep there.</summary>
        public const float ReachDistance = 0.6f;

        /// <summary>
        /// A walk to a bunk can end a step short (a pawn in the way, the bunk taken on arrival); from this close the
        /// sleeper makes the last straight step itself (IIntentExecutionContext.StepOntoBunk).
        /// </summary>
        public const float ApproachDistance = 1.5f;

        /// <param name="ownFirst">False for the nearest bunk however far its own is (a downed crawler sleeps where it lies).</param>
        public static bool TryGetNearestFreeBunk(in AgentContext context, out EntityId bunkId, out Vec2 position, bool ownFirst = true)
        {
            bunkId = default;
            position = default;

            // Its own bunk, as a colonist goes to its own bed
            var own = context.Bunk;
            if (ownFirst && own.IsKept && (context.Memory == null || !context.Memory.IsBlacklisted(own.BunkId, context.CurrentTick))
                && (context.Snapshot == null || !context.Snapshot.IsBunkOccupied(own.BunkId)))
            {
                bunkId = own.BunkId;
                position = own.Position;
                return true;
            }

            if (context.Memory == null || context.Memory.SpatialMemoryMap == null) return false;

            bool found = false;
            float best = float.MaxValue;
            foreach (var kvp in context.Memory.SpatialMemoryMap)
            {
                var record = kvp.Value;
                if (record.Type != EntityType.Bunk || context.Memory.IsBlacklisted(record.EntityId, context.CurrentTick)) continue;
                if (context.Snapshot != null && context.Snapshot.IsBunkOccupied(record.EntityId)) continue;

                Vec2 at = record.LastKnownLocation;
                float distSqr = (at - context.Position).SqrMagnitude;
                if (!found || distSqr < best || (distSqr == best && record.EntityId.CompareTo(bunkId) < 0))
                {
                    found = true;
                    best = distSqr;
                    bunkId = record.EntityId;
                    position = at;
                }
            }
            return found;
        }

        /// <summary>The nearest free bunk within a last step (ApproachDistance) of the agent, if any.</summary>
        public static bool TryGetBunkAtHand(in AgentContext context, out EntityId bunkId, out Vec2 position)
        {
            bunkId = default;
            if (!TryGetNearestFreeBunk(context, out var id, out position)) return false;
            if ((position - context.Position).SqrMagnitude > ApproachDistance * ApproachDistance) return false;
            bunkId = id;
            return true;
        }
    }
}
