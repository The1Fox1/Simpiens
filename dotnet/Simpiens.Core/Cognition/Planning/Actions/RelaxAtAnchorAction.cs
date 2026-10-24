using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Relaxes and bonds with peers around a communal anchor (Campfire / Hearth).
    /// Preconditions: AtAnchorLocation = true.
    /// Effects: IsGatheredAtAnchor = true, IsResting = true.
    /// </summary>
    public class RelaxAtAnchorAction : GoapAction
    {
        public RelaxAtAnchorAction(float cost = 1.0f) : base("RelaxAtAnchor", cost)
        {
            Preconditions = WorldState.Empty.With(StateFact.AtAnchorLocation, true);
            Effects = WorldState.Empty
                .With(StateFact.IsGatheredAtAnchor, true)
                .With(StateFact.IsResting, true);
        }

        /// <summary>
        /// A tired crawler who knows a free bunk sleeps there rather than dozing by the fire (Considerations.HearthRestCost);
        /// otherwise relaxing at the hearth keeps its base cost.
        /// </summary>
        public override float GetCost(AgentContext context)
        {
            return Considerations.HearthRestCost(context, BaseCost);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new IdleIntent(context.AgentId, 3.0f);
        }
    }
}
