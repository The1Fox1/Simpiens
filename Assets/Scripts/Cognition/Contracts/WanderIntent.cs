using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Represents an intent to wander to a specific location to prevent active idling.
    /// </summary>
    public class WanderIntent : AgentIntent
    {
        public readonly PathResponse Path;

        public WanderIntent(UnityEngine.GUID agentId, PathResponse path) : base(agentId)
        {
            Path = path;
        }
    }
}
