namespace Simpiens.Simulation.Spatial
{
    public enum EntityType
    {
        Walkable, // Represents an empty/walkable cell if needed
        Pawn,
        Resource,
        Wall,
        Anchor,
        Stall,
        Bunk, // a bed: walkable, one sleeper (Epic MAP Phase 3)
        GuildBunk, // a bunk in the guild's barracks, kept for a member (Epic 10 Part 2, W5.2, D62)
        Creature, // a creature of a lair (Epic 10 Part 3, K2.1): its state is the entity's Creature
        Pile // items on the ground, a kill's drop or a body (Epic 10 Part 3, K4.1, A5): its state is the entity's Pile
    }
}
