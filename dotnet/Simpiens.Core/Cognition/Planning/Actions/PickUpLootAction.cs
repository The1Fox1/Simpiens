using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic 10 Part 3, K4.3d): walks to the pile it would take from (<see cref="LootTargeting.TryChoosePile"/>) and
    /// picks it over. Preconditions: none. Effects: LootCollected = true.
    /// </summary>
    public class PickUpLootAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public PickUpLootAction(IAsyncPathfinder pathfinder = null, float cost = 1.0f) : base("PickUpLoot", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty;
            Effects = WorldState.Empty.With(StateFact.LootCollected, true);
        }

        public override bool IsValid(AgentContext context) => LootTargeting.TryChoosePile(context, out _);

        public override AgentIntent CreateIntent(AgentContext context)
        {
            if (!LootTargeting.TryChoosePile(context, out var pile)) return new IdleIntent(context.AgentId, duration: 1.0f);
            return new PickUpIntent(context.AgentId, pile.Id, pile.Position, null);
        }

        public override async ValueTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (!LootTargeting.TryChoosePile(context, out var pile)) return new IdleIntent(context.AgentId, duration: 1.0f);
            if (_pathfinder == null || context.Snapshot == null || (pile.Position - context.Position).SqrMagnitude <= LootTargeting.Reach * LootTargeting.Reach)
            {
                return new PickUpIntent(context.AgentId, pile.Id, pile.Position, null);
            }
            var path = await _pathfinder.CalculatePathAsync(new PathRequest { StartPosition = context.Position, TargetPosition = pile.Position, RequesterId = context.AgentId }, context.Snapshot);
            if (ResourceTargeting.IsReachable(path, pile.Position)) return new PickUpIntent(context.AgentId, pile.Id, pile.Position, path);
            path?.ReturnToPool();
            return new IdleIntent(context.AgentId, duration: 1.0f); // seen but out of reach: it decides afresh
        }
    }
}
