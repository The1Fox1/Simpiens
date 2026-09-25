using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Cognition.Evaluators
{
    /// <summary>
    /// Tier 1.5 Cognitive Evaluator that assesses altruistic food sharing for companions in distress.
    /// Evaluates off-thread using read-only spatial snapshots and dyadic affinity scores.
    /// </summary>
    public class AltruismEvaluator : ICognitiveEvaluator
    {
        public const float AltruismThreshold = 25f;
        public const float SatedHungerThreshold = 50f;
        public const float DistressHungerThreshold = 85f;
        public const float PerceptionRadius = 15f;

        private readonly IAsyncPathfinder _pathfinder;

        [System.ThreadStatic]
        private static List<EntitySnapshot> _peerResults;

        public AltruismEvaluator(IAsyncPathfinder pathfinder)
        {
            _pathfinder = pathfinder;
        }

        public async UniTask<AgentIntent> EvaluateAsync(AgentContext context, CancellationToken ct)
        {
            // Self-preservation: an agent with Hunger >= 50 will not share food
            if (context.Hunger >= SatedHungerThreshold)
            {
                return null;
            }

            if (context.Snapshot == null || context.Memory == null)
            {
                return null;
            }

            if (_peerResults == null)
            {
                _peerResults = new List<EntitySnapshot>(16);
            }
            _peerResults.Clear();

            context.Snapshot.GetEntitiesInRange(context.Position, PerceptionRadius, _peerResults);

            GUID bestTargetId = default;
            Vector2 bestTargetPos = Vector2.zero;
            float highestUtility = -1f;

            int count = _peerResults.Count;
            for (int i = 0; i < count; i++)
            {
                var peer = _peerResults[i];
                if (peer.Id == context.AgentId || peer.Type != EntityType.Pawn) continue;

                // Check if peer is in distress (critically starving or collapsed)
                bool isDistressed = peer.Hunger >= DistressHungerThreshold || peer.IsCollapsed;
                if (!isDistressed) continue;

                float utility = context.Memory.CalculateAltruismScore(peer.Id, context.CurrentTick, context.Hunger, peer.IsCollapsed);
                if (utility >= AltruismThreshold && utility > highestUtility)
                {
                    highestUtility = utility;
                    bestTargetId = peer.Id;
                    bestTargetPos = peer.Position;
                }
            }

            if (bestTargetId != default)
            {
                // Defensive null-safety for isolated tests
                if (_pathfinder == null)
                {
                    return new ShareResourceIntent(context.AgentId, bestTargetId, default);
                }

                var pathRequest = new PathRequest
                {
                    StartPosition = context.Position,
                    TargetPosition = bestTargetPos
                };

                var pathResponse = await _pathfinder.CalculatePathAsync(pathRequest, context.Snapshot);
                if (pathResponse.IsValid)
                {
                    return new ShareResourceIntent(context.AgentId, bestTargetId, pathResponse);
                }

                pathResponse.ReturnToPool();
            }

            return null;
        }
    }
}
