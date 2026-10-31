using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Cognition.Groups;
namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Navigates toward the target location of an active group directive.
    /// Preconditions: HasActiveDirective = true.
    /// Effects: AtDirectiveLocation = true.
    /// </summary>
    public class TravelToDirectiveAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public TravelToDirectiveAction(IAsyncPathfinder pathfinder = null, float cost = 2.0f) : base("TravelToDirective", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty.With(StateFact.HasActiveDirective, true);
            Effects = WorldState.Empty.With(StateFact.AtDirectiveLocation, true);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new WanderIntent(context.AgentId, new PathResponse());
        }

        public override async ValueTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            Vec2 target = GetTargetLocation(context);
            if (_pathfinder == null || context.Snapshot == null)
            {
                return CreateIntent(context);
            }

            var req = new PathRequest
            {
                StartPosition = context.Position,
                TargetPosition = target
            };

            var pathResponse = await _pathfinder.CalculatePathAsync(req, context.Snapshot);
            return new WanderIntent(context.AgentId, pathResponse);
        }

        private Vec2 GetTargetLocation(AgentContext context)
        {
            if (context.Snapshot != null && context.DirectiveMembership.HasGroup &&
                context.Snapshot.TryGetGroup(context.DirectiveMembership.GroupId, out var groupData) &&
                groupData.ActiveDirective.IsActive(context.CurrentTick))
            {
                return groupData.ActiveDirective.TargetLocation;
            }
            return context.Position;
        }
    }
}
