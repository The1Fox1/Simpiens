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

        /// <summary>
        /// A member of a clear picks up what lies in sight above the order (Epic 10 Part 3, K7.1: the haul is what the party
        /// picked up): over the expedition's 120, under a mate's rescue.
        /// </summary>
        public const float ClearPriority = 125f;

        public override float CalculatePriority(AgentContext context) => context.Expedition.Kind == Simpiens.Simulation.Guild.ExpeditionKind.Clear && context.Expedition.IsActive ? ClearPriority : Priority;

        public override bool IsValid(AgentContext context)
        {
            if (context.Inventory == null || context.IsDowned) return false;
            if (context.Hunger >= 60f || context.Energy <= 25f) return false;
            return LootTargeting.TryChoosePile(context, out _);
        }
    }
}
