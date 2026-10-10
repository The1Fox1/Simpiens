using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation;
using UnityEngine;

namespace Simpiens.Cognition.Evaluators
{
    /// <summary>
    /// Tier-3 ambient fallback: idles or wanders a short distance when neither the Tier-1 reflexes
    /// nor Tier-2 GOAP produced an intent. Logic moved unchanged from the former SurvivalUtilityEvaluator.
    /// Randomness is seeded per (agent, tick) through <see cref="SimRandom"/>, so a given seed reproduces the same choices.
    /// </summary>
    public class AmbientWanderEvaluator : ICognitiveEvaluator
    {
        private const float IdleChance = 0.45f;

        private readonly IAsyncPathfinder _pathfinder;
        private readonly ulong _seed;

        public AmbientWanderEvaluator(IAsyncPathfinder pathfinder, SimulationSettings settings = null)
        {
            _pathfinder = pathfinder;
            _seed = settings != null ? settings.Seed : SimulationSettings.DefaultSeed;
        }

        public async UniTask<AgentIntent> EvaluateAsync(AgentContext context, CancellationToken ct)
        {
            var rng = SimRandom.For(_seed, context.AgentId, context.CurrentTick, SimRandomSalt.AmbientWander);

            if (rng.NextFloat() < IdleChance || _pathfinder == null)
            {
                return new IdleIntent(context.AgentId, duration: 2.0f);
            }

            // Pick a point 3 to 5 units away
            float angle = rng.NextFloat() * Mathf.PI * 2f;
            float distance = rng.Range(3f, 5f);

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
