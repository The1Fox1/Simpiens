using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Memory;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer.Unity;

namespace Simpiens.Testing
{
    /// <summary>
    /// Automated validation harness for Epic 5 Phase 2: Memory Decay & Gossip.
    /// Executes on startup to verify all memory retention and social exchange invariants.
    /// </summary>
    public class Epic5Phase2Validator : IStartable
    {
        public void Start()
        {
            Debug.Log("[Epic5Phase2Validator] Beginning automated validation of Epic 5 Phase 2...");

            ValidateMemoryDecay();
            ValidateGossipTransmission();
            ValidateGossipFreshnessUpdate();
            ValidateGossipCooldown();
            ValidateIdleDuration();

            Debug.Log("<color=green>[Epic5Phase2Validator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateMemoryDecay()
        {
            var memory = new AgentMemory();
            var dummySnapshot = new SharedWorldSnapshot(10, 10, 1f, 4, _ => { });

            var pawnId = GUID.Generate();
            var resourceId = GUID.Generate();

            // Seed memory at tick 100
            memory.SpatialMemoryMap[pawnId] = new SpatialMemoryRecord(pawnId, EntityType.Pawn, new Vector2Int(100, 100), 100);
            memory.SpatialMemoryMap[resourceId] = new SpatialMemoryRecord(resourceId, EntityType.Resource, new Vector2Int(200, 200), 100);

            // Agent is located at (0,0) with visionRadius = 10, so locations (100,100) and (200,200) are outside line of sight
            // Tick 450: Pawn age = 350 <= 400 (PawnDecayTicks)
            memory.UpdateMemory(dummySnapshot, Vector2Int.zero, visionRadius: 10, currentTick: 450);
            Assert(memory.SpatialMemoryMap.ContainsKey(pawnId), "Pawn memory should not decay before 400 ticks.");
            Assert(memory.SpatialMemoryMap.ContainsKey(resourceId), "Resource memory should not decay before 1500 ticks.");

            // Tick 550: Pawn age = 450 > 400 (PawnDecayTicks) -> Pawn should be decayed!
            memory.UpdateMemory(dummySnapshot, Vector2Int.zero, visionRadius: 10, currentTick: 550);
            Assert(!memory.SpatialMemoryMap.ContainsKey(pawnId), "Pawn memory should decay after 400 ticks of being unobserved.");
            Assert(memory.SpatialMemoryMap.ContainsKey(resourceId), "Resource memory should still be retained at tick 550.");

            // Tick 1650: Resource age = 1550 > 1500 (ResourceDecayTicks) -> Resource should be decayed!
            memory.UpdateMemory(dummySnapshot, Vector2Int.zero, visionRadius: 10, currentTick: 1650);
            Assert(!memory.SpatialMemoryMap.ContainsKey(resourceId), "Resource memory should decay after 1500 ticks of being unobserved.");

            Debug.Log("[PASS] Test 1: Temporal memory decay & selective pruning passed.");
        }

        private void ValidateGossipTransmission()
        {
            var memoryA = new AgentMemory();
            var memoryB = new AgentMemory();

            var agentAId = GUID.Generate();
            var agentBId = GUID.Generate();
            var resourceId = GUID.Generate();

            // Agent A knows a resource
            memoryA.SpatialMemoryMap[resourceId] = new SpatialMemoryRecord(resourceId, EntityType.Resource, new Vector2Int(15, 25), 100);

            // Agent B does not know about it
            Assert(!memoryB.SpatialMemoryMap.ContainsKey(resourceId), "Agent B initially has no knowledge.");

            // Gossip exchange at tick 120
            bool gossiped = memoryA.TryGossip(memoryB, agentBId, agentAId, currentTick: 120);
            Assert(gossiped, "Gossip should succeed between two fresh agents.");

            // Verify Agent B learned the resource
            Assert(memoryB.SpatialMemoryMap.ContainsKey(resourceId), "Agent B should have learned Resource through gossip.");
            var recordB = memoryB.SpatialMemoryMap[resourceId];
            Assert(recordB.LastKnownLocation == new Vector2Int(15, 25), "Learned location matches Agent A's memory.");
            Assert(recordB.LastSeenTick == 100, "Learned timestamp matches Agent A's observation tick.");

            // Verify ledger entries
            Assert(memoryA.EventLedger.Count > 0 && memoryA.EventLedger.GetEvent(memoryA.EventLedger.Count - 1).EventType == MemoryEventType.GossipShared, "Agent A ledger recorded GossipShared.");
            Assert(memoryB.EventLedger.Count > 0 && memoryB.EventLedger.GetEvent(memoryB.EventLedger.Count - 1).EventType == MemoryEventType.GossipReceived, "Agent B ledger recorded GossipReceived.");

            Debug.Log("[PASS] Test 2: Gossip transmission of spatial knowledge & ledger passed.");
        }

        private void ValidateGossipFreshnessUpdate()
        {
            var memoryA = new AgentMemory();
            var memoryB = new AgentMemory();

            var agentAId = GUID.Generate();
            var agentBId = GUID.Generate();
            var sharedResourceId = GUID.Generate();

            // Agent A has stale intel: saw at (10, 10) on tick 50
            memoryA.SpatialMemoryMap[sharedResourceId] = new SpatialMemoryRecord(sharedResourceId, EntityType.Resource, new Vector2Int(10, 10), 50);

            // Agent B has newer intel: saw at (20, 20) on tick 150
            memoryB.SpatialMemoryMap[sharedResourceId] = new SpatialMemoryRecord(sharedResourceId, EntityType.Resource, new Vector2Int(20, 20), 150);

            // Gossip exchange
            memoryB.TryGossip(memoryA, agentAId, agentBId, currentTick: 200);

            // Agent A's memory should have updated to Agent B's newer intel
            var updatedRecordA = memoryA.SpatialMemoryMap[sharedResourceId];
            Assert(updatedRecordA.LastKnownLocation == new Vector2Int(20, 20), "Agent A should adopt the newer coordinates.");
            Assert(updatedRecordA.LastSeenTick == 150, "Agent A should adopt the newer timestamp.");

            Debug.Log("[PASS] Test 3: Gossip intel freshness update passed.");
        }

        private void ValidateGossipCooldown()
        {
            var memoryA = new AgentMemory();
            var memoryB = new AgentMemory();

            var agentAId = GUID.Generate();
            var agentBId = GUID.Generate();

            // First gossip at tick 100
            bool first = memoryA.TryGossip(memoryB, agentBId, agentAId, currentTick: 100, cooldownTicks: 300);
            Assert(first, "Initial gossip should succeed.");

            // Immediate second gossip at tick 150 should be rejected by cooldown
            bool second = memoryA.TryGossip(memoryB, agentBId, agentAId, currentTick: 150, cooldownTicks: 300);
            Assert(!second, "Rapid subsequent gossip attempt within cooldown should fail.");

            // Gossip at tick 401 (cooldown has expired) should succeed
            bool third = memoryA.TryGossip(memoryB, agentBId, agentAId, currentTick: 401, cooldownTicks: 300);
            Assert(third, "Gossip after cooldown expiry should succeed.");

            Debug.Log("[PASS] Test 4: Gossip cooldown enforcement passed.");
        }

        private void ValidateIdleDuration()
        {
            var agentId = GUID.Generate();

            var defaultIdle = new IdleIntent(agentId);
            Assert(Mathf.Approximately(defaultIdle.Duration, 2.0f), "Default IdleIntent duration is 2.0s.");

            var customIdle = new IdleIntent(agentId, 4.5f);
            Assert(Mathf.Approximately(customIdle.Duration, 4.5f), "Custom IdleIntent duration is 4.5s.");

            Debug.Log("[PASS] Test 5: IdleIntent duration retention passed.");
        }

        private void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Debug.LogError($"[ASSERTION FAILED] {message}");
                throw new System.Exception($"Assertion failed: {message}");
            }
        }
    }
}
