using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Executes/holds the group directive at the designated location (rallying, guarding, harvesting).
    /// Preconditions: AtDirectiveLocation = true; not work at a site, which is <see cref="WorkExpeditionNodeAction"/>'s or <see cref="WorkStationAction"/>'s.
    /// Effects: DirectiveFulfilled = true.
    /// </summary>
    public class ExecuteDirectiveAction : GoapAction
    {
        public ExecuteDirectiveAction(float cost = 1.0f) : base("ExecuteDirective", cost)
        {
            Preconditions = WorldState.Empty.With(StateFact.AtDirectiveLocation, true).With(StateFact.AtDirectiveWork, false);
            Effects = WorldState.Empty.With(StateFact.DirectiveFulfilled, true);
        }

        /// <summary>A step planned before the party got to work is dropped there, rather than standing idle in danger (Epic 10 G5).</summary>
        public override bool IsValid(AgentContext context) => !context.Expedition.IsWorking;

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new IdleIntent(context.AgentId, 2.5f);
        }
    }
}
