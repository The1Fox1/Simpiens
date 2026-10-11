using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Executes/holds the group directive at the designated location (rallying, guarding, harvesting).
    /// Preconditions: AtDirectiveLocation = true.
    /// Effects: DirectiveFulfilled = true.
    /// </summary>
    public class ExecuteDirectiveAction : GoapAction
    {
        public ExecuteDirectiveAction(float cost = 1.0f) : base("ExecuteDirective", cost)
        {
            Preconditions = WorldState.Empty.With(StateFact.AtDirectiveLocation, true);
            Effects = WorldState.Empty.With(StateFact.DirectiveFulfilled, true);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new IdleIntent(context.AgentId, 2.5f);
        }
    }
}
