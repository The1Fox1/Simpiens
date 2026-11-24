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
        /// A member out on the guild's expedition who complies follows it above the hearth (up to about 95 before personality)
        /// and earning (Epic 10 G5). It judges the order by compliance at every choice and at every stage, as an artisan judges
        /// its shift (Epic LIFE-1 plan 2's O.2, D25: orders are a motive a crawler may set aside, never a rule), and its mood
        /// weighs its duty (O.1). The reflexes and hunger from 70 still come first.
        /// </summary>
        public const float ExpeditionPriority = 120f;

        /// <summary>
        /// An artisan on shift holds to its station's work as a party member holds to its expedition (Epic 10 Part 2, W1.3,
        /// D13, D15): above the hearth, earning and rest, so tiredness alone doesn't end a shift; the break line does
        /// (<see cref="Simpiens.Simulation.Workshops.WorkshopService"/>). Unlike an expedition's, the order is judged by
        /// compliance every time. Hunger from 70 still comes first.
        /// </summary>
        public const float ShiftPriority = ExpeditionPriority;
        public const float IntimidationFearThreshold = 70f;

        public ComplyDirectiveGoal() : base("ComplyDirective", WorldState.Empty.With(StateFact.DirectiveFulfilled, true), Motive.Duty, ActivityFamily.Unweighted)
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

            // Subordinate only complies if compliance utility meets threshold (U >= 20): a member out with the guild's party too,
            // at every choice (plan 2's O.2, D25)
            float utility = ComplianceUtility.CalculateUtility(context, groupData);
            return utility >= ComplianceUtility.ComplianceThreshold;
        }

        public override float BasePriority(AgentContext context)
        {
            if (!context.DirectiveMembership.HasGroup || context.Snapshot == null) return 0f;
            if (!context.Snapshot.TryGetGroup(context.DirectiveMembership.GroupId, out var groupData)) return 0f;
            if (!groupData.ActiveDirective.IsActive(context.CurrentTick)) return 0f;
            float utility = ComplianceUtility.CalculateUtility(context, groupData);
            if (utility < ComplianceUtility.ComplianceThreshold)
            {
                return 0f;
            }
            if (context.Expedition.IsActive) return context.Hunger >= StarvationThreshold ? 10f : ExpeditionPriority;
            if (context.Station.IsActive) return context.Hunger >= StarvationThreshold ? 10f : ShiftPriority;

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
