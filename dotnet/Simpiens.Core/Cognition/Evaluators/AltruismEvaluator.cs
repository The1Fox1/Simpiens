using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Cognition.Planning;
using Simpiens.Simulation.Spatial;
namespace Simpiens.Cognition.Evaluators
{
    /// <summary>
    /// Tier 1.5 Cognitive Evaluator that assesses altruistic food sharing for companions in distress.
    /// Evaluates off-thread using read-only spatial snapshots and dyadic affinity scores.
    /// </summary>
    public class AltruismEvaluator : ICognitiveEvaluator
    {
        public const float AltruismThreshold = AltruismTargeting.AltruismThreshold;
        public const float SatedHungerThreshold = AltruismTargeting.SatedHungerThreshold;
        public const float DistressHungerThreshold = AltruismTargeting.DistressHungerThreshold;
        public const float PerceptionRadius = AltruismTargeting.PerceptionRadius;

        private readonly IAsyncPathfinder _pathfinder;

        public AltruismEvaluator(IAsyncPathfinder pathfinder)
        {
            _pathfinder = pathfinder;
        }

        public async ValueTask<AgentIntent> EvaluateAsync(AgentContext context, CancellationToken ct)
        {
            // Self-preservation (Hunger >= 50), perception, line of sight and the altruism threshold are
            // applied by the shared selector so the GOAP AidCompanion goal agrees with this reflex
            if (!AltruismTargeting.TrySelectDistressedPeer(context, out var bestTargetId, out var bestTargetPos, out _))
            {
                return null;
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
                if (pathResponse.IsValid && pathResponse.Length > 0)
                {
                    Vec2 finalWaypoint = pathResponse.Waypoints[pathResponse.Length - 1];
                    float distToPeer = Vec2.Distance(finalWaypoint, bestTargetPos);
                    if (distToPeer <= 1.5f)
                    {
                        return new ShareResourceIntent(context.AgentId, bestTargetId, pathResponse);
                    }
                }

                pathResponse.ReturnToPool();
            }

            return null;
        }
    }
}
