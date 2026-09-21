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
    /// Tier 1 Reflexive Evaluator that handles severe biological emergencies and mental breaks:
    /// 1. Exhaustion Collapse (Energy <= 5f): Forces emergency resting recovery.
    /// 2. Starvation Panic (Hunger >= 85f and no known food): Clears stale memory and triggers exploratory sprint.
    /// </summary>
    public class MentalBreakEvaluator : ICognitiveEvaluator
    {
        public const float StarvationThreshold = 85f;
        public const float ExhaustionThreshold = 5f;

        private readonly IAsyncPathfinder _pathfinder;

        [System.ThreadStatic]
        private static System.Random _random;

        [System.ThreadStatic]
        private static List<UnityEngine.GUID> _staleResourceKeys;

        public MentalBreakEvaluator(IAsyncPathfinder pathfinder)
        {
            _pathfinder = pathfinder;
        }

        public async UniTask<AgentIntent> EvaluateAsync(AgentContext context, CancellationToken ct)
        {
            // 1. Exhaustion Collapse Reflex
            if (context.Energy <= ExhaustionThreshold)
            {
                // Force an emergency rest intent on the spot (2s rest)
                return new IdleIntent(context.AgentId, 2.0f);
            }

            // 2. Starvation Panic Reflex
            if (context.Hunger >= StarvationThreshold)
            {
                bool hasKnownFood = HasKnownFood(context);

                if (!hasKnownFood)
                {
                    // Prune stale resource records from memory so agent explores anew
                    PruneStaleResourceMemories(context);

                    // Defensive null safety: If pathfinder is null (e.g. isolated unit testing), return fallback PanicIntent
                    if (_pathfinder == null)
                    {
                        return new PanicIntent(context.AgentId, default);
                    }

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
            }

            // Neither emergency condition met; hand off to Tier 2 GOAP planning
            return null;
        }

        public static bool HasKnownFood(in AgentContext context)
        {
            if (context.Memory == null || context.Memory.SpatialMemoryMap == null) return false;

            foreach (var kvp in context.Memory.SpatialMemoryMap)
            {
                var record = kvp.Value;
                if (record.Type == EntityType.Resource && !context.Memory.IsBlacklisted(record.EntityId, context.CurrentTick))
                {
                    return true;
                }
            }

            return false;
        }

        public static void PruneStaleResourceMemories(in AgentContext context)
        {
            if (context.Memory == null || context.Memory.SpatialMemoryMap == null) return;

            if (_staleResourceKeys == null)
            {
                _staleResourceKeys = new List<UnityEngine.GUID>(16);
            }
            _staleResourceKeys.Clear();

            foreach (var kvp in context.Memory.SpatialMemoryMap)
            {
                if (kvp.Value.Type == EntityType.Resource)
                {
                    _staleResourceKeys.Add(kvp.Key);
                }
            }

            int count = _staleResourceKeys.Count;
            for (int i = 0; i < count; i++)
            {
                context.Memory.SpatialMemoryMap.Remove(_staleResourceKeys[i]);
            }
        }
    }
}
