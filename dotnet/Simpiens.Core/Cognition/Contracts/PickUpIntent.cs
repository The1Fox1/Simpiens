using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// A crawler goes to a pile on the ground and picks it over (Epic 10 Part 3, K4.3d): the walk on its path, then a few game
    /// minutes' stoop, then it takes what the loot rule says it takes (<see cref="Simpiens.Simulation.Loot.LootRules"/>).
    /// </summary>
    public class PickUpIntent : AgentIntent
    {
        public readonly EntityId PileId;
        public readonly Vec2 PilePosition;
        public readonly PathResponse Path;

        public PickUpIntent(EntityId agentId, EntityId pileId, Vec2 pilePosition, PathResponse path) : base(agentId)
        {
            PileId = pileId;
            PilePosition = pilePosition;
            Path = path;
        }
    }
}
