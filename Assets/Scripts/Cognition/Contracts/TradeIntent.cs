using Simpiens.Simulation.Economy;
namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Represents an intent to execute a commercial trade (buying rations or selling salvage)
    /// at an outpost trade stall.
    /// </summary>
    public class TradeIntent : AgentIntent
    {
        public readonly EntityId StallEntityId;
        public readonly TradeActionType ActionType;
        public readonly ItemType ItemType;
        public readonly ushort Quantity;
        public readonly ushort MaxExpectedPricePerUnit;

        public TradeIntent(
            EntityId agentId,
            EntityId stallEntityId,
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
