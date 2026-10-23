using System;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Cognition.Planning;
using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;

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
            TestLog.Log("[ExplorationValidator] Beginning exploration validation...");

            ValidateNoMapKeepsRandomBearing();
            ValidateWaypointsLieInOtherSectors();
            ValidateDeterministicPerWindow();
            ValidateWaypointsAreRoutable();
            ValidateSingleSectorMapFallsBack();
            ValidateSearchTriesTheLeastRecentlyVisitedSector();

            TestLog.Log("<color=green>[ExplorationValidator] ALL 6 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateNoMapKeepsRandomBearing()
        {
            var snapshot = new SharedWorldSnapshot(40, 40, 1f, 8, null);
            for (int a = 0; a < Agents; a++)
            {
                for (int w = 0; w < Windows; w++)
                {
                    var ctx = Context(a, (uint)w * 50u, new Vec2(1f, 2f), snapshot);
                    float d = Vec2.Distance(ResourceTargeting.PickExplorationPoint(ctx), ctx.Position);
                    Assert(d >= 4.99f && d <= 9.01f, $"Without a map the waypoint is 5-9 away (got {d:F2}).");
                }
            }
            TestLog.Log("[PASS] Test 1: Without a map, exploration keeps the 5-9 unit random bearing.");
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
                    var ctx = Context(a, (uint)w * ResourceTargeting.SectorChoiceTicks, hub.Bounds.Center, snapshot);
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
            // The bold explore the hazard sectors too (Epic 10 G2); the agents above are neutral, so cautious
            var boldPicked = new bool[256];
            for (int a = 0; a < Agents; a++)
            {
                for (int w = 0; w < Windows; w++)
                {
                    var bold = Context(a, (uint)w * ResourceTargeting.SectorChoiceTicks, hub.Bounds.Center, snapshot, new AgentPersonality(0f, 0.8f, 0f, 0f, 0f));
                    boldPicked[def.FindSector(ResourceTargeting.PickExplorationPoint(bold)).Id] = true;
                }
            }
            for (int i = 0; i < def.Sectors.Count; i++)
            {
                var s = def.Sectors[i];
                if (s.Id == Floor1Layout.HubSectorId) continue;
                if (s.Hazard.IsHazardous)
                {
                    Assert(!picked[s.Id], $"A cautious agent never explores {s.Name}, a hazard sector.");
                    Assert(boldPicked[s.Id], $"A bold agent explores {s.Name} at least once from the Hub.");
                    continue;
                }
                Assert(picked[s.Id], $"Sector {s.Name} is picked at least once from the Hub.");
            }
            TestLog.Log($"[PASS] Test 2: From the Hub, {Agents}x{Windows} waypoints all lie, walkable, in other sectors; every sector is visited.");
        }

        private void ValidateDeterministicPerWindow()
        {
            var def = Floor1Layout.Definition;
            var snapshot = Floor1Snapshot(def, out _);
            int changed = 0;
            for (int a = 0; a < Agents; a++)
            {
                var first = ResourceTargeting.PickExplorationPoint(Context(a, 0u, Vec2.Zero, snapshot));
                var again = ResourceTargeting.PickExplorationPoint(Context(a, ResourceTargeting.SectorChoiceTicks - 1, Vec2.Zero, snapshot));
                Assert(first == again, "The same agent and window give the same waypoint.");
                var next = ResourceTargeting.PickExplorationPoint(Context(a, ResourceTargeting.SectorChoiceTicks, Vec2.Zero, snapshot));
                if (next != first) changed++;
            }
            Assert(changed >= Agents / 2, $"A new window gives most agents a new waypoint ({changed}/{Agents}).");
            TestLog.Log($"[PASS] Test 3: Waypoints are deterministic within a {ResourceTargeting.SectorChoiceTicks}-tick window and change between windows.");
        }

        private void ValidateWaypointsAreRoutable()
        {
            var def = Floor1Layout.Definition;
            var snapshot = Floor1Snapshot(def, out _);
            var searcher = new GridAStarSearcher();
            var starts = new[] { Vec2.Zero, new Vec2(-1f, -14f), new Vec2(-16f, 0f), new Vec2(16f, -2.5f) };
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
            TestLog.Log($"[PASS] Test 4: {routes} exploration routes across Floor 1 are all routable.");
        }

        private void ValidateSingleSectorMapFallsBack()
        {
            var def = new MapDefinitionBuilder("OneRoom", new RectF(-10f, -10f, 20f, 20f))
                .Sector(1, "Room", "", new RectF(-10f, -10f, 20f, 20f))
                .Build();
            var snapshot = new SharedWorldSnapshot(24, 24, 1f, 8, null);
            snapshot.SetMap(def);
            var ctx = Context(0, 0u, Vec2.Zero, snapshot);
            Assert(!ResourceTargeting.TryPickSectorWaypoint(ctx, def, out _), "A map with no other sector offers no sector waypoint.");
            float d = Vec2.Distance(ResourceTargeting.PickExplorationPoint(ctx), Vec2.Zero);
            Assert(d >= 4.99f && d <= 9.01f, $"It falls back to the random bearing (got {d:F2}).");
            TestLog.Log("[PASS] Test 5: A single-sector map falls back to the random bearing.");
        }

        /// <summary>
        /// A search is systematic: the agent heads for the charted sector it stood in least recently, so a starving
        /// crawler does not keep circling the rooms it has just searched (Epic MAP Phase 3).
        /// </summary>
        private void ValidateSearchTriesTheLeastRecentlyVisitedSector()
        {
            var def = Floor1Layout.Definition;
            var snapshot = Floor1Snapshot(def, out _);
            var memory = new AgentMemory();
            uint tick = 10;
            for (int i = 0; i < def.Sectors.Count; i++)
            {
                byte id = def.Sectors[i].Id;
                if (id != Floor1Layout.GardenSectorId) memory.NoteSectorVisit(id, tick++);
            }

            var id1 = new EntityId(5u, 0x5eed5eedu, 7u, 11u);
            for (uint w = 0; w < 10; w++)
            {
                var ctx = new AgentContext(id1, Vec2.Zero, new AgentNeeds(90f, 80f, 60f), 0f, snapshot, memory, 500 + w * ResourceTargeting.SectorChoiceTicks);
                Assert(def.FindSector(ResourceTargeting.PickExplorationPoint(ctx)).Id == Floor1Layout.GardenSectorId,
                    "Having searched every other room, the agent heads for the one it never saw: the Garden.");
            }

            memory.NoteSectorVisit(Floor1Layout.GardenSectorId, tick++);
            def.TryGetSector(Floor1Layout.HubSectorId, out var hub);
            var after = new AgentContext(id1, hub.Bounds.Center, new AgentNeeds(90f, 80f, 60f), 0f, snapshot, memory, 900);
            byte next = def.FindSector(ResourceTargeting.PickExplorationPoint(after)).Id;
            Assert(next == def.Sectors[0].Id || next == def.Sectors[1].Id,
                $"Then the room seen longest ago (first visited) comes next, the Hub excluded (got sector {next}).");
            TestLog.Log("[PASS] Test 6: Searches try the sector visited least recently.");
        }

        private static SharedWorldSnapshot Floor1Snapshot(MapDefinition def, out StaticNavGrid grid)
        {
            var cells = def.PartitionCells(1f);
            grid = new StaticNavGrid(cells.X, cells.Y, 1f);
            WallRasterizer.Rasterize(def, grid);
            grid.EnsureRegions();
            var snapshot = new SharedWorldSnapshot(cells.X, cells.Y, 1f, 8, null);
            snapshot.SetStaticGrid(grid);
            snapshot.SetMap(def);
            return snapshot;
        }

        private static AgentContext Context(int agent, uint tick, Vec2 position, SharedWorldSnapshot snapshot, AgentPersonality personality = default)
        {
            var id = new EntityId((uint)agent + 1u, 0x5eed5eedu, 7u, 11u);
            return new AgentContext(id, position, new AgentNeeds(20f, 80f, 60f), 0f, snapshot, new AgentMemory(), tick, personality: personality);
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
