using Simpiens.Cognition.Contracts;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Cognition.Planning.Actions
{
    /// <summary>
    /// Action: Purchases provisions (Ration) from an outpost trade stall.
    /// Preconditions: AtTradeStall = true.
    /// Effects: HasFoodInInventory = true, HasProvisions = true.
    /// </summary>
    public class BuyProvisionsAction : GoapAction
    {
        public BuyProvisionsAction(float cost = 1.0f) : base("BuyProvisions", cost)
        {
            Preconditions = WorldState.Empty
                .With(StateFact.AtTradeStall, true);

            Effects = WorldState.Empty
                .With(StateFact.HasFoodInInventory, true)
                .With(StateFact.HasProvisions, true);
        }

        /// <summary>
        /// Only plannable when a reachable stall has stock and the agent can afford a ration, so SatiateHunger
        /// falls back to foraging instead of looping on a purchase that will fail.
        /// </summary>
        public override bool IsValid(AgentContext context)
        {
            return ResourceTargeting.CanBuyRation(context);
        }

        public override AgentIntent CreateIntent(AgentContext context)
        {
            GUID stallId = default;

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

            return new TradeIntent(context.AgentId, stallId, TradeActionType.BuyItem, ItemType.Ration, quantity: 1);
        }
    }
}
