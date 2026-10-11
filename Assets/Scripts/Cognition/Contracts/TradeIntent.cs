using Simpiens.Simulation.Economy;
using UnityEngine;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Represents an intent to execute a commercial trade (buying rations or selling salvage)
    /// at an outpost trade stall.
    /// </summary>
    public class TradeIntent : AgentIntent
    {
        public readonly GUID StallEntityId;
        public readonly TradeActionType ActionType;
        public readonly ItemType ItemType;
        public readonly ushort Quantity;
        public readonly ushort MaxExpectedPricePerUnit;

        public TradeIntent(
            GUID agentId,
            GUID stallEntityId,
            TradeActionType actionType,
            ItemType itemType,
            ushort quantity = 1,
            ushort maxExpectedPricePerUnit = ushort.MaxValue) : base(agentId)
        {
            StallEntityId = stallEntityId;
            ActionType = actionType;
            ItemType = itemType;
            Quantity = quantity;
            MaxExpectedPricePerUnit = maxExpectedPricePerUnit;
        }
    }
}
