namespace Simpiens.Cognition.Groups
{
    /// <summary>
    /// Hierarchical standing of an agent within a collective group.
    /// Determines command rights and succession priority.
    /// </summary>
    public enum RoleRank : byte
    {
        None     = 0, // Unaligned or non-member
        Follower = 1, // Rank-and-file member; executes directives
        Officer  = 2, // Sub-commander; succession priority and local coordination
        Leader   = 3  // Supreme authority (Chieftain, Guildmaster); issues directives
    }
}
