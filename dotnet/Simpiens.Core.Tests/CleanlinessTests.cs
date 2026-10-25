using System;
using System.Collections.Generic;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Pathfinding;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using Xunit;

namespace Simpiens.Core.Tests
{
    public class CleanlinessTests
    {
        [Fact]
        public void PathResponse_ReturnToPool_IsIdempotent()
        {
            int returnCount = 0;
            var response = new PathResponse();
            response.Initialize(10, r => returnCount++);
            Assert.False(response.IsRented);

            response.MarkRented();
            Assert.True(response.IsRented);

            // First return succeeds
            response.ReturnToPool();
            Assert.Equal(1, returnCount);
            Assert.False(response.IsRented);

            // Duplicate return is a safe no-op
            response.ReturnToPool();
            Assert.Equal(1, returnCount);
            Assert.False(response.IsRented);
        }

        [Fact]
        public void SimulationManager_RejectsZeroBountyId()
        {
            var sim = new SimulationManager(new WorldRegistry(), new SpatialHashGrid());
            var bounty = new BountyContract(0, BountyType.GatherResource, 0, ItemType.IronOre, 1, 10, 0, 100);
            Assert.False(sim.TryRegisterBounty(bounty));
        }

        [Fact]
        public void SimulationManager_RejectsDuplicateActiveBountyId()
        {
            var sim = new SimulationManager(new WorldRegistry(), new SpatialHashGrid());
            var stallId = new EntityId(1, 2, 3, 4);
            sim.RegisterTradeStall(new TradeStallData(stallId, Vec2.Zero, treasuryGold: 1000));

            var bounty1 = new BountyContract(42, BountyType.GatherResource, 0, ItemType.IronOre, 1, 10, 0, 100, creatorStallId: stallId);
            Assert.True(sim.TryRegisterBounty(bounty1));

            // Attempting to register another active bounty with ID 42 must be rejected
            var bounty2 = new BountyContract(42, BountyType.GatherResource, 0, ItemType.MedicinalHerb, 1, 20, 0, 100, creatorStallId: stallId);
            Assert.False(sim.TryRegisterBounty(bounty2));
        }

        [Fact]
        public void SimulationManager_AllocatesNonCollidingBountyIdsWhenZero()
        {
            var interventions = new PlayerInterventionService();
            var sim = new SimulationManager(new WorldRegistry(), new SpatialHashGrid(), interventionService: interventions);
            var stallId = new EntityId(1, 2, 3, 4);
            sim.RegisterTradeStall(new TradeStallData(stallId, Vec2.Zero, treasuryGold: 1000));

            // Pre-register bounty ID 1 explicitly
            sim.TryRegisterBounty(new BountyContract(1, BountyType.GatherResource, 0, ItemType.IronOre, 1, 10, 0, 100, creatorStallId: stallId));

            // Post command with BountyId = 0 should allocate ID 2
            interventions.QueueInfluence(InfluenceEvent.CreatePostBounty("Player",
                new PostBountyCommand(0, BountyType.GatherResource, 0, ItemType.IronOre, 1, 10, 100, default, stallId)));

            sim.Tick(1);

            Assert.True(sim.TryGetBounty(1, out _));
            Assert.True(sim.TryGetBounty(2, out var allocatedBounty));
            Assert.Equal(2, allocatedBounty.BountyId);
        }

        [Fact]
        public void SuccessionEngine_SortsTiedCandidatesDeterministicallyByEntityId()
        {
            var registry = new GroupRegistry();
            var leaderId = new EntityId(10, 0, 0, 0);
            var groupId = registry.CreateGroup(GroupType.Settlement, leaderId, Vec2.Zero, initialCohesion: 100);

            var idA = new EntityId(20, 0, 0, 0);
            var idB = new EntityId(30, 0, 0, 0);
            var idC = new EntityId(40, 0, 0, 0);

            registry.AddMember(groupId, idC, RoleRank.Follower);
            registry.AddMember(groupId, idA, RoleRank.Follower);
            registry.AddMember(groupId, idB, RoleRank.Follower);

            var worldRegistry = new WorldRegistry();
            var result = SuccessionEngine.ResolveSuccession(groupId, registry, worldRegistry, 0);

            // Since all three members have RoleRank.Follower, TotalRespect = 0, Allegiance = 50,
            // idA has the lowest EntityId value and must win deterministically
            Assert.Equal(idA, result.NewLeaderId);
            Assert.Equal(SuccessionMode.Peaceful, result.Mode);
        }
    }
}
