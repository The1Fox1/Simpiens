using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Social;

namespace Simpiens.Cognition.Groups
{
    /// <summary>
    /// Sociopolitical compliance decision outcome.
    /// </summary>
    public enum ComplianceDecision : byte
    {
        ActiveDesertion = 0,
        PassiveDefiance = 1,
        Comply = 2
    }

    /// <summary>
    /// Pure C# zero-allocation static utility for computing directive compliance utility U_comply
    /// and resolving compliance/defiance decisions per Epic 8 Section 5.
    /// </summary>
    public static class ComplianceUtility
    {
        public const float ComplianceThreshold = 20f;
        public const float LineOfSightRadius = 6.0f;

        /// <summary>
        /// Computes the compliance utility equation:
        /// U_comply = (Respect * 0.35) + (Fear * 0.30) + (Allegiance * 0.25) + (Warmth * 0.10) - (SelfHunger * 0.30) - (SelfFrustration * 0.20)
        /// </summary>
        public static float CalculateUtility(in AgentContext context, in GroupData groupData)
        {
            var affinity = context.Memory != null
                ? context.Memory.GetAffinity(groupData.LeaderId, context.CurrentTick)
                : AffinityRecord.Neutral;

            float respect = affinity.Respect;
            float fear = affinity.Fear;
            float allegiance = context.DirectiveMembership.Allegiance;
            float warmth = affinity.Warmth;
            float hunger = context.Hunger;
            float frustration = context.Frustration;

            return (respect * 0.35f) + (fear * 0.30f) + (allegiance * 0.25f) + (warmth * 0.10f) - (hunger * 0.30f) - (frustration * 0.20f);
        }

        /// <summary>
        /// Resolves the categorical decision: Comply (>= 20), PassiveDefiance (0 to <20), or ActiveDesertion (< 0).
        /// </summary>
        public static ComplianceDecision Evaluate(in AgentContext context, in GroupData groupData)
        {
            float utility = CalculateUtility(context, groupData);
            if (utility >= ComplianceThreshold)
            {
                return ComplianceDecision.Comply;
            }
            if (utility >= 0f)
            {
                return ComplianceDecision.PassiveDefiance;
            }
            return ComplianceDecision.ActiveDesertion;
        }
    }
}
