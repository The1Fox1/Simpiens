using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using UnityEngine;

namespace Simpiens.Cognition.Evaluators
{
    /// <summary>
    /// Tier-3 ambient fallback: idles or wanders a short distance when neither the Tier-1 reflexes
    /// nor Tier-2 GOAP produced an intent. Logic moved unchanged from the former SurvivalUtilityEvaluator.
    /// NOTE: still uses an unseeded thread-static System.Random (ROADMAP §1A.2 Deviation #5); do not copy.
    /// </summary>
    public class AmbientWanderEvaluator : ICognitiveEvaluator
    {
        private const double IdleChance = 0.45;

        private readonly IAsyncPathfinder _pathfinder;

        [System.ThreadStatic]
        private static System.Random _random;

        public AmbientWanderEvaluator(IAsyncPathfinder pathfinder)
        {
            _pathfinder = pathfinder;
        }

        public async UniTask<AgentIntent> EvaluateAsync(AgentContext context, CancellationToken ct)
        {
            if (_random == null) _random = new System.Random();

            if (_random.NextDouble() < IdleChance || _pathfinder == null)
            {
                return new IdleIntent(context.AgentId, duration: 2.0f);
            }

            // Pick a random point 3 to 5 units away using thread-safe System.Random
            float angle = (float)(_random.NextDouble() * Mathf.PI * 2);
            float distance = 3f + (float)(_random.NextDouble() * 2f);

            Vector2 randomDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 targetPos = context.Position + randomDir * distance;

            var pathRequest = new PathRequest
            {
                StartPosition = context.Position,
                TargetPosition = targetPos
            };

            var pathResponse = await _pathfinder.CalculatePathAsync(pathRequest, context.Snapshot);

            if (pathResponse.IsValid)
            {
                return new WanderIntent(context.AgentId, pathResponse);
            }

            pathResponse.ReturnToPool();
            return new IdleIntent(context.AgentId, duration: 1.5f);
        }
    }
}
