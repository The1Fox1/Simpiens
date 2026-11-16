using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal (Epic 10 Part 3, K5.3, Q3b): a crawler who hears a friend or a party mate collapsed in danger, somewhere it would
    /// go itself, comes for it: it hands over a draught it carries, or carries the fallen out (<see cref="Rescue"/>).
    /// DesiredState: FallenRescued = true, through Rescue. Above every other goal but leaving: a life is on a clock.
    /// </summary>
    public class RescueGoal : GoapGoal
    {
        public const float Priority = 90f;

        /// <summary>A fallen party mate's rescue outranks the order (Epic 10 Part 3, K7.2c, Q8: "members carry their fallen out"): over the expedition's 120.</summary>
        public const float UnderOrdersPriority = 130f;

        public RescueGoal() : base("Rescue", WorldState.Empty.With(StateFact.FallenRescued, true))
        {
        }

        public override float CalculatePriority(AgentContext context) =>
            Rescue.TryChooseFallen(context, out var fallen, out _) && fallen.PartyGroupId != 0 && fallen.PartyGroupId == context.PartyGroupId ? UnderOrdersPriority : Priority;

        public override bool IsValid(AgentContext context) => context.Inventory != null && Rescue.TryChooseFallen(context, out _, out _);
    }
}
