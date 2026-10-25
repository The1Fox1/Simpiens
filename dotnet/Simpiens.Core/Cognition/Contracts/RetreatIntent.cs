using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// A hurt crawler heads for a bunk (Epic 10 G2): out of a hazard sector when its health runs low, or limping home
    /// when it is downed (at a fraction of its speed). A reflex: it pre-empts whatever the crawler was doing.
    /// </summary>
    public class RetreatIntent : AgentIntent
    {
        public readonly PathResponse Path;

        public RetreatIntent(EntityId agentId, PathResponse path) : base(agentId)
        {
            Path = path;
        }
    }
}
