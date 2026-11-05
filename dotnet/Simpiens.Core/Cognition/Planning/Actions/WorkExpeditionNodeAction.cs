using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic 10 G5, D9): a party member at the expedition's work goes to the node it claimed and works it, whatever
    /// its boldness (D10): it complies, so the order takes it into danger. Party members work a node side by side, without
    /// rivalry. One unit at a time, so a member re-plans after each: when the guild turns the party back (the goal met,
    /// the nodes spent, the retreat rule), it heads home within one gather.
    /// Preconditions: HasActiveDirective = true, AtDirectiveWork = true, and the work is an expedition's. Effects: DirectiveFulfilled = true.
    /// </summary>
    public class WorkExpeditionNodeAction : GoapAction
    {
        /// <summary>Units asked of the node at once.</summary>
        public const int UnitsPerVisit = 1;

        private readonly IAsyncPathfinder _pathfinder;

        public WorkExpeditionNodeAction(IAsyncPathfinder pathfinder = null, float cost = 1.0f) : base("WorkExpeditionNode", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty.With(StateFact.HasActiveDirective, true).With(StateFact.AtDirectiveWork, true);
            Effects = WorldState.Empty.With(StateFact.DirectiveFulfilled, true);
        }

        /// <summary>The site's work is an expedition's node, not a station's (Epic 10 Part 2, W1.3).</summary>
        public override bool IsValid(AgentContext context) => context.Expedition.IsWorking;

        public override AgentIntent CreateIntent(AgentContext context)
        {
            if (!context.Expedition.IsWorking) return ResourceTargeting.MissingTargetIntent(context);
            return new HarvestResourceIntent(context.AgentId, context.Expedition.NodeId, null, targetUnits: UnitsPerVisit);
        }

        public override ValueTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (!context.Expedition.IsWorking) return new ValueTask<AgentIntent>(ResourceTargeting.MissingTargetIntent(context));
            return ResourceTargeting.BuildHarvestIntentAsync(context, _pathfinder, context.Expedition.NodeId, context.Expedition.NodePosition, UnitsPerVisit);
        }
    }
}
