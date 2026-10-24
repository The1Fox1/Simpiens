using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation.World;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: a drifter whose stay is over walks to the nearest Road Gate spawn point and leaves (Epic 10 G1).
    /// Preconditions: none. Effects: HasLeftOutpost = true.
    /// </summary>
    public class LeaveOutpostAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public LeaveOutpostAction(IAsyncPathfinder pathfinder = null, float cost = 1.0f) : base("LeaveOutpost", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty;
            Effects = WorldState.Empty.With(StateFact.HasLeftOutpost, true);
        }

        public override bool IsValid(AgentContext context) => context.StayIsOver && TryGetGate(context, out _);

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new LeaveOutpostIntent(context.AgentId, default);
        }

        public override async ValueTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (_pathfinder == null || !TryGetGate(context, out var gate)) return CreateIntent(context);
            var path = await _pathfinder.CalculatePathAsync(new PathRequest { StartPosition = context.Position, TargetPosition = gate }, context.Snapshot);
            return new LeaveOutpostIntent(context.AgentId, path);
        }

        /// <summary>The map's spawn point nearest the crawler: where strangers come in, and go out.</summary>
        public static bool TryGetGate(in AgentContext context, out Vec2 gate)
        {
            gate = default;
            var map = context.Snapshot?.Map;
            if (map == null) return false;
            float bestSqr = float.MaxValue;
            bool found = false;
            for (int i = 0; i < map.Pois.Count; i++)
            {
                var poi = map.Pois[i];
                if (poi.Kind != PoiKind.SpawnPoint) continue;
                float sqr = (poi.Position - context.Position).SqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                gate = poi.Position;
                found = true;
            }
            return found;
        }
    }
}
