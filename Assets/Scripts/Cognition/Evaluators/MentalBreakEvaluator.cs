using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Cognition.Evaluators
{
    /// <summary>
    /// Tier 1 Reflexive Evaluator that handles severe biological emergencies and mental breaks:
    /// 1. Exhaustion Collapse (Energy <= 5f): Forces emergency resting recovery.
    /// 2. Starvation Panic (Hunger >= 85f and no known food, carried rations included): Clears stale memory and triggers exploratory sprint.
    /// </summary>
    public class MentalBreakEvaluator : ICognitiveEvaluator
    {
        public const float StarvationThreshold = 85f;
        public const float ExhaustionThreshold = 5f;

        /// <summary>Within this distance of a remembered food plot a starving crawler waits for it to regrow.</summary>
        public const float WaitAtPlotRadius = 1.5f;

        /// <summary>How long one wait at a plot lasts before the crawler re-evaluates (it sees the plot regrow).</summary>
        public const float WaitAtPlotSeconds = 2f;

        private readonly IAsyncPathfinder _pathfinder;
        private readonly ulong _seed;

        [System.ThreadStatic]
        private static List<EntityId> _staleResourceKeys;

        public MentalBreakEvaluator(IAsyncPathfinder pathfinder, SimulationSettings settings = null)
        {
            _pathfinder = pathfinder;
            _seed = settings != null ? settings.Seed : SimulationSettings.DefaultSeed;
        }

        public async ValueTask<AgentIntent> EvaluateAsync(AgentContext context, CancellationToken ct)
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

                    // A crawler that has seen food plots goes back to them and waits for regrowth: exploring away from
                    // the only free food it knows starved a broke crawler while the Garden regrew (review §3.3 D6)
                    var map = context.Snapshot?.Map;
                    if (map != null && Simpiens.Cognition.Planning.ResourceTargeting.TryPickFoodPlotWaypoint(context, map, out var plot))
                    {
                        if ((plot - context.Position).sqrMagnitude <= WaitAtPlotRadius * WaitAtPlotRadius)
                        {
                            return new IdleIntent(context.AgentId, WaitAtPlotSeconds);
                        }
                        if (_pathfinder != null)
                        {
                            var plotPath = await _pathfinder.CalculatePathAsync(new PathRequest { StartPosition = context.Position, TargetPosition = plot }, context.Snapshot);
                            if (plotPath.IsValid && plotPath.Length > 0)
                            {
                                return new PanicIntent(context.AgentId, plotPath);
                            }
                            plotPath.ReturnToPool();
                        }
                    }

                    // Defensive null safety: If pathfinder is null (e.g. isolated unit testing), return fallback PanicIntent
                    if (_pathfinder == null)
                    {
                        return new PanicIntent(context.AgentId, default);
                    }

                    // On a map with rooms, search another charted sector: a few units in a random direction never
                    // leaves the room, so a starving crawler who has not yet seen the Garden would never find food
                    // (Epic MAP Phase 3, Floor 1)
                    if (map != null && Simpiens.Cognition.Planning.ResourceTargeting.TryPickSectorWaypoint(context, map, out var sectorTarget))
                    {
                        var sectorRequest = new PathRequest
                        {
                            StartPosition = context.Position,
                            TargetPosition = sectorTarget
                        };
                        var sectorPath = await _pathfinder.CalculatePathAsync(sectorRequest, context.Snapshot);
                        if (sectorPath.IsValid && sectorPath.Length > 0)
                        {
                            return new PanicIntent(context.AgentId, sectorPath);
                        }
                        sectorPath.ReturnToPool();
                    }

                    var rng = SimRandom.For(_seed, context.AgentId, context.CurrentTick, SimRandomSalt.PanicDirection);
                    float baseAngle = rng.NextFloat() * Mathf.PI * 2f;

                    const int candidateCount = 8;
                    const float angleStep = (Mathf.PI * 2f) / candidateCount;
                    float distance = rng.Range(2.5f, 4.5f); // 2.5 to 4.5 units away (fits within playpens and local bounds)

                    for (int c = 0; c < candidateCount; c++)
                    {
                        float candAngle = baseAngle + c * angleStep;
                        Vector2 candDir = new Vector2(Mathf.Cos(candAngle), Mathf.Sin(candAngle));
                        Vector2 targetPos = context.Position + candDir * distance;

                        var pathRequest = new PathRequest
                        {
                            StartPosition = context.Position,
                            TargetPosition = targetPos
                        };

                        var pathResponse = await _pathfinder.CalculatePathAsync(pathRequest, context.Snapshot);
                        if (pathResponse.IsValid && pathResponse.Length > 0)
                        {
                            return new PanicIntent(context.AgentId, pathResponse);
                        }

                        pathResponse.ReturnToPool();
                    }

                    // Fallback in-place panic intent if boxed in completely, preserving panic state without falling back to GOAP
                    return new PanicIntent(context.AgentId, default);
                }
            }

            // Neither emergency condition met; hand off to Tier 2 GOAP planning
            return null;
        }

        public static bool HasKnownFood(in AgentContext context)
        {
            // A carried ration is known food: eat it instead of panicking (Epic MAP Phase 5)
            if (SurvivalUtilityEvaluator.CarriesFood(context)) return true;

            // A stall selling an affordable ration is known food too: buy instead of panicking
            if (Simpiens.Cognition.Planning.ResourceTargeting.HasStallFood(context)) return true;

            if (context.Memory == null || context.Memory.SpatialMemoryMap == null) return false;

            foreach (var kvp in context.Memory.SpatialMemoryMap)
            {
                var record = kvp.Value;
                // Salvage (ore/herb) nodes are not food: a starving agent who only knows salvage must still panic-search
                if (record.Type == EntityType.Resource && record.ResourceKind.IsFoodYield() && !context.Memory.IsBlacklisted(record.EntityId, context.CurrentTick))
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
                _staleResourceKeys = new List<EntityId>(16);
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
