using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session.Layouts;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Spatial;
using Simpiens.Testing.Sessions;

namespace Simpiens.Testing.Persistence
{
    /// <summary>
    /// Milestone 1 completion plan, step 2: each owner's capture is plain data in a fixed order. Filling a memory, a
    /// ledger or a group registry in a different order gives the same bytes; the chronicle and the regrowth queue keep
    /// their own order; a crawler's record copies every saved field; the map fingerprint tells maps apart.
    /// The live capture of a whole session is SessionCaptureScenario.
    /// </summary>
    public class SessionCaptureValidator
    {
        private static readonly EntityId A = new EntityId(1u, 0u, 0u, 7u);
        private static readonly EntityId B = new EntityId(2u, 0u, 0u, 3u);
        private static readonly EntityId C = new EntityId(3u, 0u, 0u, 9u);

        public void RunAllValidationTests()
        {
            TestLog.Log("[SessionCaptureValidator] Beginning session capture validation...");

            ValidateMemoryCaptureIgnoresInsertionOrder();
            ValidateLedgerCaptureIgnoresRecordingOrder();
            ValidateGroupCaptureIgnoresJoiningOrder();
            ValidateChronicleCaptureKeepsHistoryOrder();
            ValidateSupplyCaptureKeepsRegrowthOrder();
            ValidateCrawlerRecordCopiesEverySavedField();
            ValidateMapFingerprintTellsMapsApart();

            TestLog.Log("<color=green>[SessionCaptureValidator] ALL 7 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateMemoryCaptureIgnoresInsertionOrder()
        {
            var forward = BuildMemory(new[] { A, B, C });
            var backward = BuildMemory(new[] { C, B, A });

            var saveF = new MemorySave();
            var saveB = new MemorySave();
            forward.CaptureTo(saveF);
            backward.CaptureTo(saveB);

            AssertSameBytes(SaveSerializer.Serialize(saveF), SaveSerializer.Serialize(saveB), "Two memories filled in opposite orders");
            Assert(saveF.Sightings.Length == 3 && saveF.Sightings[0].EntityId == A && saveF.Sightings[2].EntityId == C, "Sightings are sorted by entity id.");
            Assert(saveF.Blacklist.Length == 3 && saveF.Affinities.Length == 3, "Every blacklist entry and affinity is captured.");
            Assert(saveF.Events.Length == 2 && saveF.Events[0].SimulationTick == 10 && saveF.Events[1].SimulationTick == 20, "The event ledger is oldest first.");
            Assert(saveF.SectorVisits.Length == 64 && saveF.SectorVisits[5] == 41, "Sector visits are captured (stored as tick + 1).");

            TestLog.Log("[PASS] Test 1: a memory captures sorted sightings, blacklist and affinities, the ledger oldest first, and sector visits; fill order doesn't matter.");
        }

        private static AgentMemory BuildMemory(EntityId[] order)
        {
            var memory = new AgentMemory();
            for (int i = 0; i < order.Length; i++)
            {
                var id = order[i];
                memory.SpatialMemoryMap[id] = new SpatialMemoryRecord(id, EntityType.Resource, new GridPoint((int)id.GetHashCode() % 7, 3), 100u, ItemType.IronOre);
                memory.BlacklistEntity(id, 900u);
                memory.SetAffinity(id, new AffinityRecord(10, 20, 5, -3, 4, 77u));
            }
            memory.RecordEvent(MemoryEventType.ResourceSpotted, A, new GridPoint(1, 2), 10u);
            memory.RecordEvent(MemoryEventType.GossipShared, B, GridPoint.Zero, 20u);
            memory.NoteSectorVisit(5, 40u);
            return memory;
        }

        private void ValidateLedgerCaptureIgnoresRecordingOrder()
        {
            var first = new EconomyLedger();
            var second = new EconomyLedger();
            var stall = new TradeStallData(A, Vec2.Zero, treasuryGold: 1000);
            var stallCopy = stall;

            first.RecordFaucet(500, "InitialSeed");
            first.RecordFaucet(30, SupplyService.SalvageSalesFaucetName);
            first.RecordSink(20, SupplyService.UpkeepSinkName);
            first.RecordSink(15, SupplyService.CaravanSinkName);
            first.TryLockEscrow(A, 2, 25, ref stall);
            first.TryLockEscrow(A, 1, 40, ref stall);

            second.TryLockEscrow(A, 1, 40, ref stallCopy);
            second.TryLockEscrow(A, 2, 25, ref stallCopy);
            second.RecordSink(15, SupplyService.CaravanSinkName);
            second.RecordSink(20, SupplyService.UpkeepSinkName);
            second.RecordFaucet(30, SupplyService.SalvageSalesFaucetName);
            second.RecordFaucet(500, "InitialSeed");

            var saveF = new LedgerSave();
            var saveS = new LedgerSave();
            first.CaptureTo(saveF);
            second.CaptureTo(saveS);

            AssertSameBytes(SaveSerializer.Serialize(saveF), SaveSerializer.Serialize(saveS), "Two ledgers given the same records in different orders");
            Assert(saveF.Escrows.Length == 2 && saveF.Escrows[0].BountyId == 1 && saveF.Escrows[0].Amount == 40, "Escrows are sorted by bounty id.");
            Assert(saveF.Totals.Escrow == 65 && saveF.Totals.Fauceted == 530 && saveF.Totals.Sunk == 35, "The running totals are captured.");
            Assert(saveF.FaucetNames.Length == 2 && string.CompareOrdinal(saveF.FaucetNames[0], saveF.FaucetNames[1]) < 0, "Named totals are in ordinal name order.");

            TestLog.Log("[PASS] Test 2: a ledger captures its totals, escrows by bounty id and named totals by name; recording order doesn't matter.");
        }

        private void ValidateGroupCaptureIgnoresJoiningOrder()
        {
            var first = new GroupRegistry();
            var second = new GroupRegistry();
            var directive = new GroupDirective(DirectiveType.RallyAtLocation, 1, A, new Vec2(4f, 5f), default, 10u, 500u);

            byte g1 = first.CreateGroup(GroupType.Settlement, A, new Vec2(1f, 2f), 80);
            first.AddMember(g1, C);
            first.AddMember(g1, B, RoleRank.Officer, 60);
            first.SetDirective(g1, directive);

            byte g2 = second.CreateGroup(GroupType.Settlement, A, new Vec2(1f, 2f), 80);
            second.AddMember(g2, B, RoleRank.Officer, 60);
            second.AddMember(g2, C);
            second.SetDirective(g2, directive);

            var saveF = new GroupSave();
            var saveS = new GroupSave();
            first.CaptureTo(saveF);
            second.CaptureTo(saveS);

            AssertSameBytes(SaveSerializer.Serialize(saveF), SaveSerializer.Serialize(saveS), "Two registries whose members joined in different orders");
            Assert(saveF.Groups.Length == 1 && saveF.Groups[0].GroupId == g1 && saveF.Groups[0].MemberCount == 3 && saveF.Groups[0].Cohesion == 80, "The active group is captured with its cohesion and roster size.");
            Assert(saveF.Members[0] == A && saveF.Members[1] == B && saveF.Members[2] == C, "Members are sorted by id.");
            Assert(saveF.Memberships.Length == 3 && saveF.Memberships[1].Membership.Role == RoleRank.Officer, "Memberships are sorted by id and keep their role.");
            Assert(saveF.Groups[0].ActiveDirective.Type == DirectiveType.RallyAtLocation, "The active directive is captured.");

            TestLog.Log("[PASS] Test 3: a group registry captures active groups, sorted rosters and memberships; joining order doesn't matter.");
        }

        private void ValidateChronicleCaptureKeepsHistoryOrder()
        {
            var chronicle = new NarrativeChronicleService(globalCapacity: 4, sectorCapacity: 2);
            for (uint t = 1; t <= 6; t++)
            {
                int sector = t == 1 ? 9 : (t % 2 == 0 ? 2 : 1);
                chronicle.RecordEvent(new ChronicleEvent(ChronicleEventType.TradeTransaction, A, B, sector, t, ChronicleIntensity.Notable));
            }

            var save = new ChronicleSave();
            chronicle.CaptureTo(save);

            Assert(save.Sequence == 6, $"The event sequence is captured ({save.Sequence}).");
            Assert(save.Events.Length == 4 && save.Events[0].SimulationTick == 3 && save.Events[3].SimulationTick == 6, "The facility's history is its newest 4 events, oldest first.");
            Assert(save.Sectors.Length == 3 && save.Sectors[0].SectorId == 1 && save.Sectors[1].SectorId == 2 && save.Sectors[2].SectorId == 9, "Sector histories are sorted by sector id.");
            var nine = save.Sectors[2];
            Assert(nine.Count == 1 && save.SectorEvents[nine.Start].SimulationTick == 1, "A sector keeps history older than the facility's buffer.");
            var two = save.Sectors[1];
            Assert(two.Count == 2 && save.SectorEvents[two.Start].SimulationTick == 4 && save.SectorEvents[two.Start + 1].SimulationTick == 6, "A sector's history is its newest events, oldest first.");

            chronicle.Clear();
            var cleared = new ChronicleSave();
            chronicle.CaptureTo(cleared);
            Assert(cleared.Events.Length == 0 && cleared.Sectors.Length == 0 && cleared.Sequence == 6, "A cleared chronicle captures no history but keeps its sequence.");

            TestLog.Log("[PASS] Test 4: the chronicle captures its sequence, its history oldest first and each sector's own history.");
        }

        private void ValidateSupplyCaptureKeepsRegrowthOrder()
        {
            var supply = new SupplyService();
            supply.RegisterDormant(C, 300u);
            supply.RegisterDormant(B, 100u);
            supply.RegisterDormant(A, 300u);

            var save = new SupplySave();
            supply.CaptureTo(save);

            Assert(save.Dormant.Length == 3, "Every dormant node is captured.");
            Assert(save.Dormant[0].ResourceId == B && save.Dormant[1].ResourceId == A && save.Dormant[2].ResourceId == C,
                "The regrowth queue keeps its order: ready tick first, then id.");

            TestLog.Log("[PASS] Test 5: the supply service captures its regrowth queue in order.");
        }

        private void ValidateCrawlerRecordCopiesEverySavedField()
        {
            var state = new AgentState(A)
            {
                Position = new Vec2(3.5f, -2.25f),
                Needs = new AgentNeeds(61f, 42f, 33f),
                Frustration = 12f,
                Membership = new GroupMembership(3, RoleRank.Follower, 55, 400u),
                Identity = new IdentityTag(1, 2, 3, 4),
                Personality = new AgentPersonality(0.5f, -0.25f, 0.1f, 0f, -0.75f),
                NavigationalStalls = 2,
                TantrumTimer = 1.5f,
                YieldingTimer = 0.5f,
                GossipTimer = 0.25f,
                SharingTimer = 0.75f,
                RallyTimer = 1.25f,
                DefiantTimer = 0.125f,
                LastEvaluatedDirectiveTick = 77u,
                ScoutReportBountyId = 2,
                ScoutReportTick = 88u,
                IsExhaustionCollapsed = true,
                CanBuyFoodAtStall = true
            };
            state.Activity.RecordSalvageHarvest(5u);
            state.Activity.RecordMeal(9u);
            state.TriggerGossipVisual(11f);

            var r = state.CaptureRecord();
            Assert(r.Id == A && r.Position == state.Position, "Id and position are captured.");
            Assert(r.Needs.Hunger == 61f && r.Needs.Energy == 42f && r.Needs.Social == 33f && r.Frustration == 12f, "Needs and frustration are captured.");
            Assert(r.Activity.SalvageHarvests == 1 && r.Activity.FirstMealTick == 9, "The activity tally is captured.");
            Assert(r.Membership.GroupId == 3 && r.Identity.FaithId == 3 && SimMath.Approximately(r.Personality.Greed, 0.5f), "Membership, identity and personality are captured.");
            Assert(r.NavigationalStalls == 2 && r.TantrumTimer == 1.5f && r.DefiantTimer == 0.125f && r.GossipTimer == 1.5f, "Stalls and emotion timers are captured.");
            Assert(r.LastGossipTimestamp == 11f && r.LastEvaluatedDirectiveTick == 77u, "Timestamps are captured.");
            Assert(r.ScoutReportBountyId == 2 && r.ScoutReportTick == 88u, "A scout's report is captured.");
            Assert(r.IsExhaustionCollapsed && r.CanBuyFoodAtStall, "Collapse and stall-food knowledge are captured.");

            TestLog.Log("[PASS] Test 6: a crawler's record copies every saved field.");
        }

        private void ValidateMapFingerprintTellsMapsApart()
        {
            ulong floor1 = SaveFingerprint.Of(Floor1Layout.Definition);
            ulong devMap = SaveFingerprint.Of(DevMapLayout.Definition);
            Assert(floor1 != 0UL && floor1 == SaveFingerprint.Of(Floor1Layout.Definition), "A map's fingerprint is stable.");
            Assert(floor1 != devMap, "Floor 1 and the Dev Map have different fingerprints.");
            Assert(SaveFingerprint.Of(null) == 0UL, "No map has fingerprint 0.");

            TestLog.Log($"[PASS] Test 7: the map fingerprint is stable and tells Floor 1 ({floor1:X16}) from the Dev Map ({devMap:X16}).");
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
