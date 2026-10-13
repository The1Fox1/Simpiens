using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using UnityEngine;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: walks to the nearest remembered free bunk (Epic MAP Phase 3 rest anchor).
    /// Preconditions: KnowsBunkLocation = true.
    /// Effects: AtBunk = true.
    /// Cost grows with distance (Considerations.BunkTravelCost), so a far bunk loses to resting in place.
    /// </summary>
    public class TravelToBunkAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public TravelToBunkAction(IAsyncPathfinder pathfinder = null, float cost = 1.0f) : base("TravelToBunk", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty.With(StateFact.KnowsBunkLocation, true);
            Effects = WorldState.Empty.With(StateFact.AtBunk, true);
        }

        public override float GetCost(AgentContext context)
        {
            float distance = BunkTargeting.TryGetNearestFreeBunk(context, out _, out var position) ? Vector2.Distance(context.Position, position) : 0f;
            return Considerations.BunkTravelCost(BaseCost, distance);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new WanderIntent(context.AgentId, new PathResponse());
        }

        public override UniTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (_pathfinder == null || !BunkTargeting.TryGetNearestFreeBunk(context, out _, out var position))
            {
                return UniTask.FromResult(CreateIntent(context));
            }
            return ResourceTargeting.BuildTravelIntentAsync(context, _pathfinder, position);
        }
    }
}
