using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer;

namespace Simpiens.Testing
{
    /// <summary>
    /// Visualizes the Buffer A (SharedWorldSnapshot) data state directly without referencing GameObjects.
    /// Used for validation testing of the Spatial Partitioning epic.
    /// </summary>
    public class GridVisualizer : MonoBehaviour
    {
        private ISpatialPartition _spatialPartition;

        [Inject]
        public void Construct(ISpatialPartition spatialPartition)
        {
            _spatialPartition = spatialPartition;
        }

        private void OnDrawGizmos()
        {
            if (_spatialPartition == null || !Application.isPlaying) return;

            var snapshot = _spatialPartition.GetActiveSnapshot();
            if (snapshot == null) return;

            // Retain snapshot while reading to prevent it from being recycled (though OnDrawGizmos runs on main thread, it's good practice)
            snapshot.Retain();

            try
            {
                float cellSize = snapshot.CellSize;
                int width = snapshot.Width;
                int height = snapshot.Height;

                // Assuming origin is at center. Match SpatialHashGrid math:
                float offsetX = (width * cellSize) * 0.5f;
                float offsetY = (height * cellSize) * 0.5f;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * width + x;
                        int count = snapshot.CellCounts[index];
                        var cellArray = snapshot.Grid[index];

                        Vector3 cellCenter = new Vector3(
                            (x * cellSize) + (cellSize * 0.5f) - offsetX,
                            (y * cellSize) + (cellSize * 0.5f) - offsetY,
                            0f
                        );
                        Vector3 size = new Vector3(cellSize, cellSize, 0.1f);

                        bool hasPawn = false;
                        bool hasResource = false;
                        bool hasWall = false;

                        for (int i = 0; i < count; i++)
                        {
                            var type = cellArray[i].Type;
                            if (type == EntityType.Pawn) hasPawn = true;
                            if (type == EntityType.Resource) hasResource = true;
                            if (type == EntityType.Wall) hasWall = true;
                        }

                        if (hasWall)
                        {
                            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.8f); // Gray for walls (optional extra)
                            Gizmos.DrawCube(cellCenter, size);
                        }
                        else if (hasResource)
                        {
                            Gizmos.color = new Color(0f, 0f, 1f, 0.8f); // Blue solid for resource
                            Gizmos.DrawCube(cellCenter, size);
                        }
                        else if (hasPawn)
                        {
                            Gizmos.color = new Color(1f, 0f, 0f, 0.8f); // Red solid for pawn
                            Gizmos.DrawCube(cellCenter, size);
                        }
                        else
                        {
                            Gizmos.color = new Color(0f, 1f, 0f, 0.2f); // Green wireframe for empty/walkable
                            Gizmos.DrawWireCube(cellCenter, size);
                        }
                    }
                }
            }
            finally
            {
                snapshot.Release();
            }
        }
    }
}
