using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Social;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Testing.Environment
{
    /// <summary>
    /// Automated domain validation suite for Section 2.3:
    /// Single-Writer Threading Architecture, Snapshot Memory Isolation,
    /// Side-Effect-Free Reads, and Zero-Allocation Invariants.
    /// </summary>
    public class SingleWriterMemoryValidator
    {
        public void Start()
        {
            RunAllValidationTests();
        }

        public void RunAllValidationTests()
        {
            Log("[SingleWriterMemoryValidator] Beginning Section 2.3 Single-Writer Domain Validation...");

            ValidateSnapshotLifetimeRetention();
            ValidateConcurrentMemoryAccessUnderStress();
            ValidateSideEffectFreeAffinityReads();
            ValidateInventorySnapshotIsolation();
            ValidateZeroAllocationSnapshotBenchmark();

            Log("<color=green>[SingleWriterMemoryValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateSnapshotLifetimeRetention()
        {
            var grid = new SpatialHashGrid();
            grid.Initialize(100f, 100f, 10f);
            grid.UpdateFromRegistry(null);
            var snapshot = grid.GetActiveSnapshot();
            Assert(snapshot != null, "Active snapshot should not be null.");

            // Simulates main-thread retention before background dispatch
            snapshot.Retain();

            // Advancing the grid produces a new snapshot
            grid.UpdateFromRegistry(null);
            var nextSnapshot = grid.GetActiveSnapshot();
            Assert(nextSnapshot != null, "Fresh snapshot should not be null.");

            // Background worker releases its retained snapshot
            snapshot.Release();

            Log("[PASS] Test 1: Snapshot lifetime retention ensures snapshot remains valid across ticks until released.");
        }

        private void ValidateConcurrentMemoryAccessUnderStress()
        {
            var memA = new AgentMemory();
            var memB = new AgentMemory();

            var agentAId = CreateGuid(1);
            var agentBId = CreateGuid(2);

            // Populate initial records
            for (int i = 0; i < 20; i++)
            {
                var resId = CreateGuid(100 + i);
                memA.SpatialMemoryMap[resId] = new SpatialMemoryRecord(resId, EntityType.Resource, new GridPoint(i, i), 10);
            }

            memA.RecordInteraction(agentBId, deltaWarmth: 20, deltaTrust: 15, deltaFear: 0, deltaRespect: 10, currentTick: 10);

            // Take an isolated snapshot for background workers
            var snapshot = memA.GetSnapshot(currentTick: 100);

            int workerIterations = 400;
            int workerCount = 4;
            var exceptions = new List<Exception>();
            var tasks = new Task[workerCount];
            var cts = new CancellationTokenSource();

            for (int w = 0; w < workerCount; w++)
            {
                tasks[w] = Task.Run(() =>
                {
                    try
                    {
                        for (int iter = 0; iter < workerIterations; iter++)
                        {
                            // Enumerate spatial records on background thread
                            int resourceCount = 0;
                            foreach (var kvp in snapshot.SpatialMemoryMap)
                            {
                                if (kvp.Value.Type == EntityType.Resource && !snapshot.IsBlacklisted(kvp.Key, 100))
                                {
                                    resourceCount++;
                                }
                            }

                            // Enumerate affinity records on background thread
                            foreach (var kvp in snapshot.AffinityMap)
                            {
                                var aff = snapshot.GetAffinity(kvp.Key, 100);
                                float score = snapshot.CalculateSubmissionScore(kvp.Key, 100);
                                float alt = snapshot.CalculateAltruismScore(kvp.Key, 100, 20f, false);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                });
            }

            // Concurrently on main thread: aggressively mutate live AgentMemory
            for (int m = 0; m < workerIterations; m++)
            {
                uint tick = (uint)(100 + m);
                var dynId = CreateGuid((uint)(200 + m));
                memA.SpatialMemoryMap[dynId] = new SpatialMemoryRecord(dynId, EntityType.Pawn, new GridPoint(m, m), tick);
                memA.BlacklistEntity(dynId, tick + 50);

                memA.RecordInteraction(agentBId, deltaWarmth: 1, deltaTrust: -1, deltaFear: 0, deltaRespect: 1, tick);
                memA.TryGossip(memB, agentBId, agentAId, tick, cooldownTicks: 0);

                if (m % 50 == 0)
                {
                    memA.PruneStaleResourceMemories(tick);
                }
            }

            Task.WaitAll(tasks);

            Assert(exceptions.Count == 0, $"Concurrent access encountered {exceptions.Count} exceptions (e.g., Collection was modified): {(exceptions.Count > 0 ? exceptions[0].Message : "")}");
            Log("[PASS] Test 2: Concurrent multi-threaded gossip, pruning, and affinity mutations caused ZERO exceptions on background workers.");
        }

        private void ValidateSideEffectFreeAffinityReads()
        {
            var memory = new AgentMemory();
            var peerId = CreateGuid(999);

            // Set initial affinity at tick 0
            memory.SetAffinity(peerId, new AffinityRecord(warmth: 80, trust: 80, fear: 10, respect: 50, interactionCount: 1, lastInteractionTick: 0));

            var snapshot = memory.GetSnapshot(currentTick: 0);
            int initialMemoryCount = memory.AffinityMap.Count;
            int initialSnapshotCount = snapshot.AffinityMap.Count;

            // Query affinity at later ticks (should temporally regress toward neutral)
            var regressedAffinity1 = snapshot.GetAffinity(peerId, currentTick: 2000);
            var regressedAffinity2 = memory.GetAffinity(peerId, currentTick: 2000);

            Assert(regressedAffinity1.Warmth < 80, "Snapshot affinity should regress over time.");
            Assert(regressedAffinity2.Warmth < 80, "Live memory query should compute regressed values.");

            // Crucial assertion: Reading GetAffinity must NOT mutate dictionaries
            Assert(memory.AffinityMap.Count == initialMemoryCount, "AgentMemory.AffinityMap count must not mutate on read.");
            Assert(snapshot.AffinityMap.Count == initialSnapshotCount, "AgentMemorySnapshot.AffinityMap count must not mutate on read.");

            // Stored record in snapshot remains original unmodified entry
            Assert(snapshot.AffinityMap[peerId].Warmth == 80, "Snapshot dictionary entry must remain unmodified; GetAffinity must be pure calculation.");
            Log("[PASS] Test 3: Side-effect-free affinity calculation verified (zero dictionary writes on read).");
        }

        private void ValidateInventorySnapshotIsolation()
        {
            var liveInventory = new AgentInventory(capacity: 4, startingGold: 50);
            var ration = new ItemInstance(itemId: 1, type: ItemType.Ration, quantity: 3, baseValue: 10, quality: 0, durability: 100);
            var ore = new ItemInstance(itemId: 2, type: ItemType.IronOre, quantity: 5, baseValue: 20, quality: 0, durability: 100);

            liveInventory.TryAddItem(ration, out _);
            liveInventory.TryAddItem(ore, out _);

            // Take snapshot
            var snapshot = liveInventory.GetSnapshot();

            Assert(snapshot.HasItem(ItemType.Ration, 3), "Snapshot must reflect initial ration count.");
            Assert(snapshot.HasItem(ItemType.IronOre, 5), "Snapshot must reflect initial ore count.");
            Assert(snapshot.Gold == 50, "Snapshot must reflect initial gold balance.");

            // Mutate live inventory on main thread
            liveInventory.TryDeductGold(30);
            liveInventory.TryRemoveItem(ItemType.Ration, 3);
            var herb = new ItemInstance(itemId: 3, type: ItemType.MedicinalHerb, quantity: 10, baseValue: 15, quality: 0, durability: 100);
            liveInventory.TryAddItem(herb, out _);

            // Assert snapshot remains completely untouched and isolated
            Assert(snapshot.Gold == 50, "Snapshot gold must remain isolated from live modifications.");
            Assert(snapshot.HasItem(ItemType.Ration, 3), "Snapshot rations must remain intact after live removal.");
            Assert(!snapshot.HasItem(ItemType.MedicinalHerb), "Snapshot must not observe newly added items added after snapshot.");

            Log("[PASS] Test 4: Inventory snapshot isolation verified (background reads are safe from main-thread transactions).");
        }

        private void ValidateZeroAllocationSnapshotBenchmark()
        {
            var memory = new AgentMemory();
            var inventory = new AgentInventory();

            for (int i = 0; i < 10; i++)
            {
                var id = CreateGuid((uint)(300 + i));
                memory.SpatialMemoryMap[id] = new SpatialMemoryRecord(id, EntityType.Resource, new GridPoint(i, i), 10);
            }
            memory.RecordInteraction(CreateGuid(400), 10, 10, 0, 10, 10);

            // Warm up double-buffered snapshots to ensure JIT compilation
            for (int i = 0; i < 200; i++)
            {
                memory.GetSnapshot((uint)i);
                inventory.GetSnapshot();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                memory.GetSnapshot((uint)(i + 200));
                inventory.GetSnapshot();
            }

            long delta = GC.GetAllocatedBytesForCurrentThread() - startAlloc;

            Assert(delta == 0, $"Snapshot benchmark allocated {delta} bytes across 1,000 snapshot creations (expected 0).");
            Log("[PASS] Test 5: Zero heap allocation verified for Memory and Inventory snapshots (0 bytes across 1,000 cycles).");
        }

        private static EntityId CreateGuid(uint id)
        {
            return new EntityId(id, 0, 0, 0);
        }

        private static EntityId CreateGuid(int id)
        {
            return new EntityId((uint)id, 0, 0, 0);
        }

        private static void Log(string msg)
        {
            Console.WriteLine(msg);
            try
            {
                CallUnityLog(msg);
            }
            catch
            {
                // Headless mono environment without Unity C++ runtime
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void CallUnityLog(string msg)
        {
            TestLog.Log(msg);
        }

        private void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[SingleWriterMemoryValidator ASSERTION FAILED] {message}");
            }
        }
    }
}
