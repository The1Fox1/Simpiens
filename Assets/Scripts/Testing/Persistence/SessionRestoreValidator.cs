using System;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Social;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Persistence
{
    /// <summary>
    /// Milestone 1 completion plan, step 3: every owner restores what it captured. For each part: capture, serialize,
    /// deserialize, restore into a fresh owner that held something else, capture again: the bytes must match. A restored
    /// crawler starts without work in progress; the clock and the id sequence resume where the save left them.
    /// The live round trip of a whole session is SessionRestoreScenario.
    /// </summary>
    public class SessionRestoreValidator
    {
        private static readonly EntityId A = new EntityId(1u, 0u, 0u, 7u);
        private static readonly EntityId B = new EntityId(2u, 0u, 0u, 3u);
        private static readonly EntityId C = new EntityId(3u, 0u, 0u, 9u);

        public void RunAllValidationTests()
        {
            Debug.Log("[SessionRestoreValidator] Beginning session restore validation...");

            ValidateMemoryRoundTrips();
            ValidateLedgerRoundTrips();
            ValidateGroupsRoundTrip();
            ValidateChronicleRoundTrips();
            ValidateSupplyAndFogRoundTrip();
            ValidateCrawlerRecordRoundTripsWithoutWork();
            ValidateClockAndIdsResume();

            Debug.Log("<color=green>[SessionRestoreValidator] ALL 7 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateMemoryRoundTrips()
        {
            var memory = new AgentMemory();
            memory.SpatialMemoryMap[A] = new SpatialMemoryRecord(A, EntityType.Resource, new Vector2Int(4, -2), 120u, ItemType.MedicinalHerb);
            memory.SpatialMemoryMap[C] = new SpatialMemoryRecord(C, EntityType.Stall, new Vector2Int(0, 1), 130u);
            memory.BlacklistEntity(B, 700u);
            memory.SetAffinity(C, new AffinityRecord(-20, 35, 10, 5, 9, 222u));
            var peer = new AgentMemory();
            memory.TryGossip(peer, B, A, 300u); // a gossip cooldown, events on both sides
            for (uint t = 0; t < 60; t++) memory.RecordEvent(MemoryEventType.AgentSpotted, B, new Vector2Int((int)t, 0), 400u + t); // wraps the 50-event ledger
            memory.NoteSectorVisit(7, 499u);

            var fresh = new AgentMemory();
            fresh.SpatialMemoryMap[B] = new SpatialMemoryRecord(B, EntityType.Pawn, Vector2Int.zero, 1u);
            fresh.RecordEvent(MemoryEventType.Attacked, C, Vector2Int.zero, 2u);

            var save = new MemorySave();
            memory.CaptureTo(save);
            var bytes = SaveSerializer.Serialize(save);
            fresh.RestoreFrom(SaveSerializer.Deserialize<MemorySave>(bytes));
            var again = new MemorySave();
            fresh.CaptureTo(again);

            AssertSameBytes(bytes, SaveSerializer.Serialize(again), "A memory restored from its save");
            Assert(!fresh.SpatialMemoryMap.ContainsKey(B), "What the fresh memory held before the load is gone.");
            Assert(fresh.EventLedger.Count == 50 && fresh.EventLedger.GetEvent(49).SimulationTick == 459u, "The event ledger holds the newest 50 events in order.");
            Assert(!fresh.CanGossipWith(B, 400u) && fresh.CanGossipWith(B, 600u), "The gossip cooldown came back.");
            Assert(fresh.IsBlacklisted(B, 650u) && fresh.LastSectorVisit(7) == 500u, "The blacklist and sector visits came back.");

            Debug.Log("[PASS] Test 1: a memory restores sightings, blacklist, gossip cooldowns, affinities, a wrapped event ledger and sector visits.");
        }

        private void ValidateLedgerRoundTrips()
        {
            var ledger = new EconomyLedger();
            var stall = new TradeStallData(A, Vector2.zero, treasuryGold: 900);
            ledger.RecordFaucet(800, "InitialSeed");
            ledger.RecordSink(30, SupplyService.UpkeepSinkName);
            ledger.TryLockEscrow(A, 4, 25, ref stall);
            ledger.TryLockEscrow(A, 2, 15, ref stall);
            var buyer = new Simpiens.Cognition.Inventory.AgentInventory(4, startingGold: 50);
            ledger.TryExecuteStallPurchase(A, ref stall, buyer, 12);

            var fresh = new EconomyLedger();
            fresh.RecordFaucet(5, "Elsewhere");

            var save = new LedgerSave();
            ledger.CaptureTo(save);
            var bytes = SaveSerializer.Serialize(save);
            fresh.RestoreFrom(SaveSerializer.Deserialize<LedgerSave>(bytes));
            var again = new LedgerSave();
            fresh.CaptureTo(again);

            AssertSameBytes(bytes, SaveSerializer.Serialize(again), "A ledger restored from its save");
            Assert(fresh.GetFaucetTotal("Elsewhere") == 0 && fresh.GetFaucetTotal("InitialSeed") == 800, "The restored ledger holds the saved faucets only.");
            Assert(fresh.HasEscrow(4) && fresh.GetEscrowAmount(2) == 15 && fresh.TotalEscrowGold == 40, "Escrows came back.");
            Assert(fresh.Flows.Purchases == 1 && fresh.Flows.PurchaseGold == 12, "Flow counters came back.");

            Debug.Log("[PASS] Test 2: a ledger restores totals, escrows, flows and named faucets and sinks, and drops what it held.");
        }

        private void ValidateGroupsRoundTrip()
        {
            var groups = new GroupRegistry();
            var d = new EntityId(4u, 0u, 0u, 1u);
            byte first = groups.CreateGroup(GroupType.Settlement, A, new Vector2(2f, 3f), 90);
            groups.AddMember(first, d);
            byte second = groups.CreateGroup(GroupType.TemporaryParty, C, new Vector2(-1f, 0f), 40);
            groups.SetDirective(second, new GroupDirective(DirectiveType.DefendAnchor, second, C, new Vector2(-1f, 0f), default, 5u, 900u));
            byte third = groups.CreateGroup(GroupType.FactionGuild, B, new Vector2(5f, 5f), 70);
            groups.DisbandGroup(first); // slot 1 is free; groups 2 and 3 are active

            var fresh = new GroupRegistry();
            fresh.CreateGroup(GroupType.Settlement, new EntityId(9u, 9u, 9u, 9u), Vector2.zero);

            var save = new GroupSave();
            groups.CaptureTo(save);
            var bytes = SaveSerializer.Serialize(save);
            fresh.RestoreFrom(SaveSerializer.Deserialize<GroupSave>(bytes));
            var again = new GroupSave();
            fresh.CaptureTo(again);

            AssertSameBytes(bytes, SaveSerializer.Serialize(again), "A group registry restored from its save");
            Assert(fresh.ActiveGroupCount == 2, $"Two groups are active after the load ({fresh.ActiveGroupCount}).");
            Assert(fresh.TryGetGroup(second, out var g2) && g2.LeaderId == C && g2.ActiveDirective.Type == DirectiveType.DefendAnchor, "Group ids, leaders and directives came back.");
            Assert(fresh.TryGetMembership(B, out var m) && m.GroupId == third && m.Role == RoleRank.Leader, "Memberships came back.");
            Assert(fresh.CreateGroup(GroupType.TemporaryParty, A, Vector2.zero) == first, "The free slot is still the lowest one.");

            Debug.Log("[PASS] Test 3: a group registry restores group ids, leaders, rosters, directives and memberships.");
        }

        private void ValidateChronicleRoundTrips()
        {
            var chronicle = new NarrativeChronicleService(globalCapacity: 8, sectorCapacity: 3);
            for (uint t = 1; t <= 13; t++)
            {
                chronicle.RecordEvent(new ChronicleEvent(ChronicleEventType.CampfireGossip, A, B, (int)(t % 3), t, ChronicleIntensity.Notable));
            }
            var fresh = new NarrativeChronicleService(globalCapacity: 8, sectorCapacity: 3);
            fresh.RecordEvent(new ChronicleEvent(ChronicleEventType.TantrumStarted, C, default, 7, 1u, ChronicleIntensity.Dramatic));

            var save = new ChronicleSave();
            chronicle.CaptureTo(save);
            var bytes = SaveSerializer.Serialize(save);
            fresh.RestoreFrom(SaveSerializer.Deserialize<ChronicleSave>(bytes));
            var again = new ChronicleSave();
            fresh.CaptureTo(again);

            AssertSameBytes(bytes, SaveSerializer.Serialize(again), "A chronicle restored from its save");
            var recent = new System.Collections.Generic.List<ChronicleEvent>();
            fresh.GetSectorEvents(7, recent);
            Assert(recent.Count == 0, "Sector 7's history from before the load is gone.");
            fresh.RecordEvent(new ChronicleEvent(ChronicleEventType.TradeTransaction, A, B, 1, 14u, ChronicleIntensity.Notable));
            fresh.GetAllRecentEvents(recent, 1);
            Assert(recent.Count == 1 && recent[0].EventId == 14UL, $"The event sequence continues after the load (id {(recent.Count > 0 ? recent[0].EventId : 0UL)}).");

            Debug.Log("[PASS] Test 4: a chronicle restores its wrapped history, each sector's history and its sequence.");
        }

        private void ValidateSupplyAndFogRoundTrip()
        {
            var supply = new SupplyService();
            supply.RegisterDormant(C, 450u);
            supply.RegisterDormant(A, 120u);
            var fresh = new SupplyService();
            fresh.RegisterDormant(B, 10u);

            var save = new SupplySave();
            supply.CaptureTo(save);
            var bytes = SaveSerializer.Serialize(save);
            fresh.RestoreFrom(SaveSerializer.Deserialize<SupplySave>(bytes));
            var again = new SupplySave();
            fresh.CaptureTo(again);
            AssertSameBytes(bytes, SaveSerializer.Serialize(again), "A supply service restored from its save");
            Assert(fresh.DormantCount == 2, "The restored queue holds the saved nodes only.");

            var sectors = new SectorRegistry();
            sectors.SetExplored(3, true);
            sectors.SetExplored(200, true);
            var explored = SaveSerializer.Deserialize<bool[]>(SaveSerializer.Serialize(sectors.CaptureExplored()));
            var restored = new SectorRegistry();
            restored.SetExplored(9, true);
            restored.RestoreExplored(explored);
            Assert(restored.IsExplored(3) && restored.IsExplored(200) && !restored.IsExplored(9), "The fog came back, and what the fresh registry charted is gone.");

            Debug.Log("[PASS] Test 5: the regrowth queue, the supply counters and the fog restore.");
        }

        private void ValidateCrawlerRecordRoundTripsWithoutWork()
        {
            var state = new AgentState(A)
            {
                Position = new Vector2(-6.5f, 11.25f),
                Needs = new AgentNeeds(77f, 21f, 64f),
                Frustration = 44f,
                Personality = new AgentPersonality(-0.3f, 0.6f, 0.2f, -0.9f, 0.1f),
                ScoutReportBountyId = 2,
                ScoutReportTick = 312u,
                IsExhaustionCollapsed = true
            };
            state.Activity.RecordSalvageHarvest(80u);
            state.Activity.RecordSalvageSale(95u);
            var record = state.CaptureRecord();
            var back = SaveSerializer.Deserialize<CrawlerRecord>(SaveSerializer.Serialize(record));

            var fresh = new AgentState(A);
            fresh.ActivePlan.Clear();
            fresh.ActiveGoal = new Simpiens.Cognition.Planning.Goals.RestGoal();
            fresh.CurrentIntent = new IdleIntent(A);
            fresh.HasActiveIntent = true;
            fresh.IsEvaluating = true;
            fresh.VisualState = Simpiens.Entities.AgentVisualState.Walking;
            fresh.RestoreRecord(back);

            AssertSameBytes(SaveSerializer.Serialize(record), SaveSerializer.Serialize(fresh.CaptureRecord()), "A crawler record restored and captured again");
            Assert(fresh.ActiveGoal == null && fresh.CurrentIntent == null && !fresh.HasActiveIntent && !fresh.IsEvaluating, "A loaded crawler has no work in progress.");
            Assert(fresh.VisualState == Simpiens.Entities.AgentVisualState.Resting, "A collapsed crawler still looks collapsed; otherwise its look starts idle.");
            Assert(fresh.Activity.Cycles == 1 && fresh.Activity.FirstCycleTick == 95, "The activity tally came back.");

            bool refused = false;
            try
            {
                new AgentState(B).RestoreRecord(back);
            }
            catch (ArgumentException)
            {
                refused = true;
            }
            Assert(refused, "A record is refused by a crawler with another id.");

            Debug.Log("[PASS] Test 6: a crawler record restores every saved field and starts without work in progress.");
        }

        private void ValidateClockAndIdsResume()
        {
            var clock = new SimulationClock();
            clock.AdvanceTicks(5);
            clock.ResumeAt(3000);
            Assert(clock.CurrentTick == 3000, "The clock resumes at the saved tick.");
            clock.AdvanceTicks(1);
            Assert(clock.CurrentTick == 3001, "The clock keeps counting from there.");

            var settings = new SimulationSettings(424242UL);
            var original = new EntityIdAllocator(settings);
            for (int i = 0; i < 17; i++) original.Next();
            long counter = original.Counter;
            var resumed = new EntityIdAllocator(settings);
            resumed.Next();
            resumed.ResumeAt(counter);
            Assert(counter == 17, $"The counter counts the ids handed out ({counter}).");
            Assert(resumed.Next() == original.Next() && resumed.Next() == original.Next(), "A resumed id sequence continues the saved one.");

            Debug.Log("[PASS] Test 7: the clock and the id sequence resume where the save left them.");
        }

        private static void AssertSameBytes(byte[] a, byte[] b, string what)
        {
            Assert(a.Length == b.Length, $"{what}: {a.Length} vs {b.Length} bytes.");
            for (int i = 0; i < a.Length; i++)
            {
                Assert(a[i] == b[i], $"{what}: the bytes differ at offset {i}.");
            }
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
