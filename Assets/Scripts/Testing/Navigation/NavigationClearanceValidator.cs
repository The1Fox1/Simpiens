using Simpiens.Testing.Environment;
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
            var wall = CreateNode("[TestWall_Inflation]", new Vec2(0.44f, 0f), EntityType.Wall, 0.55f);
            registry.Register(wall);
            grid.UpdateFromRegistry(registry);
            var snapshot = grid.GetActiveSnapshot();

            // Any pawn centre within wall radius + body radius of the wall would overlap it: all such points must be blocked
            float reach = 0.55f + NavConstants.PawnBodyRadius - 0.01f;
            for (int i = 0; i < 16; i++)
            {
                float a = i * SimMath.PI * 2f / 16f;
                var p = new Vec2(0.44f, 0f) + new Vec2(SimMath.Cos(a), SimMath.Sin(a)) * reach;
                Assert(!snapshot.IsStaticWalkable(p), $"Point {p} within body reach of the wall must be statically blocked.");
            }
            Debug.Log("[PASS] Test 1: Walls are rasterized inflated by the pawn body radius.");
        }

        private void ValidateStaticResetAndRebuild()
        {
            var registry = new WorldRegistry();
            var grid = new SpatialHashGrid();
            grid.Initialize(GridSize, GridSize, CellSize);

            var wall = CreateNode("[TestWall_Reset]", new Vec2(3.5f, 3.5f), EntityType.Wall, 0.55f);
            registry.Register(wall);
            grid.UpdateFromRegistry(registry);
            Assert(!grid.GetActiveSnapshot().IsStaticWalkable(new Vec2(3.5f, 3.5f)), "Registered wall must block its cell.");

            // Removal: rebuilt on the update after the wall count drops
            registry.Unregister(wall);
            grid.UpdateFromRegistry(registry);
            grid.UpdateFromRegistry(registry);
            Assert(grid.GetActiveSnapshot().IsStaticWalkable(new Vec2(3.5f, 3.5f)), "A removed wall must stop blocking after rebuild.");

            // Explicit reset (respawn path)
            registry.Register(wall);
            grid.UpdateFromRegistry(registry);
            registry.Unregister(wall);
            grid.ResetStatic();
            Assert(grid.GetActiveSnapshot().IsStaticWalkable(new Vec2(3.5f, 3.5f)), "ResetStatic must clear rasterized walls.");
            Debug.Log("[PASS] Test 2: Static grid clears on reset and rebuilds when walls disappear.");
        }

        private void ValidateDisplacementRejectsWalls()
        {
            var grid = CreateWalledGrid(out var simManager);
            var pawn = CreateNode("[TestPawn_Displace]", new Vec2(-0.5f, 0.5f), EntityType.Pawn, 0.35f);

            // Wall column at cell x = 0 (world x 0..1)
            Assert(!simManager.TryDisplacePawn(pawn, new Vec2(0.5f, 0.5f)), "Move into a wall cell must be rejected.");
            Assert(Vec2.Distance(pawn.Position, new Vec2(-0.5f, 0.5f)) < 0.001f, "Rejected move must leave the pawn in place.");
            Assert(!simManager.TryDisplacePawn(pawn, new Vec2(1.5f, 0.5f)), "Move across a wall must be rejected.");
            Assert(simManager.TryDisplacePawn(pawn, new Vec2(-1.5f, 0.5f)), "Move into open space must succeed.");

            // A pawn already inside a blocked cell may always leave it
            pawn.SetPosition(new Vec2(0.5f, 0.5f));
            Assert(simManager.TryDisplacePawn(pawn, new Vec2(-0.5f, 0.5f)), "A pawn inside a wall cell must be allowed to escape.");
            Debug.Log("[PASS] Test 3: Displacement never moves a pawn into or through static walls.");
        }

        private void ValidateWatchdogIsWallChecked()
        {
            var grid = CreateWalledGrid(out var simManager);
            // Pawn hugging the wall, heading north along it: the +perpendicular nudge (west->east side) points into the wall
            var pawn = CreateNode("[TestPawn_Watchdog]", new Vec2(-0.15f, 0.5f), EntityType.Pawn, 0.35f);
            var target = new Vec2(-0.15f, 5f);

            // dir = +Y, perpendicular = (-1, 0) * 0.2 for StallCount 1 -> west (open). Use StallCount 2 (-0.4 -> east, into the wall).
            var state = new ActiveIntentState { StallCount = 2, LastSampledPosition = pawn.Position };
            simManager.ApplyProgressiveUnstuckReflex(pawn, target, state);
            Assert(grid.GetActiveSnapshot().IsStaticWalkable(pawn.Position), $"Tier 1 nudge must not land in a wall (at {pawn.Position}).");
            Assert(pawn.Position.X < -0.15f, $"Tier 1 nudge toward the wall must fall back to the mirrored flank (x: {pawn.Position.X}).");

            // Tier 3 back-off directly into the wall is skipped
            pawn.SetPosition(new Vec2(-0.15f, 0.5f));
            var backState = new ActiveIntentState { StallCount = 4, LastSampledPosition = pawn.Position };
            simManager.ApplyProgressiveUnstuckReflex(pawn, new Vec2(-3f, 0.5f), backState); // back-off direction = +X (into wall)
            Assert(grid.GetActiveSnapshot().IsStaticWalkable(pawn.Position), $"Tier 3 back-off must not land in a wall (at {pawn.Position}).");
            Debug.Log("[PASS] Test 4: Watchdog nudges and back-offs are wall-checked.");
        }

        private void ValidateSnappedPathEndpoint()
        {
            var grid = CreateWalledGrid(out _);
            var searcher = new GridAStarSearcher();
            var response = new PathResponse();
            response.Initialize(64, null);

            // Target sits inside the wall column
            var request = new PathRequest { StartPosition = new Vec2(-4.5f, 0.5f), TargetPosition = new Vec2(0.5f, 0.5f) };
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

            var occupant = CreateNode("[TestPawn_Occupant]", new Vec2(5f, 5f), EntityType.Pawn, 0.35f);
            var mover = CreateNode("[TestPawn_Mover]", new Vec2(4.2f, 5f), EntityType.Pawn, 0.35f);
            registry.Register(occupant);
            registry.Register(mover);
            grid.UpdateFromRegistry(registry);

            // Separation rule: approaching is blocked, stepping apart is allowed
            mover.SetPosition(new Vec2(4.5f, 5f));
            Assert(simManager.IsCrowdedMove(mover, new Vec2(4.7f, 5f)), "Approaching within separation must be reported as crowding.");
            mover.SetPosition(new Vec2(4.7f, 5f));
            Assert(!simManager.IsCrowdedMove(mover, new Vec2(4.5f, 5f)), "Stepping away must never be blocked.");

            // Live positions: the occupant moved this tick (snapshot still has it at 5,5); the check must see where it is now
            occupant.SetPosition(new Vec2(3.6f, 5f));
            mover.SetPosition(new Vec2(4.2f, 5f));
            Assert(simManager.IsCrowdedMove(mover, new Vec2(3.95f, 5f)), "Crowding must use the occupant's live position, not the stale snapshot.");
            occupant.SetPosition(new Vec2(5f, 5f));
            mover.SetPosition(new Vec2(4.2f, 5f));

            // Both want the same spot: the mover arrives short of the occupant instead of stacking
            var path = new PathResponse { Waypoints = new[] { new Vec2(5f, 5f) }, Length = 1, IsValid = true };
            var state = new ActiveIntentState { Intent = new WanderIntent(mover.Id, path), LastSampledPosition = mover.Position };
            IntentResult result = IntentResult.InProgress;
            for (int tick = 0; tick < 40 && result == IntentResult.InProgress; tick++)
            {
                result = simManager.ExecutePathMovement(mover, path, state);
                grid.UpdateFromRegistry(registry);
            }
            Assert(result == IntentResult.Success, $"Mover must arrive (Success) next to an occupied destination, got {result}.");
            float gap = Vec2.Distance(mover.Position, occupant.Position);
            Assert(gap >= 0.35f, $"Mover must not stack on the occupant (gap {gap:F2}m).");
            Debug.Log("[PASS] Test 6: Pawns arrive beside an occupied destination instead of stacking.");
        }

        /// <summary>Grid with a static wall column at world x 0..1, y -10..10, wired into a SimulationManager.</summary>
        private static SpatialHashGrid CreateWalledGrid(out SimulationManager simManager)
        {
            var grid = new SpatialHashGrid();
            grid.Initialize(GridSize, GridSize, CellSize);
            for (int y = 0; y < GridSize; y++)
            {
                if (grid.StaticGrid.WorldToCell(new Vec2(0.5f, y - GridSize * 0.5f + 0.5f), out int cx, out int cy))
                {
                    grid.StaticGrid.SetWall(cx, cy);
                }
            }
            grid.UpdateFromRegistry(null); // publish a snapshot bound to the static grid
            simManager = new SimulationManager(null, grid);
            return grid;
        }

        private static WorldEntity CreateNode(string name, Vec2 position, EntityType type, float radius)
        {
            return HeadlessEntities.Prop(null, type, position, radius: radius, name: name, register: false);
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
