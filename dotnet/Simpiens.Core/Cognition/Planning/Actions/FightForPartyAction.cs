using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic 10 Part 3, K7.2a, Q8): at a clear's fight, a member fights for its party: the party's focus, else the lair's
    /// nearest creature (a <see cref="HuntIntent"/> with no bounty; its handler reads the focus from the member's order).
    /// Preconditions: HasActiveDirective = true, AtDirectiveWork = true, and the work is a clear's fight. Effects: DirectiveFulfilled = true.
    /// </summary>
    public class FightForPartyAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public FightForPartyAction(IAsyncPathfinder pathfinder = null, float cost = 1.0f) : base("FightForParty", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty.With(StateFact.HasActiveDirective, true).With(StateFact.AtDirectiveWork, true);
            Effects = WorldState.Empty.With(StateFact.DirectiveFulfilled, true);
        }

        public override bool IsValid(AgentContext context) => context.Expedition.IsFighting;

        public override AgentIntent CreateIntent(AgentContext context) => new HuntIntent(context.AgentId, 0, context.Expedition.LairIndex, null);

        public override async ValueTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            var map = context.Snapshot?.Map;
            int lair = context.Expedition.LairIndex;
            if (_pathfinder == null || map == null || lair >= map.Lairs.Count) return CreateIntent(context);
            var heart = map.Lairs[lair].Position;
            var toward = context.Position - heart;
            var target = toward.SqrMagnitude > HuntLairAction.ApproachDistance * HuntLairAction.ApproachDistance ? heart + toward.Normalized * HuntLairAction.ApproachDistance : context.Position;
            var path = await _pathfinder.CalculatePathAsync(new PathRequest { StartPosition = context.Position, TargetPosition = target, RequesterId = context.AgentId }, context.Snapshot);
            if (path != null && path.IsValid) return new HuntIntent(context.AgentId, 0, (byte)lair, path);
            path?.ReturnToPool();
            return CreateIntent(context);
        }
    }
}
