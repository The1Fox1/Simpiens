namespace Simpiens.Cognition.Groups
{
    /// <summary>
    /// Categorical classification for collective entities.
    /// Determines lifecycle expectations and default behavioral rules.
    /// </summary>
    public enum GroupType : byte
    {
        TemporaryParty = 0, // Ad-hoc transient collective (hunting party, foraging band)
        FactionGuild   = 1, // Functional occupational collective (craft guild, military order)
        Settlement     = 2  // Macro geographic collective (clan, village, town)
    }
}
