using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic 11, S3.4, D10): a member walks to the drill yard and drills a game hour at the fight work of what it
    /// wields, at practice's source Yard, to 5. Preconditions: none. Effects: Drilled = true.
    /// </summary>
    public class DrillAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public DrillAction(IAsyncPathfinder pathfinder = null, float cost = 1.0f) : base("Drill", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty;
            Effects = WorldState.Empty.With(StateFact.Drilled, true);
        }

        public override bool IsValid(AgentContext context) => Drill.TryFindYard(context, out _, out _, out _);

        public override AgentIntent CreateIntent(AgentContext context)
        {
            Drill.TryFindYard(context, out int index, out _, out _);
            return new DrillIntent(context.AgentId, index, null);
        }

        public override async ValueTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (!Drill.TryFindYard(context, out int index, out var position, out float radius)) return CreateIntent(context);
            if (_pathfinder == null || context.Snapshot == null || (position - context.Position).SqrMagnitude <= radius * radius) return new DrillIntent(context.AgentId, index, null);
            var path = await _pathfinder.CalculatePathAsync(new PathRequest { StartPosition = context.Position, TargetPosition = position, RequesterId = context.AgentId }, context.Snapshot);
            if (ResourceTargeting.IsReachable(path, position)) return new DrillIntent(context.AgentId, index, path);
            path?.ReturnToPool();
            return CreateIntent(context);
        }
    }
}
