using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal: Recover stamina and reduce fatigue through resting.
    /// DesiredState: IsResting = true.
    /// Priority: Scales inversely with current agent Energy (100 - Energy).
    /// </summary>
    public class RestGoal : GoapGoal
    {
        public RestGoal() : base("Rest", WorldState.Empty.With(StateFact.IsResting, true))
        {
        }

        public override float CalculatePriority(AgentContext context)
        {
            // The more exhausted (lower energy), the higher the priority
            return 100f - context.Energy;
        }

        public override bool IsValid(AgentContext context)
        {
            // Only consider resting when energy falls below tired threshold; a hungry crawler eats first unless it is too
            // tired to walk, so its sleep isn't cut short by hunger (Epic 10 Part 2, D34)
            if (context.Energy >= Considerations.TiredEnergy) return false;
            return context.Hunger < Considerations.Economy(context).Day.BedtimeHunger || context.Energy <= Considerations.TooTiredToWalkEnergy;
        }
    }
}
