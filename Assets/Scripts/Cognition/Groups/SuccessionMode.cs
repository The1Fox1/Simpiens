namespace Simpiens.Cognition.Groups
{
    /// <summary>
    /// Modes of leadership succession based on group cohesion (Epic 8 Phase 3).
    /// </summary>
    public enum SuccessionMode : byte
    {
        /// <summary>
        /// Cohesion >= 50: Orderly institutional succession.
        /// Prioritizes existing Officers; otherwise candidate with highest incoming group-wide respect.
        /// </summary>
        Peaceful = 0,

        /// <summary>
        /// 25 <= Cohesion < 50: Internal power struggle.
        /// Top 2 contenders compare dominance deltas; loser yields (allegiance penalty or departure).
        /// </summary>
        DominanceChallenge = 1,

        /// <summary>
        /// Cohesion < 25: Catastrophic group fragmentation.
        /// Followers split along dyadic warmth/trust lines into two splinter groups.
        /// </summary>
        FactionalSchism = 2
    }
}
