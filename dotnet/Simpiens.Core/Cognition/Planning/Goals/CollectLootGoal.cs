using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal (Epic 10 Part 3, K4.3d; A5): a crawler who sees a pile it would take from picks it over: the victor its kill's
    /// drop, anyone a pile whose claim has run out. DesiredState: LootCollected = true, reached through PickUpLoot. Not while
    /// hungry or worn out. Majesty's heroes pick up what they slay; Kenshi's scavengers what others leave.
    /// </summary>
    public class CollectLootGoal : GoapGoal
    {
        /// <summary>Above outfitting for danger (40): it lies within sight, a few minutes' work.</summary>
        public const float Priority = 42f;

        public CollectLootGoal() : base("CollectLoot", WorldState.Empty.With(StateFact.LootCollected, true))
        {
        }

        public override float CalculatePriority(AgentContext context) => Priority;

        public override bool IsValid(AgentContext context)
        {
            if (context.Inventory == null || context.IsDowned) return false;
            if (context.Hunger >= 60f || context.Energy <= 25f) return false;
            return LootTargeting.TryChoosePile(context, out _);
        }
    }
}
