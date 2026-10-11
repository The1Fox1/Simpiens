using UnityEngine;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Represents an intent to deliver the required items for an active bounty contract
    /// to an outpost trade stall, collecting the reward and boosting affinity with the player.
    /// </summary>
    public class DeliverBountyIntent : AgentIntent
    {
        public readonly ushort BountyId;
        public readonly GUID StallEntityId;

        public DeliverBountyIntent(GUID agentId, ushort bountyId, GUID stallEntityId) : base(agentId)
        {
            BountyId = bountyId;
            StallEntityId = stallEntityId;
        }
    }
}
