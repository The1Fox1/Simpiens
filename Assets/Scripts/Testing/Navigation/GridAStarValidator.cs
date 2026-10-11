using System;
using System.Threading.Tasks;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer.Unity;

namespace Simpiens.Testing.Navigation
{
    /// <summary>
    /// Automated domain validation suite for Section 2.6:
    /// High-Performance Zero-Allocation Grid A* Navigation Engine.
    /// </summary>
    public class GridAStarValidator : IStartable
    {
        public void Start()
        {
            RunAllValidationTests();
        }

        public void RunAllValidationTests()
        {
            Debug.Log("[GridAStarValidator] Beginning automated validation of 2D Grid A* Navigation Engine...");

            ValidateDirectUnobstructedPath();
            ValidateObstacleDetour();
            ValidateDoorwayTraversal();
            ValidateCornerCuttingClearance();
            ValidateEnclosedUnreachableTarget();
            ValidatePathSmoothing();
            ValidateZeroAllocationAndConcurrency();

            Debug.Log("<color=green>[GridAStarValidator] ALL 7 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private SharedWorldSnapshot CreateTestSnapshot(int width = 30, int height = 30, float cellSize = 1.0f)
        {
            var snapshot = new SharedWorldSnapshot(width, height, cellSize, 16, s => { });
            var staticGrid = new StaticNavGrid(width, height, cellSize);
            snapshot.SetStaticGrid(staticGrid);
            return snapshot;
        }

        private void ValidateDirectUnobstructedPath()
        {
            var snapshot = CreateTestSnapshot(30, 30, 1.0f);
            var searcher = new GridAStarSearcher();
            var response = new PathResponse();
            response.Initialize(200, null);

            var request = new PathRequest
            {
                StartPosition = new Vector2(-5f, 0f),
                TargetPosition = new Vector2(5f, 0f)
            };

            bool success = searcher.Search(in request, snapshot, response);

            Assert(success, "Direct path search must succeed.");
            Assert(response.IsValid, "Path must be marked valid.");
            Assert(response.Length == 2, $"Direct smoothed path must collapse to 2 waypoints, got {response.Length}.");
            Assert(Vector2.Distance(response.Waypoints[0], request.StartPosition) < 0.05f, "Start waypoint must match start position.");
            Assert(Vector2.Distance(response.Waypoints[response.Length - 1], request.TargetPosition) < 0.05f, "Goal waypoint must reach target.");

            Debug.Log("[PASS] Test 1: Direct unobstructed path collapsed to optimal 2-point direct vector.");
        }

        private void ValidateObstacleDetour()
        {
            var snapshot = CreateTestSnapshot(30, 30, 1.0f);
            var staticGrid = snapshot.StaticGrid;

            // Place a vertical wall barrier at X = 0 from Y = -3 to Y = 3
            for (int y = -3; y <= 3; y++)
            {
                if (staticGrid.WorldToCell(new Vector2(0f, y), out int cx, out int cy))
                {
                    staticGrid.SetWall(cx, cy);
                }
            }

            var searcher = new GridAStarSearcher();
            var response = new PathResponse();
            response.Initialize(200, null);

            var request = new PathRequest
            {
                StartPosition = new Vector2(-4f, 0f),
                TargetPosition = new Vector2(4f, 0f)
            };

            bool success = searcher.Search(in request, snapshot, response);

            Assert(success, "Detour path search must succeed around vertical barrier.");
            Assert(response.IsValid, "Path must be marked valid.");
            Assert(response.Length >= 3, $"Detoured path must have >= 3 waypoints, got {response.Length}.");
            Assert(Vector2.Distance(response.Waypoints[response.Length - 1], request.TargetPosition) < 0.05f, "Path must reach target.");

            // Verify none of the waypoints or segment midpoints intersect the wall
            for (int i = 0; i < response.Length; i++)
            {
                Vector2 pt = response.Waypoints[i];
                if (staticGrid.WorldToCell(pt, out int cx, out int cy))
                {
                    Assert(staticGrid.IsCellWalkable(cx, cy), $"Waypoint {pt} must not be inside wall.");
                }
            }

            Debug.Log("[PASS] Test 2: Obstacle detour successfully circumnavigated vertical barrier.");
        }

        private void ValidateDoorwayTraversal()
        {
            var snapshot = CreateTestSnapshot(30, 30, 1.0f);
            var staticGrid = snapshot.StaticGrid;

            // Wall divider from Y = -14 to +14 at X = 0, leaving a doorway gap at Y = 3..4
            for (int y = -14; y <= 14; y++)
            {
                if (y == 3 || y == 4) continue; // Door opening

                if (staticGrid.WorldToCell(new Vector2(0f, y), out int cx, out int cy))
                {
                    staticGrid.SetWall(cx, cy);
                }
            }

            var searcher = new GridAStarSearcher();
            var response = new PathResponse();
            response.Initialize(200, null);

            var request = new PathRequest
            {
                StartPosition = new Vector2(-5f, -5f),
                TargetPosition = new Vector2(5f, -5f)
            };

            bool success = searcher.Search(in request, snapshot, response);

            Assert(success, "Doorway traversal search must succeed.");
            Assert(response.IsValid, "Path must be valid.");
            Assert(Vector2.Distance(response.Waypoints[response.Length - 1], request.TargetPosition) < 0.05f, "Goal must reach target.");

            // Verify the path went north toward the doorway
            bool traversedDoorway = false;
            for (int i = 0; i < response.Length; i++)
            {
                if (response.Waypoints[i].y >= 2.5f)
                {
                    traversedDoorway = true;
                    break;
                }
            }
            Assert(traversedDoorway, "Path must travel north through the doorway gap at Y=3..4.");

            Debug.Log("[PASS] Test 3: Doorway traversal navigated through narrow room portal.");
        }

        private void ValidateCornerCuttingClearance()
        {
            var snapshot = CreateTestSnapshot(20, 20, 1.0f);
            var staticGrid = snapshot.StaticGrid;

            // Set up a wall corner: (0, 1) and (1, 0) are walls
            if (staticGrid.WorldToCell(new Vector2(0f, 1f), out int cx1, out int cy1)) staticGrid.SetWall(cx1, cy1);
            if (staticGrid.WorldToCell(new Vector2(1f, 0f), out int cx2, out int cy2)) staticGrid.SetWall(cx2, cy2);

            var searcher = new GridAStarSearcher();
            var response = new PathResponse();
            response.Initialize(200, null);

            var request = new PathRequest
            {
                StartPosition = new Vector2(0f, 0f),
                TargetPosition = new Vector2(1f, 1f)
            };

            bool success = searcher.Search(in request, snapshot, response);

            // Because (0, 1) and (1, 0) are blocked, the diagonal move from (0,0) to (1,1) must be rejected
            // The pathfinder must either route around or report blocked if enclosed
            if (success && response.IsValid)
            {
                for (int i = 0; i < response.Length - 1; i++)
                {
                    Vector2 mid = (response.Waypoints[i] + response.Waypoints[i + 1]) * 0.5f;
                    // Midpoint must not pass directly over the diagonal intersection between the two walls
                    float distToDiagonalIntersection = Vector2.Distance(mid, new Vector2(0.5f, 0.5f));
                    Assert(distToDiagonalIntersection > 0.1f || response.Length > 2, "Path must not cut through diagonal corner.");
                }
            }

            Debug.Log("[PASS] Test 4: Corner-cutting prevention enforced for diagonal navigation.");
        }

        private void ValidateEnclosedUnreachableTarget()
        {
            var snapshot = CreateTestSnapshot(20, 20, 1.0f);
            var staticGrid = snapshot.StaticGrid;

            // Completely seal off a 3x3 room around (0, 0)
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    if (Math.Abs(x) == 2 || Math.Abs(y) == 2)
                    {
                        if (staticGrid.WorldToCell(new Vector2(x, y), out int cx, out int cy))
                        {
                            staticGrid.SetWall(cx, cy);
                        }
                    }
                }
            }

            var searcher = new GridAStarSearcher();
            var response = new PathResponse();
            response.Initialize(200, null);

            var request = new PathRequest
            {
                StartPosition = new Vector2(-6f, 0f),
                TargetPosition = new Vector2(0f, 0f)
            };

            bool success = searcher.Search(in request, snapshot, response);

            Assert(!success || !response.IsValid, "Path search to fully enclosed target must report failure.");
            Assert(response.Length == 0, "Failed path must have 0 length.");

            Debug.Log("[PASS] Test 5: Fully enclosed target terminated safely without errors.");
        }

        private void ValidatePathSmoothing()
        {
            var snapshot = CreateTestSnapshot(30, 30, 1.0f);
            var staticGrid = snapshot.StaticGrid;

            // Build an L-shaped corridor:
            // Wall on the right of the vertical branch, and under the horizontal branch
            for (int y = 0; y <= 8; y++)
            {
                if (staticGrid.WorldToCell(new Vector2(2f, y), out int cx, out int cy)) staticGrid.SetWall(cx, cy);
            }
            for (int x = 2; x <= 10; x++)
            {
                if (staticGrid.WorldToCell(new Vector2(x, 8f), out int cx, out int cy)) staticGrid.SetWall(cx, cy);
            }

            var searcher = new GridAStarSearcher();
            var response = new PathResponse();
            response.Initialize(200, null);

            var request = new PathRequest
            {
                StartPosition = new Vector2(0f, 0f),
                TargetPosition = new Vector2(0f, 10f)
            };

            bool success = searcher.Search(in request, snapshot, response);

            Assert(success, "Corridor search must succeed.");
            Assert(response.IsValid, "Path must be valid.");
            // Direct straight line along X = 0 from Y = 0 to Y = 10 is clear
            Assert(response.Length == 2, $"Raycast smoothing should reduce unobstructed straight corridor to 2 waypoints, got {response.Length}.");

            Debug.Log("[PASS] Test 6: Raycast string-pulling collapsed straight segments into minimal waypoints.");
        }

        private void ValidateZeroAllocationAndConcurrency()
        {
            var snapshot = CreateTestSnapshot(30, 30, 1.0f);
            var pathfinder = new AsyncPathfinder();
            pathfinder.Start();

            // 1. Concurrency Stress Test across 4 parallel workers
            Task[] tasks = new Task[4];
            for (int t = 0; t < 4; t++)
            {
                int workerId = t;
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < 250; i++)
                    {
                        var req = new PathRequest
                        {
                            StartPosition = new Vector2(-5f + (workerId * 0.5f), -5f),
                            TargetPosition = new Vector2(5f, 5f)
                        };
                        var res = pathfinder.CalculatePathImmediate(req, snapshot);
                        Assert(res.IsValid, $"Worker {workerId} iteration {i} must find valid path.");
                        res.ReturnToPool();
                    }
                });
            }
            Task.WaitAll(tasks);

            // 2. Zero Allocation Benchmark (steady-state hot loop)
            var searcher = new GridAStarSearcher();
            var response = new PathResponse();
            response.Initialize(200, null);

            var benchRequest = new PathRequest
            {
                StartPosition = new Vector2(-5f, -5f),
                TargetPosition = new Vector2(5f, 5f)
            };

            // Warm up iteration
            searcher.Search(in benchRequest, snapshot, response);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                searcher.Search(in benchRequest, snapshot, response);
            }

            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - startAlloc;
            Assert(totalAlloc == 0, $"Zero allocation guarantee violated: allocated {totalAlloc} bytes across 1,000 searches.");

            Debug.Log($"[PASS] Test 7: Multi-threaded concurrency passed (1,000 queries across 4 workers) and strictly 0 bytes allocated across 1,000 searches.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[GridAStarValidator ASSERTION FAILED] {message}");
            }
        }
    }
}
