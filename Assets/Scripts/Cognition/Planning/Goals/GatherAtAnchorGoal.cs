using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal: Gather around a communal anchor (Campfire / Hearth) to rest and bond with peers.
    /// DesiredState: IsGatheredAtAnchor = true.
    /// Priority: Evaluates when basic drives are healthy (Hunger < 60, Energy > 20).
    /// </summary>
    public class GatherAtAnchorGoal : GoapGoal
    {
        public const float StarvationCrisisThreshold = 60f;
        public const float ExhaustionCrisisThreshold = 20f;

        public GatherAtAnchorGoal() : base("GatherAtAnchor", WorldState.Empty.With(StateFact.IsGatheredAtAnchor, true))
        {
        }

        public override float CalculatePriority(AgentContext context)
        {
            // If in biological crisis (starving or exhausted), anchor gathering is completely suppressed
            if (context.Hunger >= StarvationCrisisThreshold || context.Energy <= ExhaustionCrisisThreshold)
            {
                return 0f;
            }

            // Sated/healthy agents seek communal comfort; priority scales with social need
            float priority = Mathf.Max(0f, 75f - context.Social) + 15f;

            // Relational bonus: If agent knows warm or trusted companions, hearth gathering is especially attractive
            if (context.Memory != null && context.Memory.AffinityMap != null)
            {
                foreach (var kvp in context.Memory.AffinityMap)
                {
                    var record = context.Memory.GetAffinity(kvp.Key, context.CurrentTick);
                    if (record.Warmth >= 20 || record.Trust >= 20)
                    {
                        priority += 5f;
                        break;
                    }
                }
            }

            return priority * Considerations.TraitFactor(context.Personality.Sociability, Considerations.SociabilityOnSocial);
        }

        public override bool IsValid(AgentContext context)
        {
            // Only valid if agent is not in starvation or exhaustion crisis
            if (context.Hunger >= StarvationCrisisThreshold || context.Energy <= ExhaustionCrisisThreshold) return false;

            // And agent must know of at least one valid anchor in memory
            if (context.Memory == null || context.Memory.SpatialMemoryMap == null) return false;

            foreach (var kvp in context.Memory.SpatialMemoryMap)
            {
                if (kvp.Value.Type == EntityType.Anchor && !context.Memory.IsBlacklisted(kvp.Value.EntityId, context.CurrentTick))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
