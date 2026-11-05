namespace Simpiens.Cognition.Groups
{
    /// <summary>
    /// Type classification for collective group orders.
    /// </summary>
    public enum DirectiveType : byte
    {
        None            = 0,
        RallyAtLocation = 1, // Move to and hold position around target location
        HarvestResource = 2, // Coordinated harvest of target node / resource area
        DefendAnchor    = 3, // Patrol perimeter of the group's home anchor
        Disband         = 4, // Orderly dissolution of the group
        Expedition      = 5, // The guild's party sent out by the player (Epic 10 G5, D9): its target moves with the stage
        WorkStation     = 6  // A station's crew on shift (Epic 10 Part 2, W1.3, D13): work the station, one block at a time
    }
}
