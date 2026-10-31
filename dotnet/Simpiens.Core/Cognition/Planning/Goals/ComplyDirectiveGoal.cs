using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Social;
namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal: Comply with an active group directive issued by the collective's leadership.
    /// DesiredState: DirectiveFulfilled = true.
    /// Priority: Dynamically scaled by U_comply.
    /// Severely starving agents (Hunger >= 70) yield to food seeking unless fear intimidation (Fear >= 70) is overwhelming.
    /// </summary>
    public class ComplyDirectiveGoal : GoapGoal
    {
        public const float StarvationThreshold = 70f;

        /// <summary>
        /// A member out on the guild's expedition holds to it (Epic 10 G5): it judged the order once, at dispatch (D8), and
        /// then follows it above the hearth (up to about 95 before personality) and earning, whatever its loyalty. The
        /// reflexes and hunger from 70 still come first.
        /// </summary>
        public const float ExpeditionPriority = 120f;
        public const float IntimidationFearThreshold = 70f;

        public ComplyDirectiveGoal() : base("ComplyDirective", WorldState.Empty.With(StateFact.DirectiveFulfilled, true))
        {
        }

        public override bool IsValid(AgentContext context)
        {
            if (!context.DirectiveMembership.HasGroup) return false;
            if (context.Snapshot == null) return false;

            if (!context.Snapshot.TryGetGroup(context.DirectiveMembership.GroupId, out var groupData))
            {
                return false;
            }

            if (!groupData.ActiveDirective.IsActive(context.CurrentTick))
            {
                return false;
            }

            // A member out on the guild's expedition judged the order once, at dispatch (Epic 10 G5, D8): still with the party,
            // it complied, and holds to it
            if (context.Expedition.IsActive) return true;

            // Subordinate only complies if compliance utility meets threshold (U >= 20)
            float utility = ComplianceUtility.CalculateUtility(context, groupData);
            return utility >= ComplianceUtility.ComplianceThreshold;
        }

        public override float CalculatePriority(AgentContext context)
        {
            if (!context.DirectiveMembership.HasGroup || context.Snapshot == null) return 0f;
            if (!context.Snapshot.TryGetGroup(context.DirectiveMembership.GroupId, out var groupData)) return 0f;
            if (!groupData.ActiveDirective.IsActive(context.CurrentTick)) return 0f;
            if (context.Expedition.IsActive) return context.Hunger >= StarvationThreshold ? 10f : ExpeditionPriority;

            float utility = ComplianceUtility.CalculateUtility(context, groupData);
            if (utility < ComplianceUtility.ComplianceThreshold)
            {
                return 0f;
            }

            var affinity = context.Memory != null
                ? context.Memory.GetAffinity(groupData.LeaderId, context.CurrentTick)
                : AffinityRecord.Neutral;

            // Severe starvation preemption check:
            // If Hunger >= 70, basic survival drive takes precedence unless leader intimidation (Fear >= 70) is overwhelming.
            if (context.Hunger >= StarvationThreshold && affinity.Fear < IntimidationFearThreshold)
            {
                // Clamped low so SatiateHungerGoal (priority 90+) takes precedence
                return SimMath.Max(10f, utility * 0.5f);
            }

            // Normal compliance priority: Base 50 + U_comply (ranges typically 70 to 120)
            return (50f + utility) * Considerations.TraitFactor(context.Personality.Loyalty, Considerations.LoyaltyOnCompliance);
        }
    }
}
