using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Cognition.Evaluators
{
    public class SurvivalUtilityEvaluator : ICognitiveEvaluator
    {
        private readonly IAsyncPathfinder _pathfinder;

        // Hysteresis thresholds to prevent rapid state flipping
        private const float HighHungerThreshold = 60f;
        private const float LowHungerThreshold = 20f;

        // Caching the state across ticks per agent could be done by passing previous intent, 
        // but for now we evaluate based purely on context. If hunger > High, utility goes up.
        // We will simulate hysteresis by increasing the utility dramatically if hunger is high.

        // We need a thread-local list to avoid allocations during spatial queries
        [System.ThreadStatic]
        private static List<EntitySnapshot> _queryResults;

        public SurvivalUtilityEvaluator(IAsyncPathfinder pathfinder)
        {
            _pathfinder = pathfinder;
        }

        public async UniTask<AgentIntent> EvaluateAsync(AgentContext context, CancellationToken ct)
        {
            // The _queryResults list is no longer used here, but kept if needed by other logic.
            // Querying spatial memory doesn't allocate.

            // Simple hysteresis: if Hunger is very low, we don't even consider eating.
            if (context.Hunger < LowHungerThreshold)
            {
                // Active Idling: pick a random location 3-5 tiles away
                return await GetWanderIntentAsync(context);
            }

            // Score calculation
            // Base utility from hunger
            float baseUtility = context.Hunger;

            // Query nearby entities from memory
            Simpiens.Cognition.Memory.SpatialMemoryRecord? bestResource = null;
            float highestUtility = -1f;

            foreach (var kvp in context.Memory.SpatialMemoryMap)
            {
                var record = kvp.Value;
                if (record.Type == EntityType.Resource)
                {
                    Vector2 recordPos = new Vector2(record.LastKnownLocation.x, record.LastKnownLocation.y);
                    float dist = Vector2.Distance(context.Position, recordPos);

                    // The closer it is, the higher the utility. 
                    // If hunger is above HighHungerThreshold, we drastically boost the score to commit.
                    float hysteresisBoost = context.Hunger > HighHungerThreshold ? 50f : 0f;

                    // Simple utility formula: Hunger + Boost - (Distance * Weight)
                    float utility = baseUtility + hysteresisBoost - (dist * 2.0f);

                    if (utility > highestUtility && utility > 0)
                    {
                        highestUtility = utility;
                        bestResource = record;
                    }
                }
            }

            if (bestResource.HasValue)
            {
                // Calculate path to verify reachability
                var pathRequest = new PathRequest
                {
                    StartPosition = context.Position,
                    TargetPosition = new Vector2(bestResource.Value.LastKnownLocation.x, bestResource.Value.LastKnownLocation.y)
                };

                var pathResponse = await _pathfinder.CalculatePathAsync(pathRequest, context.Snapshot);

                if (pathResponse.IsValid)
                {
                    return new HarvestResourceIntent(context.AgentId, bestResource.Value.EntityId, pathResponse);
                }
                else
                {
                    // Clean up invalid path
                    pathResponse.ReturnToPool();
                }
            }

            // Fallback
            return await GetWanderIntentAsync(context);
        }

        [System.ThreadStatic]
        private static System.Random _random;

        private async UniTask<AgentIntent> GetWanderIntentAsync(AgentContext context)
        {
            if (_random == null) _random = new System.Random();

            // Pick a random point 3 to 5 units away using thread-safe System.Random
            float angle = (float)(_random.NextDouble() * Mathf.PI * 2);
            float distance = 3f + (float)(_random.NextDouble() * 2f);
            
            Vector2 randomDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 targetPos = context.Position + randomDir * distance;

            // Optional: if the spatial grid has bounds, we could clamp this.
            // For now, let the pathfinder validate it.

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
            return new IdleIntent(context.AgentId);
        }
    }
}
