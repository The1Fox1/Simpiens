using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Represents an intent to travel into a bounty's target sector. The ScoutIntentHandler completes it
    /// the moment the agent physically stands inside the sector and records a scout report on the agent.
    /// </summary>
    public class ScoutIntent : AgentIntent
    {
        public readonly ushort BountyId;
        public readonly byte TargetSectorId;
        public readonly PathResponse Path;

        public ScoutIntent(UnityEngine.GUID agentId, ushort bountyId, byte targetSectorId, PathResponse path) : base(agentId)
        {
            BountyId = bountyId;
            TargetSectorId = targetSectorId;
            Path = path;
        }
    }
}
