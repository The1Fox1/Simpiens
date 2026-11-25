using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Goals
{
    /// <summary>
    /// Goal: Recover stamina and reduce fatigue through resting.
    /// DesiredState: IsResting = true.
    /// Priority: Scales inversely with current agent Energy (100 - Energy); in the routine's night the motive model weighs it
    /// 1.5 (Epic LIFE-1 plan 3, R.3, D31), and it is valid from the bedtime line.
    /// </summary>
    public class RestGoal : GoapGoal
    {
        public RestGoal() : base("Rest", WorldState.Empty.With(StateFact.IsResting, true), Motive.Rest, ActivityFamily.Rest)
        {
        }

        public override float BasePriority(AgentContext context)
        {
            // The more exhausted (lower energy), the higher the priority
            return 100f - context.Energy;
        }

        public override bool IsValid(AgentContext context)
        {
            // Only consider resting when energy falls below tired threshold, or, in its routine's night, below the bedtime line
            // (Epic LIFE-1 plan 3, R.3, D31: it goes to bed at bedtime rather than when it drops); a hungry crawler eats first
            // unless it is too tired to walk, so its sleep isn't cut short by hunger (Epic 10 Part 2, D34)
            if (context.Energy >= Considerations.TiredEnergy && !Considerations.IsBedtime(context)) return false;
            return context.Hunger < Considerations.Economy(context).Day.BedtimeHunger || context.Energy <= Considerations.TooTiredToWalkEnergy;
        }
    }
}
