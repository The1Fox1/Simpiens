namespace Simpiens.Testing.Diagnostics
{
    /// <summary>
    /// Classifies visual, physical, and state glitches detected during runtime simulation execution.
    /// </summary>
    public enum VisualAnomalyType
    {
        None = 0,

        /// <summary>
        /// Pawn heading reverses rapidly (>120 degrees) multiple times in under 0.5s (visual vibration/jitter).
        /// </summary>
        OscillationJitter = 1,

        /// <summary>
        /// Two or more non-interacting pawns overlap within a tight radius for extended duration (sprite clumping).
        /// </summary>
        PawnStacking = 2,

        /// <summary>
        /// Pawn collider bounds penetrate a static obstacle or wall geometry.
        /// </summary>
        ObstaclePenetration = 3,

        /// <summary>
        /// Rapid flipping between visual states (e.g. Panic <-> Yield <-> Walk in < 0.5s), causing sprite flickering.
        /// </summary>
        VisualStateThrashing = 4,

        /// <summary>
        /// Pawn position escapes beyond simulation boundary bounds.
        /// </summary>
        BoundaryBreach = 5
    }
}
