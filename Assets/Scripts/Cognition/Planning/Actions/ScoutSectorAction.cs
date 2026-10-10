using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Travels into the claimed Scout bounty's target sector.
    /// Preconditions: HasScoutBounty = true.
    /// Effects: HasScoutReport = true, AtTradeStall = false (walks into the sector).
    /// Emits a ScoutIntent pathed to the bounty's TargetLocation; the handler completes on sector entry.
    /// </summary>
    public class ScoutSectorAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public ScoutSectorAction(IAsyncPathfinder pathfinder = null, float cost = 3.0f) : base("ScoutSector", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty.With(StateFact.HasScoutBounty, true);
            Effects = WorldState.Empty.With(StateFact.HasScoutReport, true).With(StateFact.AtTradeStall, false);
        }

        public override bool IsValid(AgentContext context)
        {
            return BountyQueries.TryGetClaimedBounty(context, out var claimed)
                ? claimed.Type == BountyType.ScoutSector
                : BountyQueries.TryGetOpenBounty(context, BountyType.ScoutSector, out _);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            BountyQueries.TryGetClaimedBounty(context, out var claimed);
            return new ScoutIntent(context.AgentId, claimed.BountyId, claimed.TargetSectorId, null);
        }

        public override async UniTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (_pathfinder == null || !BountyQueries.TryGetClaimedBounty(context, out var claimed) || claimed.Type != BountyType.ScoutSector)
            {
                return CreateIntent(context);
            }

            var request = new PathRequest
            {
                StartPosition = context.Position,
                TargetPosition = claimed.TargetLocation,
                RequesterId = context.AgentId
            };

            var pathResponse = await _pathfinder.CalculatePathAsync(request, context.Snapshot);
            if (pathResponse == null || !pathResponse.IsValid)
            {
                pathResponse?.ReturnToPool();
                pathResponse = null;
            }

            return new ScoutIntent(context.AgentId, claimed.BountyId, claimed.TargetSectorId, pathResponse);
        }
    }
}
