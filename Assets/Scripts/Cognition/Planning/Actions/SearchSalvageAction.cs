using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation.Economy;
using UnityEngine;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Explores to discover salvage (ore/herb) nodes.
    /// Preconditions: None.
    /// Effects: KnowsSalvageLocation = true, AtTradeStall = false (walks away to explore).
    /// Heads for the claimed (or first open) Gather bounty's target location when one is known, since the
    /// board advertises where the salvage is; otherwise walks to a deterministic exploration waypoint.
    /// </summary>
    public class SearchSalvageAction : GoapAction
    {
        /// <summary>Once this close to the advertised location without finding salvage, explore instead.</summary>
        public const float ArrivalRadius = 2.0f;

        private readonly IAsyncPathfinder _pathfinder;

        public SearchSalvageAction(IAsyncPathfinder pathfinder = null, float cost = 3.0f) : base("SearchSalvage", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty;
            Effects = WorldState.Empty.With(StateFact.KnowsSalvageLocation, true).With(StateFact.AtTradeStall, false);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            return new WanderIntent(context.AgentId, new PathResponse());
        }

        public override UniTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (_pathfinder == null)
            {
                return UniTask.FromResult(CreateIntent(context));
            }

            return ResourceTargeting.BuildTravelIntentAsync(context, _pathfinder, ChooseDestination(context));
        }

        public static Vector2 ChooseDestination(in AgentContext context)
        {
            Vector2 advertised = default;
            if (BountyQueries.TryGetClaimedBounty(context, out var claimed) && claimed.Type == BountyType.GatherResource)
            {
                advertised = claimed.TargetLocation;
            }
            else if (BountyQueries.TryGetOpenBounty(context, BountyType.GatherResource, out var open))
            {
                advertised = open.TargetLocation;
            }

            if (advertised != default && (advertised - context.Position).sqrMagnitude > ArrivalRadius * ArrivalRadius)
            {
                return advertised;
            }

            return ResourceTargeting.PickExplorationPoint(context);
        }
    }
}
