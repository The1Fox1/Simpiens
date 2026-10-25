using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// A drifter's last intent (Epic 10 G1): walk to the Road Gate and leave the outpost. The LeaveOutpostIntentHandler
    /// follows the path and, at its end, hands the crawler to the simulation's departure (its gold leaves with it).
    /// </summary>
    public class LeaveOutpostIntent : AgentIntent
    {
        public readonly PathResponse Path;

        public LeaveOutpostIntent(EntityId agentId, PathResponse path) : base(agentId)
        {
            Path = path;
        }
    }
}
