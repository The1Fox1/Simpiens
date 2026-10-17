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
        Disband         = 4  // Orderly dissolution of the group
    }
}
