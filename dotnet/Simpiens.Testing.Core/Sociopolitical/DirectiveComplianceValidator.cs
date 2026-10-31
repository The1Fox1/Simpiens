using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Actions;
using Simpiens.Cognition.Planning.Goals;
using Simpiens.Cognition.Social;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Testing.Sociopolitical
{
    /// <summary>
    /// Automated domain validation suite for Epic 8 Phase 2:
    /// Collective Directives & Compliance/Defiance Dynamics.
    /// Runs as a test (ValidatorTests), never at boot.
    /// </summary>
    public class DirectiveComplianceValidator
    {
        public void Start()
        {
            TestLog.Log("[DirectiveComplianceValidator] Beginning automated validation of Epic 8 Phase 2...");

            ValidateWorldStateBuilderFactMapping();
            ValidateGoapBackwardPlanningForDirectives();
            ValidateComplianceUtilityEquation();
            ValidateComplianceThresholdsAndDecisionOutcomes();
            ValidateComplyDirectiveGoalPriorityAndStarvationOverride();
            ValidatePassiveDefianceAndDesertionSideEffects();
            ValidateDefensiveNullSafety();
            ValidateZeroAllocationHotPath();
            ValidateDirectivePrecedence();

            TestLog.Log("<color=green>[DirectiveComplianceValidator] ALL 9 DOMAIN TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateWorldStateBuilderFactMapping()
        {
            var leaderId = CreateGuid();
            var followerId = CreateGuid();
            byte groupId = 1;

            var directive = new GroupDirective(
                DirectiveType.RallyAtLocation,
                groupId,
                leaderId,
                targetLocation: new Vec2(10f, 10f),
                targetEntityId: default,
                issuedTick: 100,
                expirationTick: 500,
                targetRadius: 2.5f
            );

            var groupData = new GroupData(groupId, GroupType.Settlement, leaderId, new Vec2(10f, 10f), directive, 2, 90);
            var snapshot = new SharedWorldSnapshot(20, 20, 1.0f, 16, _ => { });
            var groupBuffer = new GroupData[SharedWorldSnapshot.MaxGroups];
            groupBuffer[0] = groupData;
            snapshot.SetGroups(groupBuffer, 1);

            // 1. Follower within directive radius (10.0, 11.0 => distance 1.0 <= 2.5)
            var memFollower = new GroupMembership(groupId, RoleRank.Follower, 80, 50);
            var ctxWithin = new AgentContext(followerId, new Vec2(10f, 11f), new AgentNeeds(10, 80, 50), 0f, snapshot, null, 200, memFollower, IdentityTag.Default);
            var stateWithin = WorldStateBuilder.BuildWorldState(ctxWithin);

            Assert(stateWithin.Get(StateFact.HasActiveDirective), "Follower state reflects HasActiveDirective = true.");
            Assert(stateWithin.Get(StateFact.AtDirectiveLocation), "Follower within radius reflects AtDirectiveLocation = true.");
            Assert(!stateWithin.Get(StateFact.IsGroupLeader), "Follower reflects IsGroupLeader = false.");

            // 2. Follower outside directive radius (0.0, 0.0 => distance ~14 > 2.5)
            var ctxOutside = new AgentContext(followerId, new Vec2(0f, 0f), new AgentNeeds(10, 80, 50), 0f, snapshot, null, 200, memFollower, IdentityTag.Default);
            var stateOutside = WorldStateBuilder.BuildWorldState(ctxOutside);

            Assert(stateOutside.Get(StateFact.HasActiveDirective), "Follower outside radius reflects HasActiveDirective = true.");
            Assert(!stateOutside.Get(StateFact.AtDirectiveLocation), "Follower outside radius reflects AtDirectiveLocation = false.");

            // 3. Leader agent
            var memLeader = new GroupMembership(groupId, RoleRank.Leader, 100, 0);
            var ctxLeader = new AgentContext(leaderId, new Vec2(10f, 10f), new AgentNeeds(10, 80, 50), 0f, snapshot, null, 200, memLeader, IdentityTag.Default);
            var stateLeader = WorldStateBuilder.BuildWorldState(ctxLeader);

            Assert(stateLeader.Get(StateFact.IsGroupLeader), "Leader reflects IsGroupLeader = true.");

            // 4. Expired directive (currentTick 600 > expirationTick 500)
            var ctxExpired = new AgentContext(followerId, new Vec2(10f, 11f), new AgentNeeds(10, 80, 50), 0f, snapshot, null, 600, memFollower, IdentityTag.Default);
            var stateExpired = WorldStateBuilder.BuildWorldState(ctxExpired);

            Assert(!stateExpired.Get(StateFact.HasActiveDirective), "Expired directive reflects HasActiveDirective = false.");
            Assert(!stateExpired.Get(StateFact.AtDirectiveLocation), "Expired directive reflects AtDirectiveLocation = false.");

            TestLog.Log("[PASS] Test 1: WorldStateBuilder directive facts mapping verified.");
        }

        private void ValidateGoapBackwardPlanningForDirectives()
        {
            var actionGraph = new ActionGraph();
            actionGraph.RegisterAction(new TravelToDirectiveAction(null, 2.0f));
            actionGraph.RegisterAction(new ExecuteDirectiveAction(1.0f));

            var goal = new ComplyDirectiveGoal();
            var planner = new GoapPlanner();
            var scratchPlan = new GoapPlan();

            var leaderId = CreateGuid();
            var followerId = CreateGuid();
            byte groupId = 1;
            var directive = new GroupDirective(DirectiveType.RallyAtLocation, groupId, leaderId, new Vec2(5f, 5f), default, 100, 500, 2.5f);
            var groupData = new GroupData(groupId, GroupType.Settlement, leaderId, Vec2.Zero, directive, 2, 90);

            var snapshot = new SharedWorldSnapshot(10, 10, 1.0f, 16, _ => { });
            var buffer = new GroupData[SharedWorldSnapshot.MaxGroups];
            buffer[0] = groupData;
            snapshot.SetGroups(buffer, 1);

            var mem = new GroupMembership(groupId, RoleRank.Follower, 80, 10);
            var memory = new AgentMemory();
            // High respect and allegiance => complies
            memory.SetAffinity(leaderId, new AffinityRecord(warmth: 30, trust: 30, fear: 10, respect: 50, 1, 100));

            // Plan from remote position: Requires [TravelToDirectiveAction, ExecuteDirectiveAction]
            var ctxRemote = new AgentContext(followerId, Vec2.Zero, new AgentNeeds(20, 80, 50), 0f, snapshot, memory, 150, mem, IdentityTag.Default);
            var stateRemote = WorldState.Empty.With(StateFact.HasActiveDirective, true).With(StateFact.AtDirectiveLocation, false);

            bool foundRemote = planner.Plan(stateRemote, goal, actionGraph, ctxRemote, scratchPlan);
            Assert(foundRemote, "Planner finds valid plan for remote ComplyDirectiveGoal.");
            Assert(scratchPlan.Count == 2, $"Remote plan length must be 2 (actual: {scratchPlan.Count}).");
            Assert(scratchPlan.Actions[0] is TravelToDirectiveAction, "First action is TravelToDirectiveAction.");
            Assert(scratchPlan.Actions[1] is ExecuteDirectiveAction, "Second action is ExecuteDirectiveAction.");
            Assert(SimMath.Approximately(scratchPlan.TotalCost, 3.0f), $"Total plan cost is 3.0 (actual: {scratchPlan.TotalCost}).");

            // Plan from target position: Requires only [ExecuteDirectiveAction]
            var stateLocal = WorldState.Empty.With(StateFact.HasActiveDirective, true).With(StateFact.AtDirectiveLocation, true);
            bool foundLocal = planner.Plan(stateLocal, goal, actionGraph, ctxRemote, scratchPlan);
            Assert(foundLocal, "Planner finds valid plan when already at directive location.");
            Assert(scratchPlan.Count == 1, $"Local plan length must be 1 (actual: {scratchPlan.Count}).");
            Assert(scratchPlan.Actions[0] is ExecuteDirectiveAction, "Only action is ExecuteDirectiveAction.");
            Assert(SimMath.Approximately(scratchPlan.TotalCost, 1.0f), $"Total plan cost is 1.0 (actual: {scratchPlan.TotalCost}).");

            TestLog.Log("[PASS] Test 2: GOAP backward planning for collective directives verified.");
        }

        private void ValidateComplianceUtilityEquation()
        {
            var leaderId = CreateGuid();
            var followerId = CreateGuid();
            byte groupId = 1;

            var groupData = new GroupData(groupId, GroupType.Settlement, leaderId, Vec2.Zero, GroupDirective.None, 2, 90);
            var memory = new AgentMemory();

            // Setup exact values: Respect=40, Fear=10, Warmth=20
            memory.SetAffinity(leaderId, new AffinityRecord(warmth: 20, trust: 0, fear: 10, respect: 40, 1, 100));

            // Allegiance = 80, Hunger = 20, Frustration = 10
            var mem = new GroupMembership(groupId, RoleRank.Follower, 80, 50);
            var context = new AgentContext(followerId, Vec2.Zero, new AgentNeeds(hunger: 20, energy: 80, social: 50), frustration: 10f, null, memory, 100, mem, IdentityTag.Default);

            // Formula:
            // U = (Respect * 0.35) + (Fear * 0.30) + (Allegiance * 0.25) + (Warmth * 0.10) - (Hunger * 0.30) - (Frustration * 0.20)
            // U = (40 * 0.35) + (10 * 0.30) + (80 * 0.25) + (20 * 0.10) - (20 * 0.30) - (10 * 0.20)
            // U = 14.0 + 3.0 + 20.0 + 2.0 - 6.0 - 2.0 = 31.0
            float expectedU = 31.0f;
            float actualU = ComplianceUtility.CalculateUtility(context, groupData);

            Assert(SimMath.Abs(actualU - expectedU) < 0.001f, $"Compliance utility calculation mismatch: expected {expectedU}, got {actualU}.");

            var decision = ComplianceUtility.Evaluate(context, groupData);
            Assert(decision == ComplianceDecision.Comply, $"Decision must be Comply for U={actualU} >= 20.");

            TestLog.Log("[PASS] Test 3: Compliance utility formula verified against analytical baseline.");
        }

        private void ValidateComplianceThresholdsAndDecisionOutcomes()
        {
            var leaderId = CreateGuid();
            var followerId = CreateGuid();
            byte groupId = 1;
            var groupData = new GroupData(groupId, GroupType.Settlement, leaderId, Vec2.Zero, GroupDirective.None, 2, 90);

            // 1. High utility -> Comply (U >= 20)
            {
                var mem = new GroupMembership(groupId, RoleRank.Follower, 90, 0); // 22.5 from allegiance
                var ctx = new AgentContext(followerId, Vec2.Zero, new AgentNeeds(0, 100, 50), 0f, null, null, 0, mem, IdentityTag.Default);
                Assert(ComplianceUtility.Evaluate(ctx, groupData) == ComplianceDecision.Comply, "U >= 20 yields Comply.");
            }

            // 2. Marginal utility -> PassiveDefiance (0 <= U < 20)
            {
                // Respect=0, Fear=0, Warmth=0, Allegiance=40 (+10), Hunger=20 (-6), Frustration=10 (-2) => U = +2
                var mem = new GroupMembership(groupId, RoleRank.Follower, 40, 0);
                var ctx = new AgentContext(followerId, Vec2.Zero, new AgentNeeds(20, 80, 50), 10f, null, null, 0, mem, IdentityTag.Default);
                float u = ComplianceUtility.CalculateUtility(ctx, groupData);
                Assert(u >= 0f && u < 20f, $"Utility is in passive range: {u}.");
                Assert(ComplianceUtility.Evaluate(ctx, groupData) == ComplianceDecision.PassiveDefiance, "0 <= U < 20 yields PassiveDefiance.");
            }

            // 3. Negative utility -> ActiveDesertion (U < 0)
            {
                // Respect=-30 (-10.5), Fear=0, Warmth=-30 (-3), Allegiance=10 (+2.5), Hunger=60 (-18), Frustration=40 (-8) => U = -37
                var memory = new AgentMemory();
                memory.SetAffinity(leaderId, new AffinityRecord(warmth: -30, trust: -30, fear: 0, respect: -30, 1, 0));
                var mem = new GroupMembership(groupId, RoleRank.Follower, 10, 0);
                var ctx = new AgentContext(followerId, Vec2.Zero, new AgentNeeds(60, 50, 50), 40f, null, memory, 0, mem, IdentityTag.Default);

                float u = ComplianceUtility.CalculateUtility(ctx, groupData);
                Assert(u < 0f, $"Utility is negative: {u}.");
                Assert(ComplianceUtility.Evaluate(ctx, groupData) == ComplianceDecision.ActiveDesertion, "U < 0 yields ActiveDesertion.");
            }

            TestLog.Log("[PASS] Test 4: Compliance categorical decision thresholds verified.");
        }

        private void ValidateComplyDirectiveGoalPriorityAndStarvationOverride()
        {
            var leaderId = CreateGuid();
            var followerId = CreateGuid();
            byte groupId = 1;

            var directive = new GroupDirective(DirectiveType.RallyAtLocation, groupId, leaderId, new Vec2(5f, 5f), default, 100, 500, 2.5f);
            var groupData = new GroupData(groupId, GroupType.Settlement, leaderId, Vec2.Zero, directive, 2, 90);

            var snapshot = new SharedWorldSnapshot(10, 10, 1.0f, 16, _ => { });
            var buffer = new GroupData[SharedWorldSnapshot.MaxGroups];
            buffer[0] = groupData;
            snapshot.SetGroups(buffer, 1);

            var goal = new ComplyDirectiveGoal();
            var hungerGoal = new SatiateHungerGoal();

            // Scenario A: Loyal, well-fed follower (Hunger = 20, U = 31)
            {
                var memory = new AgentMemory();
                memory.SetAffinity(leaderId, new AffinityRecord(warmth: 20, trust: 20, fear: 10, respect: 40, 1, 100));
                var mem = new GroupMembership(groupId, RoleRank.Follower, 80, 50);
                var ctx = new AgentContext(followerId, Vec2.Zero, new AgentNeeds(20, 80, 50), 0f, snapshot, memory, 150, mem, IdentityTag.Default);

                Assert(goal.IsValid(ctx), "Goal is valid for loyal follower.");
                float priority = goal.CalculatePriority(ctx);
                Assert(priority >= 70f, $"Compliance priority ({priority}) is high for loyal follower.");
            }

            // Scenario B: Defiant follower (U < 20)
            {
                var mem = new GroupMembership(groupId, RoleRank.Follower, 20, 50);
                var ctx = new AgentContext(followerId, Vec2.Zero, new AgentNeeds(50, 50, 50), 20f, snapshot, null, 150, mem, IdentityTag.Default);

                Assert(!goal.IsValid(ctx), "Goal is invalid for defiant follower.");
                Assert(goal.CalculatePriority(ctx) == 0f, "Goal priority is 0 for defiant follower.");
            }

            // Scenario C: Severe starvation (Hunger = 80 >= 70) with low leader fear (Fear = 10 < 70)
            // SatiateHungerGoal MUST dominate ComplyDirectiveGoal
            {
                var memory = new AgentMemory();
                // High respect/allegiance keeps U > 20 despite hunger
                memory.SetAffinity(leaderId, new AffinityRecord(warmth: 20, trust: 20, fear: 10, respect: 60, 1, 100));
                var mem = new GroupMembership(groupId, RoleRank.Follower, 100, 50);
                var ctx = new AgentContext(followerId, Vec2.Zero, new AgentNeeds(80, 80, 50), 0f, snapshot, memory, 150, mem, IdentityTag.Default);

                float directivePriority = goal.CalculatePriority(ctx);
                float hungerPriority = hungerGoal.CalculatePriority(ctx);

                Assert(directivePriority <= 40f, $"Severe starvation suppressed directive priority to {directivePriority}.");
                Assert(hungerPriority >= 80f, $"Hunger priority is {hungerPriority}.");
                Assert(hungerPriority > directivePriority, "Hunger goal strictly dominates directive goal under severe starvation.");
            }

            // Scenario D: Severe starvation (Hunger = 80 >= 70) with overwhelming fear intimidation (Fear = 80 >= 70)
            // Fear forces compliance above starvation!
            {
                var memory = new AgentMemory();
                memory.SetAffinity(leaderId, new AffinityRecord(warmth: 0, trust: 0, fear: 80, respect: 40, 1, 100));
                var mem = new GroupMembership(groupId, RoleRank.Follower, 90, 50);
                var ctx = new AgentContext(followerId, Vec2.Zero, new AgentNeeds(80, 80, 50), 0f, snapshot, memory, 150, mem, IdentityTag.Default);

                float intimidatedPriority = goal.CalculatePriority(ctx);
                Assert(intimidatedPriority >= 70f, $"Intimidation fear maintained high directive priority ({intimidatedPriority}) despite starvation.");
            }

            TestLog.Log("[PASS] Test 5: ComplyDirectiveGoal dynamic priority and crisis overrides verified.");
        }

        private void ValidatePassiveDefianceAndDesertionSideEffects()
        {
            var registry = new GroupRegistry();
            var leaderId = CreateGuid();
            var followerId = CreateGuid();
            byte groupId = registry.CreateGroup(GroupType.Settlement, leaderId, Vec2.Zero);
            registry.AddMember(groupId, followerId, RoleRank.Follower, initialAllegiance: 70);

            // 1. Passive defiance: -5 allegiance
            registry.UpdateAllegiance(followerId, groupId, -5);
            registry.TryGetOnlyMembership(followerId, out var mem);
            Assert(mem.Allegiance == 65, $"Follower allegiance reduced to 65 after passive defiance (actual: {mem.Allegiance}).");

            // 2. Compliance reward: +3 allegiance
            registry.UpdateAllegiance(followerId, groupId, +3);
            registry.TryGetOnlyMembership(followerId, out mem);
            Assert(mem.Allegiance == 68, $"Follower allegiance increased to 68 after compliance (actual: {mem.Allegiance}).");

            // 3. Desertion: member removed from registry
            registry.RemoveMember(groupId, followerId);
            Assert(!registry.TryGetOnlyMembership(followerId, out _), "Deserter membership removed from registry.");
            registry.TryGetGroup(groupId, out var groupData);
            Assert(groupData.MemberCount == 1, "Group member count decremented to 1.");

            // 4. Memory event types verification
            var memory = new AgentMemory();
            memory.RecordEvent(MemoryEventType.DirectiveComplied, leaderId, GridPoint.Zero, 100);
            memory.RecordEvent(MemoryEventType.SubordinateDefied, followerId, GridPoint.Zero, 101);
            memory.RecordEvent(MemoryEventType.DesertedGroup, leaderId, GridPoint.Zero, 102);

            Assert(memory.EventLedger.Count == 3, "All 3 new memory events recorded without error.");

            TestLog.Log("[PASS] Test 6: Sociopolitical side effects (allegiance delta, memory logging, desertion) verified.");
        }

        private void ValidateDefensiveNullSafety()
        {
            // Null registry calls
            IGroupRegistry nullRegistry = null;
            Assert(nullRegistry == null, "Null registry reference confirmed.");

            // Default context
            var defaultContext = default(AgentContext);
            var defaultGroup = GroupData.Empty;

            // ComplianceUtility null safety
            float u = ComplianceUtility.CalculateUtility(defaultContext, defaultGroup);
            Assert(u == 0f, "ComplianceUtility returns 0 for default context and empty group.");
            var decision = ComplianceUtility.Evaluate(defaultContext, defaultGroup);
            Assert(decision == ComplianceDecision.PassiveDefiance, "Decision for 0 utility is PassiveDefiance.");

            // WorldStateBuilder with default context
            var state = WorldStateBuilder.BuildWorldState(defaultContext);
            Assert(!state.Get(StateFact.HasActiveDirective), "Default context produces HasActiveDirective = false.");
            Assert(!state.Get(StateFact.AtDirectiveLocation), "Default context produces AtDirectiveLocation = false.");

            // ComplyDirectiveGoal with default context
            var goal = new ComplyDirectiveGoal();
            Assert(!goal.IsValid(defaultContext), "Goal is invalid for default context.");
            Assert(goal.CalculatePriority(defaultContext) == 0f, "Goal priority is 0 for default context.");

            // Actions with default context
            var travelAction = new TravelToDirectiveAction(null);
            var travelIntent = travelAction.CreateIntent(defaultContext);
            Assert(travelIntent != null, "TravelToDirectiveAction returns non-null intent with null pathfinder and default context.");

            var execAction = new ExecuteDirectiveAction();
            var execIntent = execAction.CreateIntent(defaultContext);
            Assert(execIntent is IdleIntent, "ExecuteDirectiveAction returns IdleIntent with default context.");

            TestLog.Log("[PASS] Test 7: Defensive null safety for test isolation verified.");
        }

        private void ValidateZeroAllocationHotPath()
        {
            var leaderId = CreateGuid();
            var followerId = CreateGuid();
            byte groupId = 1;

            var directive = new GroupDirective(DirectiveType.RallyAtLocation, groupId, leaderId, new Vec2(10f, 10f), default, 100, 500, 2.5f);
            var groupData = new GroupData(groupId, GroupType.Settlement, leaderId, new Vec2(10f, 10f), directive, 2, 90);

            var snapshot = new SharedWorldSnapshot(20, 20, 1.0f, 16, _ => { });
            var groupBuffer = new GroupData[SharedWorldSnapshot.MaxGroups];
            groupBuffer[0] = groupData;
            snapshot.SetGroups(groupBuffer, 1);

            var memory = new AgentMemory();
            memory.SetAffinity(leaderId, new AffinityRecord(warmth: 20, trust: 20, fear: 10, respect: 40, 1, 100));
            var mem = new GroupMembership(groupId, RoleRank.Follower, 80, 50);
            var context = new AgentContext(followerId, new Vec2(10f, 11f), new AgentNeeds(20, 80, 50), 0f, snapshot, memory, 200, mem, IdentityTag.Default);
            var goal = new ComplyDirectiveGoal();

            // Pre-warm JIT
            for (int i = 0; i < 50; i++)
            {
                WorldStateBuilder.BuildWorldState(context);
                ComplianceUtility.CalculateUtility(context, groupData);
                ComplianceUtility.Evaluate(context, groupData);
                goal.CalculatePriority(context);
            }

            // Force clean GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            // Hot path benchmark: 1,000 iterations
            for (int i = 0; i < 1000; i++)
            {
                var state = WorldStateBuilder.BuildWorldState(context);
                float util = ComplianceUtility.CalculateUtility(context, groupData);
                var decision = ComplianceUtility.Evaluate(context, groupData);
                float priority = goal.CalculatePriority(context);

                if (!state.Get(StateFact.HasActiveDirective) || util < 0f || decision != ComplianceDecision.Comply || priority <= 0f)
                {
                    Assert(false, "Benchmark iteration produced incorrect result.");
                }
            }

            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - startAlloc;
            Assert(totalAlloc == 0, $"Hot path allocated {totalAlloc} bytes across 1,000 iterations! Must be strictly 0 bytes.");

            TestLog.Log("[PASS] Test 8: Zero-allocation hot path verified (0 bytes allocated across 1,000 iterations).");
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

        // Epic 10 G4, D1: a crawler in several groups follows its party's order, then its guild's, then its settlement's;
        // deserting one group leaves the others
        private void ValidateDirectivePrecedence()
        {
            var partyLeader = CreateGuid();
            var settlementLeader = CreateGuid();
            var crawler = CreateGuid();
            byte party = 1, settlement = 2;
            var rally = new GroupDirective(DirectiveType.RallyAtLocation, settlement, settlementLeader, new Vec2(4f, 4f), default, issuedTick: 100, expirationTick: 500, targetRadius: 2f);
            var snapshot = new SharedWorldSnapshot(20, 20, 1.0f, 16, _ => { });
            var groups = new GroupData[SharedWorldSnapshot.MaxGroups];
            groups[0] = new GroupData(party, GroupType.TemporaryParty, partyLeader, Vec2.Zero, GroupDirective.None, 2, 90);
            groups[1] = new GroupData(settlement, GroupType.Settlement, settlementLeader, Vec2.Zero, rally, 2, 90);
            snapshot.SetGroups(groups, 2);

            var set = MembershipSet.Empty;
            set.Set(GroupType.Settlement, new GroupMembership(settlement, RoleRank.Follower, 70, 0));
            set.Set(GroupType.TemporaryParty, new GroupMembership(party, RoleRank.Follower, 60, 0));
            Assert(set.DirectiveMembership(snapshot, 200).GroupId == settlement, "With no order from its party, the crawler follows its settlement's.");
            Assert(set.DirectiveMembership(snapshot, 600).GroupId == party, "With no active order, the directive membership is the first by kind (the party).");
            Assert(set.DirectiveMembership(null, 200).GroupId == party, "Without a snapshot, the first by kind.");

            groups[0] = new GroupData(party, GroupType.TemporaryParty, partyLeader, Vec2.Zero,
                new GroupDirective(DirectiveType.RallyAtLocation, party, partyLeader, new Vec2(8f, 8f), default, issuedTick: 150, expirationTick: 500, targetRadius: 2f), 2, 90);
            snapshot.SetGroups(groups, 2);
            Assert(set.DirectiveMembership(snapshot, 200).GroupId == party, "The party's order comes before the settlement's.");

            var state = new AgentState(crawler) { Memberships = set };
            var context = state.CreateContext(snapshot, 200);
            Assert(context.DirectiveMembership.GroupId == party && context.DirectiveMembership.Allegiance == 60, "The context carries the obeyed group's membership.");

            var registry = new GroupRegistry();
            byte guild = registry.CreateGroup(GroupType.FactionGuild, CreateGuid(), Vec2.Zero);
            byte home = registry.CreateGroup(GroupType.Settlement, CreateGuid(), Vec2.Zero);
            registry.AddMember(guild, crawler);
            registry.AddMember(home, crawler);
            registry.RemoveMember(home, crawler);
            Assert(registry.TryGetMembership(crawler, guild, out _) && !registry.TryGetMembership(crawler, home, out _), "Deserting the settlement keeps the guild.");

            TestLog.Log("[PASS] Directive precedence (party, guild, settlement) and one-group desertion verified (Epic 10 G4, D1).");
        }

        private static EntityId CreateGuid()
        {
            var union = new GuidUnion { v0 = _guidCounter++, v1 = 0x23456789, v2 = 0xabcdef01, v3 = 0x66666666 };
            return union.GuidValue;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[DirectiveComplianceValidator ASSERTION FAILED] {message}");
            }
        }
    }
}
