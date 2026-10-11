using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Harvests salvage (IronOre / MedicinalHerb) from the best known salvage node.
    /// Preconditions: KnowsSalvageLocation = true.
    /// Effects: HasSalvageInInventory = true, HasBountyItem = true, AtTradeStall = false (walks to the node).
    /// With a claimed Gather bounty, only its TargetItemType is considered and enough units are harvested in place
    /// to cover the remaining quantity; otherwise the node is chosen by stall buy price ÷ distance.
    /// </summary>
    public class GatherSalvageAction : GoapAction
    {
        private readonly IAsyncPathfinder _pathfinder;

        public GatherSalvageAction(IAsyncPathfinder pathfinder = null, float cost = 2.0f) : base("GatherSalvage", cost)
        {
            _pathfinder = pathfinder;
            Preconditions = WorldState.Empty.With(StateFact.KnowsSalvageLocation, true);
            Effects = WorldState.Empty
                .With(StateFact.HasSalvageInInventory, true)
                .With(StateFact.HasBountyItem, true)
                .With(StateFact.AtTradeStall, false);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            if (!TrySelectTarget(context, out var target, out _))
            {
                return ResourceTargeting.MissingTargetIntent(context);
            }
            return new HarvestResourceIntent(context.AgentId, target.EntityId, null);
        }

        public override UniTask<AgentIntent> CreateIntentAsync(AgentContext context, CancellationToken ct = default)
        {
            if (!TrySelectTarget(context, out var target, out int units))
            {
                return UniTask.FromResult(ResourceTargeting.MissingTargetIntent(context));
            }
            return ResourceTargeting.BuildHarvestIntentAsync(context, _pathfinder, target, units);
        }

        private static bool TrySelectTarget(in AgentContext context, out Simpiens.Cognition.Memory.SpatialMemoryRecord target, out int units)
        {
            ItemType required = ItemType.None;
            units = 1;

            if (BountyQueries.TryGetClaimedBounty(context, out var claimed) && claimed.Type == BountyType.GatherResource && claimed.TargetItemType.IsSalvage())
            {
                required = claimed.TargetItemType;
                int held = context.Inventory != null ? context.Inventory.GetItemCount(required) : 0;
                units = claimed.TargetQuantity - held;
                if (units < 1) units = 1;
            }

            return ResourceTargeting.TrySelectSalvage(context, required, out target);
        }
    }
}
