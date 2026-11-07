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
        GuildBunk // a bunk in the guild's barracks, kept for a member (Epic 10 Part 2, W5.2, D62)
    }
}
