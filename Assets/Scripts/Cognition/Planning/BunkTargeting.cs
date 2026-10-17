using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Allocation-free bunk lookups for the rest anchor (Epic MAP Phase 3): bunks the agent remembers, that are not
    /// blacklisted and that the snapshot shows free. Nearest wins; ties go to the lower entity id.
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

        public static bool TryGetNearestFreeBunk(in AgentContext context, out EntityId bunkId, out Vector2 position)
        {
            bunkId = default;
            position = default;
            if (context.Memory == null || context.Memory.SpatialMemoryMap == null) return false;

            bool found = false;
            float best = float.MaxValue;
            foreach (var kvp in context.Memory.SpatialMemoryMap)
            {
                var record = kvp.Value;
                if (record.Type != EntityType.Bunk || context.Memory.IsBlacklisted(record.EntityId, context.CurrentTick)) continue;
                if (context.Snapshot != null && context.Snapshot.IsBunkOccupied(record.EntityId)) continue;

                Vector2 at = record.LastKnownLocation;
                float distSqr = (at - context.Position).sqrMagnitude;
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
        public static bool TryGetBunkAtHand(in AgentContext context, out EntityId bunkId, out Vector2 position)
        {
            bunkId = default;
            if (!TryGetNearestFreeBunk(context, out var id, out position)) return false;
            if ((position - context.Position).sqrMagnitude > ApproachDistance * ApproachDistance) return false;
            bunkId = id;
            return true;
        }
    }
}
