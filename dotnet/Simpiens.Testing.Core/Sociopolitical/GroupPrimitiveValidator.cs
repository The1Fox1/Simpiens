using System;
using System.Linq;
using System.Runtime.InteropServices;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Memory;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Testing.Sociopolitical
{
    /// <summary>
    /// Automated domain validation suite for Epic 8 Phase 1:
    /// Identity Tags & The Generic Group Primitive.
    /// Runs as a test (ValidatorTests), never at boot.
    /// </summary>
    public class GroupPrimitiveValidator
    {
        public void Start()
        {
            TestLog.Log("[GroupPrimitiveValidator] Beginning automated validation of Epic 8 Phase 1...");

            ValidateUnmanagedStructSizesAndLayout();
            ValidateGroupLifecycleAndMembership();
            ValidateSharedWorldSnapshotLockFreeQuery();
            ValidateDirectiveLifecycleAndExpiration();
            ValidateAgentContextAndMemoryIntegration();
            ValidateDefensiveNullSafety();
            ValidateZeroAllocationHotPath();
            ValidateManyToManyOnePerKind();

            TestLog.Log("<color=green>[GroupPrimitiveValidator] ALL 8 DOMAIN TESTS PASSED SUCCESSFULLY!</color>");
        }

        // Epic 10 G4, D1: a crawler belongs to a settlement, a guild and a party at once; a second group of a kind replaces
        // the first of that kind only; disbanding or leaving one group leaves the others
        private void ValidateManyToManyOnePerKind()
        {
            var registry = new GroupRegistry();
            var crawler = CreateGuid();
            byte settlement = registry.CreateGroup(GroupType.Settlement, CreateGuid(), Vec2.Zero);
            byte guild = registry.CreateGroup(GroupType.FactionGuild, CreateGuid(), Vec2.Zero);
            byte party = registry.CreateGroup(GroupType.TemporaryParty, CreateGuid(), Vec2.Zero);

            Assert(registry.AddMember(settlement, crawler, RoleRank.Follower, 70), "Joins a settlement.");
            Assert(registry.AddMember(guild, crawler, RoleRank.Follower, 70), "Joins a guild too.");
            Assert(registry.AddMember(party, crawler, RoleRank.Officer, 60), "And a party.");
            var set = registry.GetMemberships(crawler);
            Assert(set.Count == 3, $"Three memberships at once ({set.Count}).");
            Assert(set.KindAt(0) == GroupType.TemporaryParty && set.KindAt(1) == GroupType.FactionGuild && set.KindAt(2) == GroupType.Settlement,
                "Memberships are ordered by kind: party, guild, settlement (the directive precedence).");
            Assert(registry.GetMembers(settlement).Contains(crawler) && registry.GetMembers(guild).Contains(crawler) && registry.GetMembers(party).Contains(crawler),
                "The crawler is on all three rosters.");
            Assert(registry.TryGetMembership(crawler, party, out var inParty) && inParty.Role == RoleRank.Officer && inParty.Allegiance == 60, "Each membership keeps its own role and allegiance.");
            Assert(registry.TryGetMembership(crawler, GroupType.FactionGuild, out var inGuild) && inGuild.GroupId == guild, "A membership is found by kind.");

            registry.UpdateAllegiance(crawler, guild, -20);
            Assert(registry.TryGetMembership(crawler, guild, out inGuild) && inGuild.Allegiance == 50, "Allegiance changes in one group only.");
            Assert(registry.TryGetMembership(crawler, settlement, out var inSettlement) && inSettlement.Allegiance == 70, "The other groups' allegiance is untouched.");

            byte second = registry.CreateGroup(GroupType.Settlement, CreateGuid(), Vec2.Zero);
            registry.AddMember(second, crawler, RoleRank.Follower, 80);
            Assert(!registry.GetMembers(settlement).Contains(crawler) && registry.GetMembers(second).Contains(crawler), "A second settlement replaces the first: one per kind.");
            Assert(registry.GetMemberships(crawler).Count == 3 && registry.TryGetMembership(crawler, guild, out _) && registry.TryGetMembership(crawler, party, out _),
                "Changing settlement keeps the guild and the party.");

            registry.DisbandGroup(guild);
            set = registry.GetMemberships(crawler);
            Assert(set.Count == 2 && !set.Has(GroupType.FactionGuild) && set.Has(GroupType.TemporaryParty) && set.Has(GroupType.Settlement), "Disbanding the guild leaves the others.");

            registry.RemoveMember(party, crawler);
            Assert(registry.GetMemberships(crawler).Count == 1 && registry.TryGetMembership(crawler, second, out _), "Leaving the party leaves the settlement.");

            byte guild2 = registry.CreateGroup(GroupType.FactionGuild, CreateGuid(), Vec2.Zero);
            registry.AddMember(guild2, crawler);
            registry.LeaveAllGroups(crawler);
            Assert(!registry.GetMemberships(crawler).HasAny && !registry.GetMembers(second).Contains(crawler) && !registry.GetMembers(guild2).Contains(crawler),
                "Leaving every group empties the memberships and the rosters.");

            TestLog.Log("[PASS] Test 8: Groups are many-to-many, one per kind (Epic 10 G4, D1).");
        }

        private void ValidateUnmanagedStructSizesAndLayout()
        {
            int directiveSize = Marshal.SizeOf<GroupDirective>();
            int membershipSize = Marshal.SizeOf<GroupMembership>();
            int identitySize = Marshal.SizeOf<IdentityTag>();
            int groupDataSize = Marshal.SizeOf<GroupData>();

            Assert(directiveSize <= 64, $"GroupDirective struct size ({directiveSize} bytes) exceeds budget of 64 bytes.");
            Assert(membershipSize <= 16, $"GroupMembership struct size ({membershipSize} bytes) exceeds budget of 16 bytes.");
            Assert(identitySize <= 8, $"IdentityTag struct size ({identitySize} bytes) exceeds budget of 8 bytes.");
            Assert(groupDataSize <= 96, $"GroupData struct size ({groupDataSize} bytes) exceeds budget of 96 bytes.");

            // Verify default instances
            Assert(!GroupMembership.None.HasGroup, "GroupMembership.None has HasGroup == false.");
            Assert(GroupDirective.None.Type == DirectiveType.None, "GroupDirective.None has Type == DirectiveType.None.");
            Assert(!GroupDirective.None.IsActive(0), "GroupDirective.None is not active at tick 0.");
            Assert(GroupData.Empty.GroupId == 0 && !GroupData.Empty.IsActive, "GroupData.Empty has GroupId 0 and IsActive == false.");

            TestLog.Log("[PASS] Test 1: Unmanaged struct sizes and default layouts verified.");
        }

        private void ValidateGroupLifecycleAndMembership()
        {
            var registry = new GroupRegistry();
            var leaderId = CreateGuid();
            var followerId = CreateGuid();
            var anchorPos = new Vec2(10f, 15f);

            // 1. Create group
            byte groupId = registry.CreateGroup(GroupType.Settlement, leaderId, anchorPos, initialCohesion: 90);
            Assert(groupId == 1, $"First group receives ID 1 (actual: {groupId}).");
            Assert(registry.ActiveGroupCount == 1, "ActiveGroupCount is 1.");

            // 2. Verify leader auto-registered
            Assert(registry.TryGetGroup(groupId, out var groupData), "TryGetGroup succeeds for active group.");
            Assert(groupData.LeaderId == leaderId, "Leader ID matches.");
            Assert(groupData.AnchorPosition == anchorPos, "Anchor position matches.");
            Assert(groupData.MemberCount == 1, "Initial member count is 1.");
            Assert(registry.TryGetOnlyMembership(leaderId, out var leaderMembership), "Leader membership registered.");
            Assert(leaderMembership.Role == RoleRank.Leader, "Leader has RoleRank.Leader.");
            Assert(leaderMembership.GroupId == groupId, "Leader GroupId matches.");

            // 3. Add follower
            Assert(registry.AddMember(groupId, followerId, RoleRank.Follower, initialAllegiance: 75), "AddMember succeeds.");
            Assert(registry.TryGetGroup(groupId, out groupData), "TryGetGroup succeeds.");
            Assert(groupData.MemberCount == 2, "Member count is now 2.");
            Assert(registry.TryGetOnlyMembership(followerId, out var followerMembership), "Follower membership found.");
            Assert(followerMembership.Role == RoleRank.Follower, "Follower has RoleRank.Follower.");
            Assert(followerMembership.Allegiance == 75, "Follower allegiance is 75.");

            // 4. Promote follower to Officer
            Assert(registry.SetRole(groupId, followerId, RoleRank.Officer), "SetRole to Officer succeeds.");
            registry.TryGetOnlyMembership(followerId, out followerMembership);
            Assert(followerMembership.Role == RoleRank.Officer, "Follower promoted to RoleRank.Officer.");

            // 5. Remove follower
            Assert(registry.RemoveMember(groupId, followerId), "RemoveMember succeeds.");
            Assert(!registry.TryGetOnlyMembership(followerId, out _), "Follower membership removed.");
            registry.TryGetGroup(groupId, out groupData);
            Assert(groupData.MemberCount == 1, "Member count back to 1.");

            // 6. Disband group
            Assert(registry.DisbandGroup(groupId), "DisbandGroup succeeds.");
            Assert(registry.ActiveGroupCount == 0, "ActiveGroupCount is 0 after disband.");
            Assert(!registry.TryGetGroup(groupId, out _), "Disbanded group not found.");
            Assert(!registry.TryGetOnlyMembership(leaderId, out _), "Leader membership unlinked after disband.");

            TestLog.Log("[PASS] Test 2: Group creation, roster management, role promotion, and disbanding verified.");
        }

        private void ValidateSharedWorldSnapshotLockFreeQuery()
        {
            var registry = new GroupRegistry();
            var leaderA = CreateGuid();
            var leaderB = CreateGuid();

            byte groupA = registry.CreateGroup(GroupType.TemporaryParty, leaderA, new Vec2(5f, 5f));
            byte groupB = registry.CreateGroup(GroupType.FactionGuild, leaderB, new Vec2(-10f, 20f));

            var snapshot = new SharedWorldSnapshot(20, 20, 1.0f, 16, _ => { });
            var scratch = new GroupData[SharedWorldSnapshot.MaxGroups];

            registry.CopySnapshotData(scratch, out int activeCount);
            Assert(activeCount == 2, $"Copied 2 active groups to scratch buffer (actual: {activeCount}).");

            snapshot.SetGroups(scratch, activeCount);
            Assert(snapshot.ActiveGroupCount == 2, "Snapshot ActiveGroupCount is 2.");

            // Lock-free lookups
            Assert(snapshot.TryGetGroup(groupA, out var snapA), "TryGetGroup(groupA) succeeds.");
            Assert(snapA.Type == GroupType.TemporaryParty, "groupA type matches TemporaryParty.");
            Assert(snapA.LeaderId == leaderA, "groupA leader matches.");
            Assert(snapA.AnchorPosition == new Vec2(5f, 5f), "groupA anchor matches.");

            Assert(snapshot.TryGetGroup(groupB, out var snapB), "TryGetGroup(groupB) succeeds.");
            Assert(snapB.Type == GroupType.FactionGuild, "groupB type matches FactionGuild.");

            // Invalid queries
            Assert(!snapshot.TryGetGroup(0, out _), "TryGetGroup(0) returns false.");
            Assert(!snapshot.TryGetGroup(99, out _), "TryGetGroup(99) returns false.");

            // Clear snapshot
            snapshot.Clear();
            Assert(snapshot.ActiveGroupCount == 0, "Snapshot ActiveGroupCount is 0 after Clear.");
            Assert(!snapshot.TryGetGroup(groupA, out _), "TryGetGroup returns false after Clear.");

            TestLog.Log("[PASS] Test 3: SharedWorldSnapshot lock-free buffer synchronization and query verified.");
        }

        private void ValidateDirectiveLifecycleAndExpiration()
        {
            var registry = new GroupRegistry();
            var leaderId = CreateGuid();
            byte groupId = registry.CreateGroup(GroupType.TemporaryParty, leaderId, Vec2.Zero);

            var directive = new GroupDirective(
                DirectiveType.RallyAtLocation,
                groupId,
                leaderId,
                new Vec2(15f, 25f),
                CreateGuid(),
                issuedTick: 10,
                expirationTick: 100,
                targetRadius: 3.0f
            );

            Assert(directive.IsActive(50), "Directive is active at tick 50.");
            Assert(!directive.IsActive(101), "Directive is inactive at tick 101.");

            // Set directive
            Assert(registry.SetDirective(groupId, directive), "SetDirective succeeds.");
            Assert(registry.TryGetGroup(groupId, out var data), "TryGetGroup succeeds.");
            Assert(data.ActiveDirective.Type == DirectiveType.RallyAtLocation, "Active directive is RallyAtLocation.");
            Assert(data.ActiveDirective.TargetLocation == new Vec2(15f, 25f), "Target location matches.");

            // Tick before expiration
            registry.Tick(50);
            registry.TryGetGroup(groupId, out data);
            Assert(data.ActiveDirective.Type == DirectiveType.RallyAtLocation, "Directive persists at tick 50.");

            // Tick past expiration
            registry.Tick(105);
            registry.TryGetGroup(groupId, out data);
            Assert(data.ActiveDirective.Type == DirectiveType.None, "Directive automatically expired after tick 100.");

            TestLog.Log("[PASS] Test 4: Directive issuance, spatial parameters, and automatic expiration verified.");
        }

        private void ValidateAgentContextAndMemoryIntegration()
        {
            var memory = new AgentMemory();
            var agentId = CreateGuid();
            var membership = new GroupMembership(1, RoleRank.Officer, 85, joinTick: 50);
            var identity = new IdentityTag(cultureId: 3, kinshipId: 7, faithId: 2, casteRank: 1);

            // Context with group standing
            var context = new AgentContext(
                agentId,
                new Vec2(2f, 3f),
                new AgentNeeds(20, 80, 50),
                frustration: 0f,
                snapshot: null,
                memory: memory,
                currentTick: 100,
                directiveMembership: membership,
                identity: identity
            );

            Assert(context.DirectiveMembership.HasGroup, "Context reflects active group membership.");
            Assert(context.DirectiveMembership.GroupId == 1, "Context membership GroupId is 1.");
            Assert(context.DirectiveMembership.Role == RoleRank.Officer, "Context role is Officer.");
            Assert(context.DirectiveMembership.Allegiance == 85, "Context allegiance is 85.");
            Assert(context.Identity.CultureId == 3, "Context CultureId is 3.");
            Assert(context.Identity.KinshipId == 7, "Context KinshipId is 7.");

            // Backwards-compatible context
            var legacyContext = new AgentContext(
                agentId,
                Vec2.Zero,
                hunger: 30f,
                energy: 70f,
                frustration: 0f,
                snapshot: null,
                memory: memory,
                currentTick: 100
            );

            Assert(!legacyContext.DirectiveMembership.HasGroup, "Legacy context defaults to HasGroup == false.");
            Assert(legacyContext.Identity.CultureId == 0, "Legacy context defaults to CultureId == 0.");

            // Record group memory events
            var joinEvent = new MemoryEvent(MemoryEventType.JoinedGroup, agentId, new GridPoint(2, 3), 100);
            var dirEvent = new MemoryEvent(MemoryEventType.DirectiveReceived, agentId, new GridPoint(2, 3), 105);
            memory.EventLedger.AddEvent(joinEvent);
            memory.EventLedger.AddEvent(dirEvent);

            Assert(memory.EventLedger.Count == 2, "MemoryLedger recorded 2 group events.");
            Assert(memory.EventLedger.GetEvent(0).EventType == MemoryEventType.JoinedGroup, "First event is JoinedGroup.");
            Assert(memory.EventLedger.GetEvent(1).EventType == MemoryEventType.DirectiveReceived, "Second event is DirectiveReceived.");

            TestLog.Log("[PASS] Test 5: AgentContext and MemoryEvent sociopolitical integration verified.");
        }

        private void ValidateDefensiveNullSafety()
        {
            // 1. SimulationManager with all null parameters
            var simManager = new SimulationManager(null, null, null, null);
            simManager.Tick();
            simManager.ResetState();
            Assert(!simManager.IsPaused, "SimulationManager ticks benignly with all null interfaces.");

            // 2. GroupRegistry boundary safety
            var registry = new GroupRegistry();
            Assert(!registry.TryGetGroup(0, out _), "TryGetGroup(0) handled safely.");
            Assert(!registry.TryGetGroup(255, out _), "TryGetGroup(255) handled safely.");
            Assert(!registry.AddMember(0, CreateGuid()), "AddMember with GroupId 0 returns false.");
            Assert(!registry.AddMember(1, default), "AddMember with default EntityId returns false.");
            Assert(!registry.RemoveMember(0, CreateGuid()), "RemoveMember with GroupId 0 returns false.");
            Assert(!registry.SetRole(0, CreateGuid(), RoleRank.Leader), "SetRole with GroupId 0 returns false.");
            Assert(!registry.TryGetOnlyMembership(default, out _), "TryGetMembership with default EntityId returns false.");
            Assert(registry.GetMembers(0).Count == 0, "GetMembers with GroupId 0 returns empty collection.");
            Assert(!registry.SetDirective(0, GroupDirective.None), "SetDirective with GroupId 0 returns false.");
            Assert(!registry.ClearDirective(0), "ClearDirective with GroupId 0 returns false.");
            Assert(!registry.DisbandGroup(0), "DisbandGroup(0) returns false.");

            // 3. CopySnapshotData null destination
            registry.CopySnapshotData(null, out int count);
            Assert(count == 0, "CopySnapshotData with null destination safely yields 0 count.");

            TestLog.Log("[PASS] Test 6: Defensive null-safety and boundary conditions verified.");
        }

        private void ValidateZeroAllocationHotPath()
        {
            var registry = new GroupRegistry();
            var leader = CreateGuid();
            byte groupId = registry.CreateGroup(GroupType.Settlement, leader, new Vec2(10f, 10f));

            var snapshot = new SharedWorldSnapshot(10, 10, 1.0f, 16, _ => { });
            var scratch = new GroupData[SharedWorldSnapshot.MaxGroups];

            registry.CopySnapshotData(scratch, out int activeCount);
            snapshot.SetGroups(scratch, activeCount);

            // Pre-warm JIT
            for (int i = 0; i < 50; i++)
            {
                registry.CopySnapshotData(scratch, out _);
                snapshot.TryGetGroup(groupId, out _);
            }

            // Force clean GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            // Hot path benchmark: 1,000 snapshot updates and lookups
            for (int i = 0; i < 1000; i++)
            {
                registry.CopySnapshotData(scratch, out int count);
                snapshot.SetGroups(scratch, count);
                snapshot.TryGetGroup(groupId, out var data);
                if (data.GroupId == 0)
                {
                    Assert(false, "Failed to retrieve group in hot path.");
                }
            }

            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - startAlloc;
            Assert(totalAlloc == 0, $"Hot path allocated {totalAlloc} bytes across 1,000 iterations! Must be strictly 0 bytes.");

            TestLog.Log("[PASS] Test 7: Zero-allocation hot path verified (0 bytes allocated across 1,000 iterations).");
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct GuidUnion
        {
            [FieldOffset(0)] public uint v0;
            [FieldOffset(4)] public uint v1;
            [FieldOffset(8)] public uint v2;
            [FieldOffset(12)] public uint v3;
            [FieldOffset(0)] public EntityId GuidValue;
        }

        private static uint _guidCounter = 1;

        private static EntityId CreateGuid()
        {
            var union = new GuidUnion { v0 = _guidCounter++, v1 = 0x12345678, v2 = 0x9abcdef0, v3 = 0x55555555 };
            return union.GuidValue;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[GroupPrimitiveValidator ASSERTION FAILED] {message}");
            }
        }
    }
}
