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
            // Only consider resting when energy falls below tired threshold
            return context.Energy < 40f;
        }
    }
}
