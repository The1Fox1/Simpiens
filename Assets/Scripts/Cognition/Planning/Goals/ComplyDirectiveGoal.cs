using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Social;
using UnityEngine;

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
        public const float IntimidationFearThreshold = 70f;

        public ComplyDirectiveGoal() : base("ComplyDirective", WorldState.Empty.With(StateFact.DirectiveFulfilled, true))
        {
        }

        public override bool IsValid(AgentContext context)
        {
            if (!context.Membership.HasGroup) return false;
            if (context.Snapshot == null) return false;

            if (!context.Snapshot.TryGetGroup(context.Membership.GroupId, out var groupData))
            {
                return false;
            }

            if (!groupData.ActiveDirective.IsActive(context.CurrentTick))
            {
                return false;
            }

            // Subordinate only complies if compliance utility meets threshold (U >= 20)
            float utility = ComplianceUtility.CalculateUtility(context, groupData);
            return utility >= ComplianceUtility.ComplianceThreshold;
        }

        public override float CalculatePriority(AgentContext context)
        {
            if (!context.Membership.HasGroup || context.Snapshot == null) return 0f;
            if (!context.Snapshot.TryGetGroup(context.Membership.GroupId, out var groupData)) return 0f;
            if (!groupData.ActiveDirective.IsActive(context.CurrentTick)) return 0f;

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
                return Mathf.Max(10f, utility * 0.5f);
            }

            // Normal compliance priority: Base 50 + U_comply (ranges typically 70 to 120)
            return 50f + utility;
        }
    }
}
