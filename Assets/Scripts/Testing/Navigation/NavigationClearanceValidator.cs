using System;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer.Unity;

namespace Simpiens.Testing.Navigation
{
    /// <summary>
    /// Milestone 1 closure, Phase 1: pawns never enter wall geometry and never stack.
    /// Covers inflated wall rasterization, static-grid reset/rebuild, wall-checked displacement (path steps,
    /// watchdog nudges and back-offs), A* endpoints snapped out of blocked cells, and pawn separation on arrival.
    /// </summary>
    public class NavigationClearanceValidator : IStartable
    {
        private const float CellSize = 1f;
        private const int GridSize = 20; // world spans -10..10

        public void Start()
        {
            RunAllValidationTests();
        }

        public void RunAllValidationTests()
        {
            Debug.Log("[NavigationClearanceValidator] Beginning navigation clearance validation...");

            ValidateInflatedWallRasterization();
            ValidateStaticResetAndRebuild();
            ValidateDisplacementRejectsWalls();
            ValidateWatchdogIsWallChecked();
            ValidateSnappedPathEndpoint();
            ValidateArrivalDoesNotStack();

            Debug.Log("<color=green>[NavigationClearanceValidator] ALL 6 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateInflatedWallRasterization()
        {
            var registry = new WorldRegistry();
            var grid = new SpatialHashGrid();
            grid.Initialize(GridSize, GridSize, CellSize);

            // Off-grid wall: before inflation its 0.55 rasterization left a free cell whose pawns overlapped the box
            var wall = CreateNode("[TestWall_Inflation]", new Vector2(0.44f, 0f), EntityType.Wall, 0.55f);
            registry.RegisterNode(wall);
            grid.UpdateFromRegistry(registry);
            var snapshot = grid.GetActiveSnapshot();

            // Any pawn centre within wall radius + body radius of the wall would overlap it: all such points must be blocked
            float reach = 0.55f + NavConstants.PawnBodyRadius - 0.01f;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                var p = new Vector2(0.44f, 0f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * reach;
                Assert(!snapshot.IsStaticWalkable(p), $"Point {p} within body reach of the wall must be statically blocked.");
            }

            Destroy(wall);
            Debug.Log("[PASS] Test 1: Walls are rasterized inflated by the pawn body radius.");
        }

        private void ValidateStaticResetAndRebuild()
        {
            var registry = new WorldRegistry();
            var grid = new SpatialHashGrid();
            grid.Initialize(GridSize, GridSize, CellSize);

            var wall = CreateNode("[TestWall_Reset]", new Vector2(3.5f, 3.5f), EntityType.Wall, 0.55f);
            registry.RegisterNode(wall);
            grid.UpdateFromRegistry(registry);
            Assert(!grid.GetActiveSnapshot().IsStaticWalkable(new Vector2(3.5f, 3.5f)), "Registered wall must block its cell.");

            // Removal: rebuilt on the update after the wall count drops
            registry.UnregisterNode(wall);
            grid.UpdateFromRegistry(registry);
            grid.UpdateFromRegistry(registry);
            Assert(grid.GetActiveSnapshot().IsStaticWalkable(new Vector2(3.5f, 3.5f)), "A removed wall must stop blocking after rebuild.");

            // Explicit reset (respawn path)
            registry.RegisterNode(wall);
            grid.UpdateFromRegistry(registry);
            registry.UnregisterNode(wall);
            grid.ResetStatic();
            Assert(grid.GetActiveSnapshot().IsStaticWalkable(new Vector2(3.5f, 3.5f)), "ResetStatic must clear rasterized walls.");

            Destroy(wall);
            Debug.Log("[PASS] Test 2: Static grid clears on reset and rebuilds when walls disappear.");
        }

        private void ValidateDisplacementRejectsWalls()
        {
            var grid = CreateWalledGrid(out var simManager);
            var pawn = CreateNode("[TestPawn_Displace]", new Vector2(-0.5f, 0.5f), EntityType.Pawn, 0.35f);

            // Wall column at cell x = 0 (world x 0..1)
            Assert(!simManager.TryDisplacePawn(pawn, new Vector2(0.5f, 0.5f)), "Move into a wall cell must be rejected.");
            Assert(Vector2.Distance(pawn.Position, new Vector2(-0.5f, 0.5f)) < 0.001f, "Rejected move must leave the pawn in place.");
            Assert(!simManager.TryDisplacePawn(pawn, new Vector2(1.5f, 0.5f)), "Move across a wall must be rejected.");
            Assert(simManager.TryDisplacePawn(pawn, new Vector2(-1.5f, 0.5f)), "Move into open space must succeed.");

            // A pawn already inside a blocked cell may always leave it
            pawn.transform.position = new Vector2(0.5f, 0.5f);
            Assert(simManager.TryDisplacePawn(pawn, new Vector2(-0.5f, 0.5f)), "A pawn inside a wall cell must be allowed to escape.");

            Destroy(pawn);
            Debug.Log("[PASS] Test 3: Displacement never moves a pawn into or through static walls.");
        }

        private void ValidateWatchdogIsWallChecked()
        {
            var grid = CreateWalledGrid(out var simManager);
            // Pawn hugging the wall, heading north along it: the +perpendicular nudge (west->east side) points into the wall
            var pawn = CreateNode("[TestPawn_Watchdog]", new Vector2(-0.15f, 0.5f), EntityType.Pawn, 0.35f);
            var target = new Vector2(-0.15f, 5f);

            // dir = +Y, perpendicular = (-1, 0) * 0.2 for StallCount 1 -> west (open). Use StallCount 2 (-0.4 -> east, into the wall).
            var state = new ActiveIntentState { StallCount = 2, LastSampledPosition = pawn.Position };
            simManager.ApplyProgressiveUnstuckReflex(pawn, target, state);
            Assert(grid.GetActiveSnapshot().IsStaticWalkable(pawn.Position), $"Tier 1 nudge must not land in a wall (at {pawn.Position}).");
            Assert(pawn.Position.x < -0.15f, $"Tier 1 nudge toward the wall must fall back to the mirrored flank (x: {pawn.Position.x}).");

            // Tier 3 back-off directly into the wall is skipped
            pawn.transform.position = new Vector2(-0.15f, 0.5f);
            var backState = new ActiveIntentState { StallCount = 4, LastSampledPosition = pawn.Position };
            simManager.ApplyProgressiveUnstuckReflex(pawn, new Vector2(-3f, 0.5f), backState); // back-off direction = +X (into wall)
            Assert(grid.GetActiveSnapshot().IsStaticWalkable(pawn.Position), $"Tier 3 back-off must not land in a wall (at {pawn.Position}).");

            Destroy(pawn);
            Debug.Log("[PASS] Test 4: Watchdog nudges and back-offs are wall-checked.");
        }

        private void ValidateSnappedPathEndpoint()
        {
            var grid = CreateWalledGrid(out _);
            var searcher = new GridAStarSearcher();
            var response = new PathResponse();
            response.Initialize(64, null);

            // Target sits inside the wall column
            var request = new PathRequest { StartPosition = new Vector2(-4.5f, 0.5f), TargetPosition = new Vector2(0.5f, 0.5f) };
            bool ok = searcher.Search(in request, grid.GetActiveSnapshot(), response);
            Assert(ok && response.Length > 0, "Search toward a blocked target must still yield a path to the nearest walkable cell.");

            var snapshot = grid.GetActiveSnapshot();
            var end = response.Waypoints[response.Length - 1];
            Assert(snapshot.IsStaticWalkable(end), $"Final waypoint {end} must lie in a walkable cell.");
            for (int i = 1; i < response.Length; i++)
            {
                Assert(snapshot.IsSegmentStaticClear(response.Waypoints[i - 1], response.Waypoints[i]),
                    $"Path segment {i} must stay in walkable cells.");
            }

            Debug.Log("[PASS] Test 5: Blocked targets snap to the reachable cell; every segment is statically clear.");
        }

        private void ValidateArrivalDoesNotStack()
        {
            var registry = new WorldRegistry();
            var grid = new SpatialHashGrid();
            grid.Initialize(GridSize, GridSize, CellSize);
            var simManager = new SimulationManager(registry, grid);

            var occupant = CreateNode("[TestPawn_Occupant]", new Vector2(5f, 5f), EntityType.Pawn, 0.35f);
            var mover = CreateNode("[TestPawn_Mover]", new Vector2(4.2f, 5f), EntityType.Pawn, 0.35f);
            registry.RegisterNode(occupant);
            registry.RegisterNode(mover);
            grid.UpdateFromRegistry(registry);

            // Separation rule: approaching is blocked, stepping apart is allowed
            mover.transform.position = new Vector2(4.5f, 5f);
            Assert(simManager.IsCrowdedMove(mover, new Vector2(4.7f, 5f)), "Approaching within separation must be reported as crowding.");
            mover.transform.position = new Vector2(4.7f, 5f);
            Assert(!simManager.IsCrowdedMove(mover, new Vector2(4.5f, 5f)), "Stepping away must never be blocked.");

            // Live positions: the occupant moved this tick (snapshot still has it at 5,5); the check must see where it is now
            occupant.transform.position = new Vector2(3.6f, 5f);
            mover.transform.position = new Vector2(4.2f, 5f);
            Assert(simManager.IsCrowdedMove(mover, new Vector2(3.95f, 5f)), "Crowding must use the occupant's live position, not the stale snapshot.");
            occupant.transform.position = new Vector2(5f, 5f);
            mover.transform.position = new Vector2(4.2f, 5f);

            // Both want the same spot: the mover arrives short of the occupant instead of stacking
            var path = new PathResponse { Waypoints = new[] { new Vector2(5f, 5f) }, Length = 1, IsValid = true };
            var state = new ActiveIntentState { Intent = new WanderIntent(mover.Id, path), LastSampledPosition = mover.Position };
            IntentResult result = IntentResult.InProgress;
            for (int tick = 0; tick < 40 && result == IntentResult.InProgress; tick++)
            {
                result = simManager.ExecutePathMovement(mover, path, state);
                grid.UpdateFromRegistry(registry);
            }
            Assert(result == IntentResult.Success, $"Mover must arrive (Success) next to an occupied destination, got {result}.");
            float gap = Vector2.Distance(mover.Position, occupant.Position);
            Assert(gap >= 0.35f, $"Mover must not stack on the occupant (gap {gap:F2}m).");

            Destroy(occupant);
            Destroy(mover);
            Debug.Log("[PASS] Test 6: Pawns arrive beside an occupied destination instead of stacking.");
        }

        /// <summary>Grid with a static wall column at world x 0..1, y -10..10, wired into a SimulationManager.</summary>
        private static SpatialHashGrid CreateWalledGrid(out SimulationManager simManager)
        {
            var grid = new SpatialHashGrid();
            grid.Initialize(GridSize, GridSize, CellSize);
            for (int y = 0; y < GridSize; y++)
            {
                if (grid.StaticGrid.WorldToCell(new Vector2(0.5f, y - GridSize * 0.5f + 0.5f), out int cx, out int cy))
                {
                    grid.StaticGrid.SetWall(cx, cy);
                }
            }
            grid.UpdateFromRegistry(null); // publish a snapshot bound to the static grid
            simManager = new SimulationManager(null, grid);
            return grid;
        }

        private static NodeController CreateNode(string name, Vector2 position, EntityType type, float radius)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = radius;
            var node = go.AddComponent<NodeController>();
            node.Type = type;
            node.Initialize(EntityId.Generate(), new NodeConfiguration(2f));
            return node;
        }

        private static void Destroy(NodeController node)
        {
            if (node != null) GameObject.DestroyImmediate(node.gameObject);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[ASSERTION FAILED] {message}");
            }
        }
    }
}
