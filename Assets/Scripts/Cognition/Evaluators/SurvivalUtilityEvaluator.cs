using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Cognition.Planning;

namespace Simpiens.Cognition.Evaluators
{
    /// <summary>
    /// Tier-1 survival reflex. Fires only for genuine hunger (>= HighHungerThreshold) when a reachable
    /// food node is remembered; otherwise returns null so control passes to Tier-2 GOAP.
    /// Ambient idle/wander lives in <see cref="AmbientWanderEvaluator"/> as the Tier-3 fallback.
    /// </summary>
    public class SurvivalUtilityEvaluator : ICognitiveEvaluator
    {
        private readonly IAsyncPathfinder _pathfinder;

        public const float HighHungerThreshold = 60f;
        public const float MaxResourceEngagementDistance = 8.0f;

        public SurvivalUtilityEvaluator(IAsyncPathfinder pathfinder)
        {
            _pathfinder = pathfinder;
        }

        public async UniTask<AgentIntent> EvaluateAsync(AgentContext context, CancellationToken ct)
        {
            if (context.Hunger < HighHungerThreshold) return null;

            // A carried ration comes first: eat it on the spot instead of walking to a plot
            if (CarriesFood(context)) return new EatCarriedFoodIntent(context.AgentId);

            // Only food nodes qualify: a starving agent must never path to an ore vein to "eat" it.
            if (!ResourceTargeting.TrySelectFood(context, MaxResourceEngagementDistance, out var food)) return null;

            // Defensive null-safety for isolated tests
            if (_pathfinder == null)
            {
                return new HarvestResourceIntent(context.AgentId, food.EntityId, default);
            }

            var request = new PathRequest
            {
                StartPosition = context.Position,
                TargetPosition = food.LastKnownLocation,
                RequesterId = context.AgentId
            };

            var pathResponse = await _pathfinder.CalculatePathAsync(request, context.Snapshot);

            // Path must not only be valid, but must actually reach within harvest proximity of the target
            if (ResourceTargeting.IsReachable(pathResponse, food.LastKnownLocation))
            {
                return new HarvestResourceIntent(context.AgentId, food.EntityId, pathResponse);
            }

            // Unreachable or wall-blocked: defer to GOAP rather than committing to a doomed reflex
            pathResponse?.ReturnToPool();
            return null;
        }

        public static bool CarriesFood(in AgentContext context) =>
            context.Inventory != null && context.Inventory.HasItem(Simpiens.Simulation.Economy.ItemType.Ration);
    }
}
