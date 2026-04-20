using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Simpiens.Simulation.Spatial
{
    /// <summary>
    /// A pooled, thread-safe snapshot of the entire world's spatial state.
    /// Shared by all cognitive agents during a tick to avoid per-agent allocations.
    /// Uses reference counting to return to the pool when all background tasks are done.
    /// </summary>
    public class SharedWorldSnapshot
    {
        private int _refCount;
        private Action<SharedWorldSnapshot> _returnToPoolAction;

        public readonly float CellSize;
        public readonly int Width;
        public readonly int Height;

        // Using a 1D array for the grid: index = y * Width + x
        // Each cell has a pre-allocated array of EntitySnapshots.
        public readonly EntitySnapshot[][] Grid;
        public readonly int[] CellCounts;

        public SharedWorldSnapshot(int width, int height, float cellSize, int maxEntitiesPerCell, Action<SharedWorldSnapshot> returnAction)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
            _returnToPoolAction = returnAction;

            int totalCells = width * height;
            Grid = new EntitySnapshot[totalCells][];
            CellCounts = new int[totalCells];

            for (int i = 0; i < totalCells; i++)
            {
                Grid[i] = new EntitySnapshot[maxEntitiesPerCell];
            }
        }

        public void Retain()
        {
            Interlocked.Increment(ref _refCount);
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _refCount) == 0)
            {
                _returnToPoolAction(this);
            }
        }

        public void Clear()
        {
            Array.Clear(CellCounts, 0, CellCounts.Length);
        }

        public void AddEntity(int cellX, int cellY, in EntitySnapshot snapshot)
        {
            if (cellX < 0 || cellX >= Width || cellY < 0 || cellY >= Height) return;

            int index = cellY * Width + cellX;
            int count = CellCounts[index];

            if (count < Grid[index].Length)
            {
                Grid[index][count] = snapshot;
                CellCounts[index]++;
            }
            // If the cell is full, we drop it for now, or we could have dynamic resizing 
            // but dynamic resizing allocates. In a strict zero-alloc setup, we size appropriately.
        }

        /// <summary>
        /// Thread-safe query method for cognitive agents to find entities in a radius.
        /// </summary>
        public void GetEntitiesInRange(Vector2 position, float radius, List<EntitySnapshot> results)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt((position.x - radius) / CellSize));
            int maxX = Mathf.Min(Width - 1, Mathf.FloorToInt((position.x + radius) / CellSize));
            int minY = Mathf.Max(0, Mathf.FloorToInt((position.y - radius) / CellSize));
            int maxY = Mathf.Min(Height - 1, Mathf.FloorToInt((position.y + radius) / CellSize));

            float sqrRadius = radius * radius;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int cellIndex = y * Width + x;
                    int count = CellCounts[cellIndex];
                    var cellArray = Grid[cellIndex];

                    for (int i = 0; i < count; i++)
                    {
                        var entity = cellArray[i];
                        
                        // Prevent duplicates if an entity spans multiple cells
                        // A quick check is to ensure we haven't already added it, 
                        // but List.Contains is O(N).
                        // Since this runs on background threads, small O(N) is okay, 
                        // or we could use a HashSet, but that allocates if not pooled.
                        
                        // For now, check distance:
                        float sqrDist = (entity.Position - position).sqrMagnitude;
                        
                        // We account for the entity's radius in the overlap
                        float totalRadius = radius + entity.Radius;

                        if (sqrDist <= totalRadius * totalRadius)
                        {
                            // Avoid duplicates (since entity could be in multiple cells)
                            bool exists = false;
                            for (int r = 0; r < results.Count; r++)
                            {
                                if (results[r].Id == entity.Id)
                                {
                                    exists = true;
                                    break;
                                }
                            }

                            if (!exists)
                            {
                                results.Add(entity);
                            }
                        }
                    }
                }
            }
        }
    }
}
