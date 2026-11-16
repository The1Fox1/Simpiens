using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action (Epic 10 Part 3, K6.2): joins the hunt it would choose (<see cref="Hunting.TryChoose"/>), signing at the board.
    /// Preconditions: HasHuntBounty = false. Effects: HasHuntBounty = true.
    /// </summary>
    public class ClaimHuntAction : GoapAction
    {
        public ClaimHuntAction(float cost = 1.0f) : base("ClaimHunt", cost)
        {
            Preconditions = WorldState.Empty.With(StateFact.HasHuntBounty, false);
            Effects = WorldState.Empty.With(StateFact.HasHuntBounty, true);
        }

        public override bool IsValid(AgentContext context) => Hunting.TryChoose(context, out _, out _);

        public override AgentIntent CreateIntent(AgentContext context)
        {
            ushort id = Hunting.TryChoose(context, out var hunt, out _) ? hunt.BountyId : (ushort)0;
            return new ClaimBountyIntent(context.AgentId, id);
        }
    }

    /// <summary>
    /// Action (K6.2): goes after its hunt's lair (<see cref="HuntIntent"/>): the walk toward the lair's heart, then its creatures.
    /// Preconditions: HasHuntBounty = true. Effects: LairHunted = true.
    /// </summary>
    public class HuntLairAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        /// <summary>How far short of the lair's heart the walk stops: its creatures are met on the way in.</summary>
        public const float ApproachDistance = 1.5f;

        public HuntLairAction(IAsyncPathfinder pathfinder = null, float cost = 1.0f) : base("HuntLair", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty.With(StateFact.HasHuntBounty, true);
            Effects = WorldState.Empty.With(StateFact.LairHunted, true);
        }

        /// <summary>In a hunt, or about to join one: the planner judges a chain before its first step runs (the claim comes first).</summary>
        public override bool IsValid(AgentContext context) => Hunting.TryGetJoined(context, out _) || Hunting.TryChoose(context, out _, out _);

        public override AgentIntent CreateIntent(AgentContext context)
        {
            Hunting.TryGetJoined(context, out var hunt);
            return new HuntIntent(context.AgentId, hunt.BountyId, hunt.LairIndex, null);
        }

        public override async ValueTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (!Hunting.TryGetJoined(context, out var hunt)) return new IdleIntent(context.AgentId, duration: 1.0f);
            var map = context.Snapshot?.Map;
            if (_pathfinder == null || map == null || hunt.LairIndex >= map.Lairs.Count) return CreateIntent(context);
            var heart = map.Lairs[hunt.LairIndex].Position;
            var toward = context.Position - heart;
            var target = toward.SqrMagnitude > ApproachDistance * ApproachDistance ? heart + toward.Normalized * ApproachDistance : context.Position;
            var path = await _pathfinder.CalculatePathAsync(new PathRequest { StartPosition = context.Position, TargetPosition = target, RequesterId = context.AgentId }, context.Snapshot);
            if (path != null && path.IsValid) return new HuntIntent(context.AgentId, hunt.BountyId, hunt.LairIndex, path);
            path?.ReturnToPool();
            return new HuntIntent(context.AgentId, hunt.BountyId, hunt.LairIndex, null); // in the lair already, or no way: it fights what it meets
        }
    }
}
