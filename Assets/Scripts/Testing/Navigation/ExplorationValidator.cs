using System;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Cognition.Planning;
using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using UnityEngine;

namespace Simpiens.Testing.Navigation
{
    /// <summary>
    /// Sector-directed exploration (Epic MAP Phase 3, step 4). On a map with rooms, a searching agent heads for
    /// another sector instead of a random point a few units away, which on a walled floor mostly lands in a wall or
    /// the same room. Without a map the old random-bearing waypoint is unchanged.
    /// </summary>
    public class ExplorationValidator
    {
        private const int Agents = 12;
        private const int Windows = 40;

        public void RunAllValidationTests()
        {
            Debug.Log("[ExplorationValidator] Beginning exploration validation...");

            ValidateNoMapKeepsRandomBearing();
            ValidateWaypointsLieInOtherSectors();
            ValidateDeterministicPerWindow();
            ValidateWaypointsAreRoutable();
            ValidateSingleSectorMapFallsBack();

            Debug.Log("<color=green>[ExplorationValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateNoMapKeepsRandomBearing()
        {
            var snapshot = new SharedWorldSnapshot(40, 40, 1f, 8, null);
            for (int a = 0; a < Agents; a++)
            {
                for (int w = 0; w < Windows; w++)
                {
                    var ctx = Context(a, (uint)w * 50u, new Vector2(1f, 2f), snapshot);
                    float d = Vector2.Distance(ResourceTargeting.PickExplorationPoint(ctx), ctx.Position);
                    Assert(d >= 4.99f && d <= 9.01f, $"Without a map the waypoint is 5-9 away (got {d:F2}).");
                }
            }
            Debug.Log("[PASS] Test 1: Without a map, exploration keeps the 5-9 unit random bearing.");
        }

        private void ValidateWaypointsLieInOtherSectors()
        {
            var def = Floor1Layout.Definition;
            var snapshot = Floor1Snapshot(def, out var grid);
            def.TryGetSector(Floor1Layout.HubSectorId, out var hub);

            var picked = new bool[256];
            for (int a = 0; a < Agents; a++)
            {
                for (int w = 0; w < Windows; w++)
                {
                    var ctx = Context(a, (uint)w * ResourceTargeting.SectorChoiceTicks, hub.Bounds.center, snapshot);
                    var p = ResourceTargeting.PickExplorationPoint(ctx);
                    byte sector = def.FindSector(p).Id;
                    Assert(sector != 0, $"Waypoint {p} lies in a real sector.");
                    Assert(sector != Floor1Layout.HubSectorId, $"Waypoint {p} leaves the agent's own sector.");
                    Assert(grid.WorldToCell(p, out int x, out int y) && grid.IsCellWalkable(x, y), $"Waypoint {p} (sector {sector}) is walkable.");
                    picked[sector] = true;

                    // Standing in the sector it just picked, the agent moves on to another one
                    var arrived = Context(a, (uint)w * ResourceTargeting.SectorChoiceTicks, p, snapshot);
                    Assert(def.FindSector(ResourceTargeting.PickExplorationPoint(arrived)).Id != sector, $"An agent that reached sector {sector} picks a different one.");
                }
            }
            for (int i = 0; i < def.Sectors.Count; i++)
            {
                var s = def.Sectors[i];
                if (s.Id == Floor1Layout.HubSectorId) continue;
                Assert(picked[s.Id], $"Sector {s.Name} is picked at least once from the Hub.");
            }
            Debug.Log($"[PASS] Test 2: From the Hub, {Agents}x{Windows} waypoints all lie, walkable, in other sectors; every sector is visited.");
        }

        private void ValidateDeterministicPerWindow()
        {
            var def = Floor1Layout.Definition;
            var snapshot = Floor1Snapshot(def, out _);
            int changed = 0;
            for (int a = 0; a < Agents; a++)
            {
                var first = ResourceTargeting.PickExplorationPoint(Context(a, 0u, Vector2.zero, snapshot));
                var again = ResourceTargeting.PickExplorationPoint(Context(a, ResourceTargeting.SectorChoiceTicks - 1, Vector2.zero, snapshot));
                Assert(first == again, "The same agent and window give the same waypoint.");
                var next = ResourceTargeting.PickExplorationPoint(Context(a, ResourceTargeting.SectorChoiceTicks, Vector2.zero, snapshot));
                if (next != first) changed++;
            }
            Assert(changed >= Agents / 2, $"A new window gives most agents a new waypoint ({changed}/{Agents}).");
            Debug.Log($"[PASS] Test 3: Waypoints are deterministic within a {ResourceTargeting.SectorChoiceTicks}-tick window and change between windows.");
        }

        private void ValidateWaypointsAreRoutable()
        {
            var def = Floor1Layout.Definition;
            var snapshot = Floor1Snapshot(def, out _);
            var searcher = new GridAStarSearcher();
            var starts = new[] { Vector2.zero, new Vector2(-1f, -14f), new Vector2(-16f, 0f), new Vector2(16f, -2.5f) };
            int routes = 0;
            for (int s = 0; s < starts.Length; s++)
            {
                for (int a = 0; a < Agents; a++)
                {
                    for (int w = 0; w < 10; w++)
                    {
                        var ctx = Context(a, (uint)w * ResourceTargeting.SectorChoiceTicks, starts[s], snapshot);
                        var target = ResourceTargeting.PickExplorationPoint(ctx);
                        var response = new PathResponse();
                        response.Initialize(256, null);
                        var request = new PathRequest { StartPosition = starts[s], TargetPosition = target };
                        Assert(searcher.Search(in request, snapshot, response) && response.IsValid, $"Exploration from {starts[s]} to {target} is routable through the doors.");
                        routes++;
                    }
                }
            }
            Debug.Log($"[PASS] Test 4: {routes} exploration routes across Floor 1 are all routable.");
        }

        private void ValidateSingleSectorMapFallsBack()
        {
            var def = new MapDefinitionBuilder("OneRoom", new Rect(-10f, -10f, 20f, 20f))
                .Sector(1, "Room", "", new Rect(-10f, -10f, 20f, 20f))
                .Build();
            var snapshot = new SharedWorldSnapshot(24, 24, 1f, 8, null);
            snapshot.SetMap(def);
            var ctx = Context(0, 0u, Vector2.zero, snapshot);
            Assert(!ResourceTargeting.TryPickSectorWaypoint(ctx, def, out _), "A map with no other sector offers no sector waypoint.");
            float d = Vector2.Distance(ResourceTargeting.PickExplorationPoint(ctx), Vector2.zero);
            Assert(d >= 4.99f && d <= 9.01f, $"It falls back to the random bearing (got {d:F2}).");
            Debug.Log("[PASS] Test 5: A single-sector map falls back to the random bearing.");
        }

        private static SharedWorldSnapshot Floor1Snapshot(MapDefinition def, out StaticNavGrid grid)
        {
            var cells = def.PartitionCells(1f);
            grid = new StaticNavGrid(cells.x, cells.y, 1f);
            WallRasterizer.Rasterize(def, grid);
            grid.EnsureRegions();
            var snapshot = new SharedWorldSnapshot(cells.x, cells.y, 1f, 8, null);
            snapshot.SetStaticGrid(grid);
            snapshot.SetMap(def);
            return snapshot;
        }

        private static AgentContext Context(int agent, uint tick, Vector2 position, SharedWorldSnapshot snapshot)
        {
            var id = new UnityEngine.GUID((uint)agent + 1u, 0x5eed5eedu, 7u, 11u);
            return new AgentContext(id, position, new AgentNeeds(20f, 80f, 60f), 0f, snapshot, new AgentMemory(), tick);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[ExplorationValidator ASSERTION FAILED] {message}");
            }
        }
    }
}
