using UnityEngine;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Represents an intent by an agent to claim an open public bounty contract.
    /// Executed atomically by SimulationManager.
    /// </summary>
    public class ClaimBountyIntent : AgentIntent
    {
        public readonly ushort BountyId;

        public ClaimBountyIntent(GUID agentId, ushort bountyId) : base(agentId)
        {
            BountyId = bountyId;
        }
    }
}
