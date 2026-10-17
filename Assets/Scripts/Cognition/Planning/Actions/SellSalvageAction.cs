using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Sells raw salvage (Iron Ore or Medicinal Herbs) to an outpost trade stall.
    /// Preconditions: AtTradeStall = true, HasSalvageInInventory = true.
    /// Effects: HasSalvageInInventory = false, HasSoldSalvage = true.
    /// </summary>
    public class SellSalvageAction : GoapAction
    {
        public SellSalvageAction(float cost = 1.0f) : base("SellSalvage", cost)
        {
            Preconditions = WorldState.Empty
                .With(StateFact.AtTradeStall, true)
                .With(StateFact.HasSalvageInInventory, true);

            Effects = WorldState.Empty
                .With(StateFact.HasSalvageInInventory, false)
                .With(StateFact.HasSoldSalvage, true);
        }

        /// <summary>
        /// Plannable when the reachable stall can pay for at least the cheapest salvage. At execution the agent
        /// sells the held item the treasury can afford (see CreateIntent).
        /// </summary>
        public override bool IsValid(AgentContext context)
        {
            return ResourceTargeting.NearestStallCanBuySalvage(context);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            EntityId stallId = default;

            if (context.Snapshot != null && context.Snapshot.ActiveStallCount > 0)
            {
                // Nearest stall the agent can actually walk to
                if (ResourceTargeting.TryGetNearestStall(context, out var reachable))
                {
                    stallId = reachable.StallEntityId;
                }
            }
            else if (context.Memory != null && context.Memory.SpatialMemoryMap != null)
            {
                foreach (var kvp in context.Memory.SpatialMemoryMap)
                {
                    if (kvp.Value.Type == EntityType.Stall)
                    {
                        stallId = kvp.Key;
                        break;
                    }
                }
            }

            // Determine what salvage item the agent is carrying
            ItemType salvageType = ItemType.IronOre;
            ushort quantity = 1;

            if (context.Inventory != null)
            {
                // Prefer the held item the stall can actually pay for
                ItemType affordable = ResourceTargeting.TryGetNearestStall(context, out var reachable)
                    ? ResourceTargeting.AffordableHeldSalvage(context, reachable)
                    : ItemType.None;

                if (affordable != ItemType.None)
                {
                    salvageType = affordable;
                }
                else if (ResourceTargeting.HoldsSellable(context, ItemType.IronOre))
                {
                    salvageType = ItemType.IronOre;
                }
                else if (ResourceTargeting.HoldsSellable(context, ItemType.MedicinalHerb))
                {
                    salvageType = ItemType.MedicinalHerb;
                }
                else
                {
                    // Everything held is owed to a claimed bounty: nothing to sell
                    return new IdleIntent(context.AgentId, 0.5f);
                }
            }

            return new TradeIntent(context.AgentId, stallId, TradeActionType.SellItem, salvageType, quantity);
        }
    }
}
