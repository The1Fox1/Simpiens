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
        Bunk // a bed: walkable, one sleeper (Epic MAP Phase 3)
    }
}
