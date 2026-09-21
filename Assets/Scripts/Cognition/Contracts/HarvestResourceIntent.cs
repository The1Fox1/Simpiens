using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Represents an intent to move towards and harvest a specific resource.
    /// </summary>
    public class HarvestResourceIntent : AgentIntent
    {
        public readonly UnityEngine.GUID TargetEntityId;
        public readonly PathResponse Path;
        public readonly float GatherDuration;

        public HarvestResourceIntent(UnityEngine.GUID agentId, UnityEngine.GUID targetEntityId, PathResponse path, float gatherDuration = 1.5f) : base(agentId)
        {
            TargetEntityId = targetEntityId;
            Path = path;
            GatherDuration = gatherDuration;
        }
    }
}
