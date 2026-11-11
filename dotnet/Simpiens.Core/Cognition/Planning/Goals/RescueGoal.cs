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

        public RescueGoal() : base("Rescue", WorldState.Empty.With(StateFact.FallenRescued, true))
        {
        }

        public override float CalculatePriority(AgentContext context) => Priority;

        public override bool IsValid(AgentContext context) => context.Inventory != null && Rescue.TryChooseFallen(context, out _, out _);
    }
}
