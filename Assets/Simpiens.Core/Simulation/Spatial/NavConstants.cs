namespace Simpiens.Simulation.Spatial
{
    /// <summary>
    /// Shared pawn navigation dimensions. The motor (SimulationManager), the static grid rasterizer
    /// (SpatialHashGrid) and the PlayMode anomaly detector must agree on these, or "free" cells can still overlap walls.
    /// </summary>
    public static class NavConstants
    {
        /// <summary>
        /// World-space pawn body radius (agent collider 0.35 at 0.7 scale). Walls are rasterized into the static grid
        /// inflated by this radius, so a pawn whose centre stays in a walkable cell never overlaps wall geometry.
        /// </summary>
        public const float PawnBodyRadius = 0.25f;

        /// <summary>
        /// Minimum centre distance a moving pawn keeps from other pawns (anomaly detector flags stacking below 0.35m).
        /// Checked against the previous tick's snapshot positions, hence the margin.
        /// </summary>
        public const float PawnSeparation = 0.4f;

        /// <summary>
        /// When the final waypoint is occupied by another pawn, a pawn this close to it counts as arrived
        /// instead of pushing into the occupant.
        /// </summary>
        public const float ArrivalTolerance = 1.0f;

        /// <summary>
        /// Half extent of the playable area around the origin. Nav cells lying outside it are statically blocked, so
        /// pawns can't wander to the edge of an oversized spatial grid. The anomaly detector uses the same bound.
        /// </summary>
        public const float PlayAreaHalfExtent = 20f;
    }
}
