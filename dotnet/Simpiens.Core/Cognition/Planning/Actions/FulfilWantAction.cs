using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic LIFE-1 plan 2, W.3, D20): the crawler walks to the seat of the best spell that answers a want it holds and
    /// spends it there (a <see cref="RecreateIntent"/>, whose end fulfils the want). Preconditions: none. Effects: WantMet = true.
    /// </summary>
    public class FulfilWantAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public FulfilWantAction(IAsyncPathfinder pathfinder = null, float cost = 1.0f) : base("FulfilWant", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty;
            Effects = WorldState.Empty.With(StateFact.WantMet, true);
        }

        public override bool IsValid(AgentContext context) => Leisure.TryChooseForWants(context, out _, out _, out _, out _);

        public override AgentIntent CreateIntent(AgentContext context)
        {
            Leisure.TryChooseForWants(context, out int venue, out var activity, out int seat, out _);
            return new RecreateIntent(context.AgentId, venue, activity, seat, null);
        }

        public override async ValueTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (!Leisure.TryChooseForWants(context, out int venue, out var activity, out int seat, out _)) return CreateIntent(context);
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
