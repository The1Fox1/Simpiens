using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// A crawler struck by a creature runs (Epic 10 Part 3, K2.3, D19): toward the bunks, shown panicking, until it stands
    /// clear of every lair's reach and no blow has fallen for <see cref="Vitality.FlightSeconds"/>. A reflex: it pre-empts
    /// whatever the crawler was doing. Nobody fights back until K3.
    /// </summary>
    public class FleeIntent : AgentIntent
    {
        public readonly PathResponse Path;

        public FleeIntent(EntityId agentId, PathResponse path) : base(agentId)
        {
            Path = path;
        }
    }
}
