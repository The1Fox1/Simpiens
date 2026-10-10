using System;
using Simpiens.Environment;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Simpiens.Testing.Environment
{
    /// <summary>
    /// Automated headless domain validation suite for Phase 1 of Epic MAP:
    /// Utilitarian Outpost Map Construction & Doorway Walkability.
    /// Runs on startup via VContainer IStartable and can be executed headlessly in isolation.
    /// </summary>
    public class OutpostMapBuilderValidator : IStartable
    {
        private readonly IOutpostMapBuilder _mapBuilder;
        private readonly IWorldRegistry _registry;

        [Inject]
        public OutpostMapBuilderValidator(IOutpostMapBuilder mapBuilder = null, IWorldRegistry registry = null)
        {
            _registry = registry ?? new WorldRegistry();
            _mapBuilder = mapBuilder ?? new OutpostMapBuilder(_registry);
        }

        public void Start()
        {
            RunAllValidationTests();
        }

        public void RunAllValidationTests()
        {
            Debug.Log("<color=#38bdf8><b>[OutpostMapBuilderValidator]</b> >>> Starting Epic MAP Phase 1 Domain Suite <<<</color>");

            ValidateDefensiveNullSafety();
            ValidateMapConstructionAndWallCount();
            ValidateDoorwayWalkabilityAndZeroBlockage();
            ValidateWallObstructionAndCollision();
            ValidateDoorwayLineOfSightTraversal();
            ValidateTeardownAndClearance();

            Debug.Log("<color=#10b981><b>[OutpostMapBuilderValidator]</b> >>> ALL 6 MAP BUILDER TESTS PASSED CLEANLY! <<<</color>");
        }

        public void ValidateDefensiveNullSafety()
        {
            // Instantiating with completely null dependencies must not throw
            var isolatedBuilder = new OutpostMapBuilder(registry: null);
            isolatedBuilder.BuildMap();
            if (isolatedBuilder.WallCount <= 0)
            {
                throw new Exception("Isolated OutpostMapBuilder failed to construct wall nodes with null registry.");
            }
            isolatedBuilder.ClearMap();
            if (isolatedBuilder.WallCount != 0)
            {
                throw new Exception("Isolated OutpostMapBuilder failed to clear wall nodes with null registry.");
            }

            Debug.Log("  ✔ ValidateDefensiveNullSafety passed.");
        }

        public void ValidateMapConstructionAndWallCount()
        {
            var registry = new WorldRegistry();
            var builder = new OutpostMapBuilder(registry);

            builder.BuildMap();

            if (builder.WallCount != 218)
            {
                throw new Exception($"Expected exactly 218 wall nodes for the 36x28 outpost, but got {builder.WallCount}!");
            }

            if (registry.ActiveNodes.Count != 218)
            {
                throw new Exception($"WorldRegistry has {registry.ActiveNodes.Count} nodes, expected 218 wall nodes.");
            }

            var nodes = registry.ActiveNodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Type != EntityType.Wall)
                {
                    throw new Exception($"Spawned node #{i} has type {nodes[i].Type}, expected EntityType.Wall.");
                }
            }

            builder.ClearMap();
            Debug.Log("  ✔ ValidateMapConstructionAndWallCount passed: Exactly 218 wall nodes registered with correct EntityType.");
        }

        public void ValidateDoorwayWalkabilityAndZeroBlockage()
        {
            var registry = new WorldRegistry();
            var builder = new OutpostMapBuilder(registry);
            builder.BuildMap();

            var spatialGrid = new SpatialHashGrid();
            spatialGrid.Initialize(44f, 36f, 1.0f);
            spatialGrid.UpdateFromRegistry(registry);

            var snapshot = spatialGrid.GetActiveSnapshot();
            snapshot.Retain();

            try
            {
                float offsetX = (snapshot.Width * snapshot.CellSize) * 0.5f;
                float offsetY = (snapshot.Height * snapshot.CellSize) * 0.5f;

                var doors = builder.DoorwayPositions;
                if (doors.Count != 10)
                {
                    throw new Exception($"Expected 10 canonical doorways, but got {doors.Count}!");
                }

                for (int i = 0; i < doors.Count; i++)
                {
                    Vector2 doorPos = doors[i];
                    int cellX = Mathf.FloorToInt((doorPos.x + offsetX) / snapshot.CellSize);
                    int cellY = Mathf.FloorToInt((doorPos.y + offsetY) / snapshot.CellSize);

                    if (!snapshot.IsWalkable(cellX, cellY))
                    {
                        throw new Exception($"Doorway #{i + 1} at {doorPos} (cell: {cellX}, {cellY}) is blocked! Expected walkable cell.");
                    }
                }
            }
            finally
            {
                snapshot.Release();
                builder.ClearMap();
            }

            Debug.Log("  ✔ ValidateDoorwayWalkabilityAndZeroBlockage passed: All 10 doorways are 100% walkable with zero obstacle collision.");
        }

        public void ValidateWallObstructionAndCollision()
        {
            var registry = new WorldRegistry();
            var builder = new OutpostMapBuilder(registry);
            builder.BuildMap();

            var spatialGrid = new SpatialHashGrid();
            spatialGrid.Initialize(44f, 36f, 1.0f);
            spatialGrid.UpdateFromRegistry(registry);

            var snapshot = spatialGrid.GetActiveSnapshot();
            snapshot.Retain();

            try
            {
                float offsetX = (snapshot.Width * snapshot.CellSize) * 0.5f;
                float offsetY = (snapshot.Height * snapshot.CellSize) * 0.5f;

                // 1. Solid cell on outer North perimeter (0, 14)
                int northCellX = Mathf.FloorToInt((0f + offsetX) / snapshot.CellSize);
                int northCellY = Mathf.FloorToInt((14f + offsetY) / snapshot.CellSize);
                if (snapshot.IsWalkable(northCellX, northCellY))
                {
                    throw new Exception($"Outer North perimeter at (0, 14) reported IsWalkable == true! Expected solid wall obstruction.");
                }

                // 2. Solid cell on interior divider between Orchard and Bunkhouse (-6, 10)
                int dividerCellX = Mathf.FloorToInt((-6f + offsetX) / snapshot.CellSize);
                int dividerCellY = Mathf.FloorToInt((10f + offsetY) / snapshot.CellSize);
                if (snapshot.IsWalkable(dividerCellX, dividerCellY))
                {
                    throw new Exception($"Interior divider at (-6, 10) reported IsWalkable == true! Expected solid wall obstruction.");
                }

                // 3. Line of sight directly across a solid wall should be blocked
                bool losBlocked = snapshot.HasLineOfSight(new Vector2(-8f, 10f), new Vector2(-4f, 10f));
                if (losBlocked)
                {
                    throw new Exception($"Line of sight from (-8, 10) to (-4, 10) succeeded through a solid wall! Expected blockage.");
                }
            }
            finally
            {
                snapshot.Release();
                builder.ClearMap();
            }

            Debug.Log("  ✔ ValidateWallObstructionAndCollision passed: Wall cells and line-of-sight raycasts correctly block traversal.");
        }

        public void ValidateDoorwayLineOfSightTraversal()
        {
            var registry = new WorldRegistry();
            var builder = new OutpostMapBuilder(registry);
            builder.BuildMap();

            var spatialGrid = new SpatialHashGrid();
            spatialGrid.Initialize(44f, 36f, 1.0f);
            spatialGrid.UpdateFromRegistry(registry);

            var snapshot = spatialGrid.GetActiveSnapshot();
            snapshot.Retain();

            try
            {
                // 1. Raycast through Door North (from Hub (0, 2) to Orchard (0, 8))
                bool losNorth = snapshot.HasLineOfSight(new Vector2(0f, 2f), new Vector2(0f, 8f));
                if (!losNorth)
                {
                    throw new Exception("Line of sight traversal failed through Door North! Raycast should be clear.");
                }

                // 2. Raycast through Door South (from Hub (0, -2) to Descent Gate (0, -8))
                bool losSouth = snapshot.HasLineOfSight(new Vector2(0f, -2f), new Vector2(0f, -8f));
                if (!losSouth)
                {
                    throw new Exception("Line of sight traversal failed through Door South! Raycast should be clear.");
                }

                // 3. Raycast through Door West (from Hub (-3, 0) to Deep Quarry (-9, 0))
                bool losWest = snapshot.HasLineOfSight(new Vector2(-3f, 0f), new Vector2(-9f, 0f));
                if (!losWest)
                {
                    throw new Exception("Line of sight traversal failed through Door West! Raycast should be clear.");
                }

                // 4. Raycast through Door East (from Hub (3, 0) to Herbal Conservatory (9, 0))
                bool losEast = snapshot.HasLineOfSight(new Vector2(3f, 0f), new Vector2(9f, 0f));
                if (!losEast)
                {
                    throw new Exception("Line of sight traversal failed through Door East! Raycast should be clear.");
                }

                // 5. Raycast through Door W1 (from Bunkhouse (-12, 8) to Deep Quarry (-12, 2))
                bool losW1 = snapshot.HasLineOfSight(new Vector2(-12f, 8f), new Vector2(-12f, 2f));
                if (!losW1)
                {
                    throw new Exception("Line of sight traversal failed through Door W1! Raycast should be clear.");
                }
            }
            finally
            {
                snapshot.Release();
                builder.ClearMap();
            }

            Debug.Log("  ✔ ValidateDoorwayLineOfSightTraversal passed: Unobstructed straight-line raycasts verified across all key doorways.");
        }

        public void ValidateTeardownAndClearance()
        {
            var registry = new WorldRegistry();
            var builder = new OutpostMapBuilder(registry);

            builder.BuildMap();
            if (registry.ActiveNodes.Count == 0)
            {
                throw new Exception("BuildMap failed to populate nodes.");
            }

            builder.ClearMap();
            if (builder.WallCount != 0 || registry.ActiveNodes.Count != 0)
            {
                throw new Exception($"ClearMap failed! Builder walls: {builder.WallCount}, Registry nodes: {registry.ActiveNodes.Count}");
            }

            Debug.Log("  ✔ ValidateTeardownAndClearance passed: 100% clean teardown with zero orphaned nodes.");
        }
    }
}
