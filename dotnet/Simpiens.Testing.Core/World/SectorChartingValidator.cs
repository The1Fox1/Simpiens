using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Planning;
using Simpiens.Core;
using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;

namespace Simpiens.Testing.World
{
    /// <summary>
    /// Fog of war over uncharted sectors (Epic MAP Phase 3, producer decision D1): an adventurer does not know what
    /// is inside an uncharted room until a scout visits it and successfully reports back. Mechanism checks on
    /// Floor 1, with the Deep Seam uncharted.
    /// </summary>
    public class SectorChartingValidator
    {
        private static readonly Vec2 QuarryBelowSeamDoor = new Vec2(-16f, 4f);
        private static readonly Vec2 SeamNode = new Vec2(-16f, 10f);
        private static readonly Vec2 OvergrowthNode = new Vec2(16f, 10f);

        private const byte Seam = Floor1Layout.DeepSeamSectorId;

        private uint _nextId = 1;

        public void RunAllValidationTests()
        {
            TestLog.Log("[SectorChartingValidator] Beginning fog-of-war validation...");

            ValidateExploredMask();
            ValidateNoPerceptionThroughTheFog();
            ValidateScoutKeepsPrivateSurveyNotes();
            ValidateSurveyIsNotGossiped();
            ValidateReportPublishesTheChart();
            ValidateUnchartedGatherBountiesCannotBeClaimed();
            ValidateExplorationAvoidsUnchartedSectors();

            TestLog.Log("<color=green>[SectorChartingValidator] ALL 7 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateExploredMask()
        {
            var sectors = new SectorRegistry();
            sectors.Load(Floor1Layout.Definition);
            ulong mask = sectors.ExploredMask;
            for (int i = 0; i < Floor1Layout.Definition.Sectors.Count; i++)
            {
                var s = Floor1Layout.Definition.Sectors[i];
                Assert(((mask >> s.Id) & 1UL) == (s.StartsExplored ? 1UL : 0UL), $"Mask bit {s.Id} ({s.Name}) matches its starting exploration.");
            }
            sectors.SetExplored(Seam, true);
            Assert(((sectors.ExploredMask >> Seam) & 1UL) == 1UL, "Charting the Deep Seam sets its bit.");

            var snapshot = Floor1Snapshot(uncharted: true);
            Assert(!snapshot.IsCharted(SeamNode) && snapshot.IsCharted(QuarryBelowSeamDoor), "The snapshot knows the Deep Seam is uncharted and the quarry is not.");
            var noMap = new SharedWorldSnapshot(8, 8, 1f, 4, null);
            Assert(noMap.IsCharted(SeamNode), "Without a map everything is charted.");
            TestLog.Log("[PASS] Test 1: The explored-sector mask follows the registry and reaches the snapshot.");
        }

        private void ValidateNoPerceptionThroughTheFog()
        {
            var snapshot = Floor1Snapshot(uncharted: true);
            var node = AddResource(snapshot, SeamNode);
            Assert(snapshot.HasLineOfSight(QuarryBelowSeamDoor, SeamNode), "Precondition: the seam node is in line of sight through the door.");

            var memory = new AgentMemory();
            memory.UpdateMemory(snapshot, GridPoint.RoundToInt(QuarryBelowSeamDoor), 6, 10);
            Assert(!memory.SpatialMemoryMap.ContainsKey(node), "A crawler looking through the door learns nothing inside the uncharted Deep Seam.");
            Assert(memory.SurveyNoteCount == 0, "And keeps no survey notes either.");

            var charted = Floor1Snapshot(uncharted: false);
            var sameNode = AddResource(charted, SeamNode);
            memory.UpdateMemory(charted, GridPoint.RoundToInt(QuarryBelowSeamDoor), 6, 11);
            Assert(memory.SpatialMemoryMap.ContainsKey(sameNode), "Once charted, the same view reveals the node.");
            TestLog.Log("[PASS] Test 2: Nothing inside an uncharted sector is perceived, even in line of sight.");
        }

        private void ValidateScoutKeepsPrivateSurveyNotes()
        {
            var snapshot = Floor1Snapshot(uncharted: true);
            var seam = AddResource(snapshot, SeamNode);
            var overgrowth = AddResource(snapshot, OvergrowthNode);

            var scout = new AgentMemory();
            scout.UpdateMemory(snapshot, new GridPoint(-16, 12), 6, 20, surveySectorId: Seam);
            Assert(!scout.SpatialMemoryMap.ContainsKey(seam), "The scout does not plan on what it surveys before reporting.");
            Assert(scout.SurveyNoteCount == 1, $"The scout notes the seam node (notes {scout.SurveyNoteCount}).");

            var far = new AgentMemory();
            far.UpdateMemory(snapshot, new GridPoint(16, 12), 6, 20, surveySectorId: Seam);
            Assert(far.SurveyNoteCount == 0 && !far.SpatialMemoryMap.ContainsKey(overgrowth), "A Deep Seam survey notes nothing in the Overgrowth.");

            var taken = new List<SpatialMemoryRecord>();
            scout.TakeSurveyNotes(Floor1Layout.OvergrowthSectorId, taken);
            Assert(taken.Count == 0 && scout.SurveyNoteCount == 1, "Notes are taken per sector.");
            TestLog.Log("[PASS] Test 3: The surveying scout keeps private notes of its own sector only.");
        }

        private void ValidateSurveyIsNotGossiped()
        {
            var snapshot = Floor1Snapshot(uncharted: true);
            AddResource(snapshot, SeamNode);
            var scoutId = NextGuid();
            var peerId = NextGuid();
            var scout = new AgentMemory();
            var peer = new AgentMemory();
            scout.UpdateMemory(snapshot, new GridPoint(-16, 12), 6, 30, surveySectorId: Seam);

            Assert(scout.TryGossip(peer, peerId, scoutId, 400), "Precondition: the two gossip.");
            Assert(peer.SpatialMemoryMap.Count == 0, $"Gossip passes on nothing from the survey (peer learned {peer.SpatialMemoryMap.Count}).");
            TestLog.Log("[PASS] Test 4: A survey is never gossiped before the report.");
        }

        private void ValidateReportPublishesTheChart()
        {
            var snapshot = Floor1Snapshot(uncharted: true);
            var seam = AddResource(snapshot, SeamNode);

            var agents = new AgentRegistry();
            var states = new List<AgentState>();
            for (int i = 0; i < 4; i++)
            {
                var state = new AgentState(NextGuid());
                agents.RegisterAgent(state);
                states.Add(state);
            }
            var sim = new SimulationManager(new WorldRegistry(), null, agentRegistry: agents);

            var scout = states[1];
            scout.Memory.UpdateMemory(snapshot, new GridPoint(-16, 12), 6, 40, surveySectorId: Seam);
            Assert(scout.Memory.SurveyNoteCount == 1, "Precondition: the scout surveyed the seam node.");

            Assert(sim.PublishSurvey(states[0].AgentId, Seam) == 0, "Only the scout's own notes are published.");
            int published = sim.PublishSurvey(scout.AgentId, Seam);
            Assert(published == 1, $"The report publishes the scout's survey ({published}).");
            for (int i = 0; i < states.Count; i++)
            {
                Assert(states[i].Memory.SpatialMemoryMap.TryGetValue(seam, out var record) && record.ResourceKind == ItemType.IronOre,
                    $"Agent {i} knows the charted seam node after the report.");
            }
            Assert(scout.Memory.SurveyNoteCount == 0, "The notes are consumed by the report.");
            Assert(sim.PublishSurvey(scout.AgentId, Seam) == 0, "A second report publishes nothing new.");

            // An abandoned survey stays private and unused
            var quitter = new AgentMemory();
            quitter.UpdateMemory(snapshot, new GridPoint(-16, 12), 6, 50, surveySectorId: Seam);
            quitter.UpdateMemory(snapshot, new GridPoint(-16, 12), 6, 51);
            Assert(quitter.SurveyNoteCount == 1 && !quitter.SpatialMemoryMap.ContainsKey(seam), "Without a report the survey stays private notes.");
            TestLog.Log("[PASS] Test 5: A successful report publishes the chart to every agent; an abandoned survey charts nothing.");
        }

        private void ValidateUnchartedGatherBountiesCannotBeClaimed()
        {
            var gather = new BountyContract(1, BountyType.GatherResource, Seam, ItemType.IronOre, 1, 25, 0, 6000);
            var scout = new BountyContract(2, BountyType.ScoutSector, Seam, ItemType.None, 1, 15, 0, 6000);

            var uncharted = Floor1Snapshot(uncharted: true);
            uncharted.SetBounties(new[] { gather, scout }, 2);
            var ctx = Context(uncharted, Vec2.Zero, 100);
            Assert(!BountyQueries.IsFulfillableFrom(ctx, gather), "A Gather bounty in the uncharted Deep Seam cannot be claimed.");
            Assert(BountyQueries.IsFulfillableFrom(ctx, scout), "The Scout bounty for it can.");
            Assert(BountyQueries.TryGetOpenBounty(ctx, BountyType.GatherResource, out _) == false, "No open Gather bounty is offered.");

            var charted = Floor1Snapshot(uncharted: false);
            charted.SetBounties(new[] { gather, scout }, 2);
            Assert(BountyQueries.IsFulfillableFrom(Context(charted, Vec2.Zero, 100), gather), "Once charted, the Gather bounty can be claimed.");

            var claimedScout = new BountyContract(2, BountyType.ScoutSector, Seam, ItemType.None, 1, 15, 0, 6000, claimantId: ctx.AgentId, status: BountyStatus.Claimed);
            uncharted.SetBounties(new[] { claimedScout }, 1);
            Assert(BountyQueries.SurveyingSector(uncharted, ctx.AgentId) == Seam, "The scout's claim names the sector it surveys.");
            Assert(BountyQueries.SurveyingSector(uncharted, NextGuid()) == 0, "Nobody else surveys it.");
            TestLog.Log("[PASS] Test 6: Gather bounties in uncharted sectors stay unclaimed; the scout's claim marks its survey.");
        }

        private void ValidateExplorationAvoidsUnchartedSectors()
        {
            var def = Floor1Layout.Definition;
            var snapshot = Floor1Snapshot(uncharted: true);
            for (int a = 0; a < 12; a++)
            {
                for (uint w = 0; w < 40; w++)
                {
                    var ctx = new AgentContext(new EntityId((uint)a + 1u, 9u, 9u, 9u), Vec2.Zero, new AgentNeeds(20f, 80f, 60f), 0f,
                        snapshot, new AgentMemory(), w * ResourceTargeting.SectorChoiceTicks);
                    var p = ResourceTargeting.PickExplorationPoint(ctx);
                    byte sector = def.FindSector(p).Id;
                    Assert(sector != Floor1Layout.DeepSeamSectorId && sector != Floor1Layout.OvergrowthSectorId, $"Exploration never heads into an uncharted sector (got {sector} at {p}).");
                }
            }
            TestLog.Log("[PASS] Test 7: Exploration only picks charted sectors.");
        }

        /// <summary>Floor 1 with walls; the Deep Seam (and the Overgrowth) charted or not.</summary>
        private static SharedWorldSnapshot Floor1Snapshot(bool uncharted)
        {
            var def = Floor1Layout.Definition;
            var cells = def.PartitionCells(1f);
            var grid = new StaticNavGrid(cells.X, cells.Y, 1f);
            WallRasterizer.Rasterize(def, grid);
            grid.EnsureRegions();
            var snapshot = new SharedWorldSnapshot(cells.X, cells.Y, 1f, 8, null);
            snapshot.SetStaticGrid(grid);
            snapshot.SetMap(def);

            var sectors = new SectorRegistry();
            sectors.Load(def);
            if (!uncharted) sectors.SetAllExplored(true);
            snapshot.SetExploredSectors(sectors.ExploredMask);
            return snapshot;
        }

        private EntityId AddResource(SharedWorldSnapshot snapshot, Vec2 position)
        {
            var id = NextGuid();
            var entity = new EntitySnapshot(id, position, Vec2.Zero, 0.35f, EntityType.Resource, false, 0f, ItemType.IronOre);
            float offset = snapshot.Width * snapshot.CellSize * 0.5f;
            snapshot.AddEntity(SimMath.FloorToInt(position.X + offset), SimMath.FloorToInt(position.Y + snapshot.Height * snapshot.CellSize * 0.5f), in entity);
            return id;
        }

        private AgentContext Context(SharedWorldSnapshot snapshot, Vec2 position, uint tick)
        {
            return new AgentContext(new EntityId(77u, 7u, 7u, 7u), position, new AgentNeeds(20f, 80f, 60f), 0f, snapshot, new AgentMemory(), tick);
        }

        private EntityId NextGuid() => new EntityId(_nextId++, 0xc4a27u, 3u, 5u);

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[SectorChartingValidator ASSERTION FAILED] {message}");
            }
        }
    }
}
