using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic LIFE-1 plan 2, L2.6, D14): the crawler walks to the best seat its leisure score finds and spends the
    /// activity's spell there (<see cref="Leisure.TryChoose"/>). Preconditions: none. Effects: Recreated = true.
    /// </summary>
    public class RecreateAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public RecreateAction(IAsyncPathfinder pathfinder = null, float cost = 1.0f) : base("Recreate", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty;
            Effects = WorldState.Empty.With(StateFact.Recreated, true);
        }

        public override bool IsValid(AgentContext context) => Leisure.TryChoose(context, out _, out _, out _, out _);

        public override AgentIntent CreateIntent(AgentContext context)
        {
            Leisure.TryChoose(context, out int venue, out var activity, out int seat, out _);
            return new RecreateIntent(context.AgentId, venue, activity, seat, null);
        }

        public override async ValueTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (!Leisure.TryChoose(context, out int venue, out var activity, out int seat, out _)) return CreateIntent(context);
            var position = context.Snapshot.Map.Venues[venue].Seats[seat];
            if (_pathfinder == null || (position - context.Position).SqrMagnitude <= Handlers.RecreateIntentHandler.SeatReach * Handlers.RecreateIntentHandler.SeatReach)
                return new RecreateIntent(context.AgentId, venue, activity, seat, null);
            var path = await _pathfinder.CalculatePathAsync(new PathRequest { StartPosition = context.Position, TargetPosition = position, RequesterId = context.AgentId }, context.Snapshot);
            if (ResourceTargeting.IsReachable(path, position)) return new RecreateIntent(context.AgentId, venue, activity, seat, path);
            path?.ReturnToPool();
            return CreateIntent(context);
        }
    }
}
