using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic 10 Part 2, W1.3, D13): an artisan on shift goes to its station and works it, as a party member works
    /// its node (<see cref="WorkExpeditionNodeAction"/>). It re-plans after each game hour at the station, so needs and the
    /// end of its shift come back into its decisions.
    /// Preconditions: HasActiveDirective = true, AtDirectiveWork = true, and the work is a station's. Effects: DirectiveFulfilled = true.
    /// </summary>
    public class WorkStationAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public WorkStationAction(IAsyncPathfinder pathfinder = null, float cost = 1.0f) : base("WorkStation", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty.With(StateFact.HasActiveDirective, true).With(StateFact.AtDirectiveWork, true);
            Effects = WorldState.Empty.With(StateFact.DirectiveFulfilled, true);
        }

        /// <summary>The site's work is a station's, on shift.</summary>
        public override bool IsValid(AgentContext context) => context.Station.IsActive;

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new WorkStationIntent(context.AgentId, context.Station.StationIndex, null);
        }

        public override async ValueTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            var station = context.Station;
            if (_pathfinder == null || context.Snapshot == null || (station.Position - context.Position).SqrMagnitude <= station.Radius * station.Radius)
            {
                return CreateIntent(context);
            }
            var path = await _pathfinder.CalculatePathAsync(new PathRequest { StartPosition = context.Position, TargetPosition = station.Position, RequesterId = context.AgentId }, context.Snapshot);
            if (ResourceTargeting.IsReachable(path, station.Position)) return new WorkStationIntent(context.AgentId, station.StationIndex, path);
            path?.ReturnToPool();
            return CreateIntent(context);
        }
    }
}
