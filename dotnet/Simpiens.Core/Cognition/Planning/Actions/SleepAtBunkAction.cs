using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: sleeps in the free bunk the agent stands at, recovering energy faster than resting in place
    /// (Epic MAP Phase 3 rest anchor).
    /// Preconditions: AtBunk = true.
    /// Effects: IsResting = true.
    /// </summary>
    public class SleepAtBunkAction : GoapAction
    {
        public SleepAtBunkAction(float cost = 0.5f) : base("SleepAtBunk", cost)
        {
            Preconditions = WorldState.Empty.With(StateFact.AtBunk, true);
            Effects = WorldState.Empty.With(StateFact.IsResting, true);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            // No free bunk within a step any more (taken since planning): a short pause, then the plan is re-evaluated
            // The sleeper makes the last step onto the bunk itself (SleepAtBunkIntentHandler), so a walk that ended
            // a step short still sleeps
            if (!BunkTargeting.TryGetBunkAtHand(context, out var bunkId, out _))
            {
                return new IdleIntent(context.AgentId, 0.5f);
            }
            // Asleep for the session's longest sleep, or until rested (DayTuning, Epic 10 Part 2, milestone T)
            return new SleepAtBunkIntent(context.AgentId, bunkId, Considerations.Economy(context).Day.SleepSeconds);
        }
    }
}
