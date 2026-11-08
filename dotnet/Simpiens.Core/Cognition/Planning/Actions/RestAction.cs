using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Rest at current location for a brief recovery period.
    /// Preconditions: None.
    /// Effects: IsResting = true.
    /// </summary>
    public class RestAction : GoapAction
    {
        public RestAction(float cost = 1.0f) : base("Rest", cost)
        {
            Preconditions = WorldState.Empty;
            Effects = WorldState.Empty.With(StateFact.IsResting, true);
        }

        /// <summary>
        /// Resting where you stand is the fallback: dearer than a walk to a free bunk the agent knows, unless it is too
        /// tired to walk (Considerations.FieldRestCost).
        /// </summary>
        public override float GetCost(AgentContext context)
        {
            return Considerations.FieldRestCost(context, BaseCost);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            // A pause of the session's rest length (DayTuning, Epic 10 Part 2, milestone T)
            return new IdleIntent(context.AgentId, Considerations.Economy(context).Day.RestSeconds);
        }
    }
}
