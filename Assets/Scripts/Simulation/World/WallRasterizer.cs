using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Simulation.World
{
    /// <summary>
    /// Writes a map definition's static walls into a <see cref="StaticNavGrid"/> base layer. Pure, headless, allocation-free.
    /// Walls are inflated by the pawn body radius (as dynamic walls are in SpatialHashGrid), so a pawn whose centre
    /// stays in a walkable cell never overlaps wall geometry.
    /// </summary>
    public static class WallRasterizer
    {
        public static float Inflation => NavConstants.PawnBodyRadius;

        /// <summary>
        /// Replaces the grid's base layer with the definition's walls and applies its playable bounds.
        /// Region labels are not rebuilt here; call <see cref="StaticNavGrid.EnsureRegions"/> afterwards.
        /// </summary>
        public static void Rasterize(MapDefinition definition, StaticNavGrid grid)
        {
            if (definition == null || grid == null) return;

            grid.ClearBaseLayer();

            var walls = definition.Walls;
            for (int i = 0; i < walls.Count; i++)
            {
                MarkRect(walls[i].Solid, grid);
            }

            // Bounds first, then Clear(): walkability is recomputed from the base layer and the new bounds only,
            // dropping dynamic walls and any cells the previous bounds had blocked
            grid.SetPlayableBounds(definition.PlayableBounds);
            grid.Clear();
        }

        private static void MarkRect(Rect solid, StaticNavGrid grid)
        {
            float inflate = Inflation;
            float cs = grid.CellSize;
            int minX = Mathf.Max(0, Mathf.FloorToInt((solid.xMin - inflate + grid.OffsetX) / cs));
            int maxX = Mathf.Min(grid.Width - 1, Mathf.FloorToInt((solid.xMax + inflate + grid.OffsetX) / cs));
            int minY = Mathf.Max(0, Mathf.FloorToInt((solid.yMin - inflate + grid.OffsetY) / cs));
            int maxY = Mathf.Min(grid.Height - 1, Mathf.FloorToInt((solid.yMax + inflate + grid.OffsetY) / cs));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    grid.SetBaseWall(x, y);
                }
            }
        }

        /// <summary>
        /// True when a circle overlaps any static wall of the definition (exact geometry, no inflation).
        /// Used by diagnostics; allocation-free.
        /// </summary>
        public static bool CircleOverlapsWall(MapDefinition definition, Vector2 center, float radius)
        {
            if (definition == null) return false;
            var walls = definition.Walls;
            float r2 = radius * radius;
            for (int i = 0; i < walls.Count; i++)
            {
                var rect = walls[i].Solid;
                float cx = Mathf.Clamp(center.x, rect.xMin, rect.xMax);
                float cy = Mathf.Clamp(center.y, rect.yMin, rect.yMax);
                float dx = center.x - cx;
                float dy = center.y - cy;
                if (dx * dx + dy * dy < r2) return true;
            }
            return false;
        }
    }
}
