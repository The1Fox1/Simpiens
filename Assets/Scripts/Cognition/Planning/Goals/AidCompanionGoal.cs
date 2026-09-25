using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal: Share food with a distressed companion to alleviate their starvation or collapse.
    /// DesiredState: HasAidedPeer = true.
    /// Priority: Dynamically calculated from Warmth, Trust, and companion distress state.
    /// </summary>
    public class AidCompanionGoal : GoapGoal
    {
        public AidCompanionGoal() : base("AidCompanion", WorldState.Empty.With(StateFact.HasAidedPeer, true))
        {
        }

        public override float CalculatePriority(AgentContext context)
        {
            if (context.Hunger >= 50f || context.Snapshot == null || context.Memory == null) return -1f;

            float highestAltruism = -1f;
            foreach (var kvp in context.Memory.AffinityMap)
            {
                var affinity = context.Memory.GetAffinity(kvp.Key, context.CurrentTick);
                if (affinity.Warmth > 0 || affinity.Trust > 0)
                {
                    float score = context.Memory.CalculateAltruismScore(kvp.Key, context.CurrentTick, context.Hunger, isTargetCollapsed: false);
                    if (score > highestAltruism)
                    {
                        highestAltruism = score;
                    }
                }
            }

            return highestAltruism;
        }

        public override bool IsValid(AgentContext context)
        {
            // Only valid if agent is sated (< 50 hunger)
            return context.Hunger < 50f;
        }
    }
}
