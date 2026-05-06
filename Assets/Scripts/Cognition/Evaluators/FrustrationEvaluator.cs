using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using UnityEngine;

namespace Simpiens.Cognition.Evaluators
{
    public class FrustrationEvaluator : ICognitiveEvaluator
    {
        private readonly IAsyncPathfinder _pathfinder;
        
        [System.ThreadStatic]
        private static System.Random _random;

        public FrustrationEvaluator(IAsyncPathfinder pathfinder)
        {
            _pathfinder = pathfinder;
        }

        public async UniTask<AgentIntent> EvaluateAsync(AgentContext context, CancellationToken ct)
        {
            if (context.Frustration > 80f)
            {
                // Panic triggers! 
                // Clear the memory map simulating forgetting the confusing area
                context.Memory.SpatialMemoryMap.Clear();

                if (_random == null) _random = new System.Random();
                float angle = (float)(_random.NextDouble() * Mathf.PI * 2);
                float distance = 10f + (float)(_random.NextDouble() * 5f); // 10 to 15 units away
                
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
                    return new PanicIntent(context.AgentId, pathResponse);
                }

                pathResponse.ReturnToPool();
            }

            // Return null to allow the next evaluator in the chain to proceed
            return null;
        }
    }
}
