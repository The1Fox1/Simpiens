using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// A hunter goes after its hunt's lair (Epic 10 Part 3, K6.2, Q7): the walk toward the lair's heart, then a fight with the
    /// lair's nearest creature, one after another, until the lair is empty or the hunter must break off (the retreat line).
    /// </summary>
    public class HuntIntent : AgentIntent
    {
        public readonly ushort BountyId;
        public readonly byte LairIndex;
        public readonly PathResponse Path;

        public HuntIntent(EntityId agentId, ushort bountyId, byte lairIndex, PathResponse path) : base(agentId)
        {
            BountyId = bountyId;
            LairIndex = lairIndex;
            Path = path;
        }
    }
}
