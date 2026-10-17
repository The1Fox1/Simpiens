using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal: Eliminate hunger.
    /// DesiredState: IsHungry = false.
    /// Priority: Directly proportional to current agent hunger.
    /// </summary>
    public class SatiateHungerGoal : GoapGoal
    {
        public SatiateHungerGoal() : base("SatiateHunger", WorldState.Empty.With(StateFact.IsHungry, false))
        {
        }

        public override float CalculatePriority(AgentContext context)
        {
            // Higher hunger yields higher priority
            return context.Hunger;
        }

        public override bool IsValid(AgentContext context)
        {
            // Only valid if the agent is actually hungry (> 20)
            return context.Hunger > 20f;
        }
    }
}
