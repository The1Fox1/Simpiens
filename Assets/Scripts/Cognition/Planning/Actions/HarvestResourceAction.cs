using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Harvests the nearest known food node.
    /// Preconditions: AtResourceLocation = true.
    /// Effects: HasFoodInInventory = true, IsHungry = false.
    /// Emits a real HarvestResourceIntent (pathing the last stretch if needed) with the same reachability rule
    /// as the Tier-1 survival reflex. If no food is known at execution time the intent reports TargetMissing,
    /// which aborts the plan so the agent replans.
    /// </summary>
    public class HarvestResourceAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public HarvestResourceAction(float cost = 1.0f, IAsyncPathfinder pathfinder = null) : base("HarvestResource", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty.With(StateFact.AtResourceLocation, true);
            Effects = WorldState.Empty
                .With(StateFact.HasFoodInInventory, true)
                .With(StateFact.IsHungry, false);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            if (!ResourceTargeting.TrySelectFood(context, ResourceTargeting.DefaultFoodSearchDistance, out var food))
            {
                return ResourceTargeting.MissingTargetIntent(context);
            }
            return new HarvestResourceIntent(context.AgentId, food.EntityId, null);
        }

        public override UniTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (!ResourceTargeting.TrySelectFood(context, ResourceTargeting.DefaultFoodSearchDistance, out var food))
            {
                return UniTask.FromResult(ResourceTargeting.MissingTargetIntent(context));
            }
            return ResourceTargeting.BuildHarvestIntentAsync(context, _pathfinder, food);
        }
    }
}
