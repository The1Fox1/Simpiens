using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using UnityEngine;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Approaches and transfers food to a companion in need.
    /// Preconditions: None.
    /// Effects: HasAidedPeer = true.
    /// Targets the distressed companion chosen by <see cref="AltruismTargeting"/>; only valid while one exists.
    /// </summary>
    public class ShareFoodAction : GoapAction
    {
        /// <summary>The share handler transfers food once within this distance of the peer.</summary>
        public const float AidReach = 1.5f;

        private readonly IAsyncPathfinder _pathfinder;

        public ShareFoodAction(float cost = 1.5f, IAsyncPathfinder pathfinder = null) : base("ShareFood", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty;
            Effects = WorldState.Empty
                .With(StateFact.HasAidedPeer, true);
        }

        public override bool IsValid(AgentContext context)
        {
            return AltruismTargeting.TrySelectDistressedPeer(context, out _, out _, out _);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            // Without a peer the handler reports TargetMissing and the plan is aborted
            AltruismTargeting.TrySelectDistressedPeer(context, out var peerId, out _, out _);
            return new ShareResourceIntent(context.AgentId, peerId, null);
        }

        public override async UniTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (_pathfinder == null || !AltruismTargeting.TrySelectDistressedPeer(context, out var peerId, out var peerPosition, out _))
            {
                return CreateIntent(context);
            }

            var request = new PathRequest
            {
                StartPosition = context.Position,
                TargetPosition = peerPosition,
                RequesterId = context.AgentId
            };

            var path = await _pathfinder.CalculatePathAsync(request, context.Snapshot);
            if (path != null && path.IsValid && path.Length > 0 &&
                Vector2.Distance(path.Waypoints[path.Length - 1], peerPosition) <= AidReach)
            {
                return new ShareResourceIntent(context.AgentId, peerId, path);
            }

            path?.ReturnToPool();
            return new ShareResourceIntent(context.AgentId, peerId, null);
        }
    }
}
