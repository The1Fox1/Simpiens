using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Social;
using Simpiens.Simulation;
using UnityEngine;
using VContainer.Unity;

namespace Simpiens.Testing.Sociopolitical
{
    /// <summary>
    /// Automated domain validation suite for Epic 8 Phase 3:
    /// Organic Emergence, Leadership Succession & Schisms.
    /// Runs headlessly on startup via VContainer IStartable.
    /// Uses decoupled ISocialAgent for 100% pure headless C# test isolation without Unity GameObject dependencies.
    /// </summary>
    public class SuccessionValidator : IStartable
    {
        public class MockSocialAgent : ISocialAgent
        {
            public GUID AgentId { get; set; }
            public AgentMemory Memory { get; set; }
            public GroupMembership Membership { get; set; } = GroupMembership.None;
            public bool IsExhaustionCollapsed { get; set; }
            public bool IsStarvationPanicking { get; set; }

            public MockSocialAgent(GUID id)
            {
                AgentId = id;
                Memory = new AgentMemory();
            }
        }

        public void Start()
        {
            Debug.Log("[SuccessionValidator] Beginning automated validation of Epic 8 Phase 3...");

            ValidateOrganicHearthEmergence();
            ValidatePeacefulSuccession();
            ValidateDominanceChallengeSuccession();
            ValidateDominanceLoserDeparture();
            ValidateFactionalSchism();
            ValidateIncapacitationAndLegitimacyWatchdog();
            ValidateDefensiveNullSafety();
            ValidateZeroAllocationHotPath();

            Debug.Log("<color=green>[SuccessionValidator] ALL 8 DOMAIN TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateOrganicHearthEmergence()
        {
            var registry = new GroupRegistry();
            var agentA = CreateTestAgent();
            var agentB = CreateTestAgent();
            var agentC = CreateTestAgent();

            uint currentTick = 100;

            // Setup mutual warmth >= 40 and trust >= 30 between all 3 agents
            agentA.Memory.SetAffinity(agentB.AgentId, new AffinityRecord(warmth: 50, trust: 40, fear: 10, respect: 20, 1, currentTick));
            agentA.Memory.SetAffinity(agentC.AgentId, new AffinityRecord(warmth: 60, trust: 50, fear: 0, respect: 30, 1, currentTick));

            agentB.Memory.SetAffinity(agentA.AgentId, new AffinityRecord(warmth: 50, trust: 40, fear: 10, respect: 60, 1, currentTick));
            agentB.Memory.SetAffinity(agentC.AgentId, new AffinityRecord(warmth: 45, trust: 35, fear: 0, respect: 30, 1, currentTick));

            agentC.Memory.SetAffinity(agentA.AgentId, new AffinityRecord(warmth: 60, trust: 50, fear: 10, respect: 70, 1, currentTick));
            agentC.Memory.SetAffinity(agentB.AgentId, new AffinityRecord(warmth: 45, trust: 35, fear: 10, respect: 30, 1, currentTick));

            // Net standing (Respect - Fear) for each candidate:
            // For A: from B (60 - 10 = 50) + from C (70 - 10 = 60) => Net = 110 (CLEAR CONSENSUS LEADER!)
            // For B: from A (20 - 10 = 10) + from C (30 - 10 = 20) => Net = 30
            // For C: from A (30 - 0 = 30) + from B (30 - 0 = 30) => Net = 60

            var agents = new ISocialAgent[] { agentA, agentB, agentC };
            Vector2 hearthPos = new Vector2(12f, 18f);

            bool formed = SuccessionEngine.TryFormEmergentGroup(hearthPos, agents, registry, currentTick, out byte newGroupId);
            Assert(formed, "Emergent group formed successfully around hearth.");
            Assert(newGroupId > 0, "Valid non-zero group ID created.");

            Assert(registry.TryGetGroup(newGroupId, out var groupData), "Group found in registry.");
            Assert(groupData.LeaderId == agentA.AgentId, "Agent A elected leader by consensus.");
            Assert(groupData.Type == GroupType.Settlement, "Emergent group type is Settlement.");
            Assert(groupData.MemberCount == 3, "Emergent group has exactly 3 members.");
            Assert(groupData.Cohesion == 80, "Initial emergent cohesion is 80.");
            Assert(groupData.AnchorPosition == hearthPos, "Anchor position matches hearth location.");

            // Verify memberships
            Assert(agentA.Membership.Role == RoleRank.Leader, "Agent A role is Leader.");
            Assert(agentB.Membership.Role == RoleRank.Follower, "Agent B role is Follower.");
            Assert(agentB.Membership.Allegiance == 70, "Agent B allegiance is 70.");
            Assert(agentC.Membership.Role == RoleRank.Follower, "Agent C role is Follower.");
            Assert(agentC.Membership.Allegiance == 70, "Agent C allegiance is 70.");

            Debug.Log("[PASS] Test 1: Organic hearth emergence verified.");
        }

        private void ValidatePeacefulSuccession()
        {
            var registry = new GroupRegistry();
            var leader = CreateTestAgent();
            var officer = CreateTestAgent();
            var followerA = CreateTestAgent();
            var followerB = CreateTestAgent();

            var agentMap = new Dictionary<GUID, ISocialAgent>
            {
                { leader.AgentId, leader },
                { officer.AgentId, officer },
                { followerA.AgentId, followerA },
                { followerB.AgentId, followerB }
            };

            byte groupId = registry.CreateGroup(GroupType.Settlement, leader.AgentId, Vector2.zero, initialCohesion: 80);
            registry.AddMember(groupId, officer.AgentId, RoleRank.Officer, initialAllegiance: 85);
            registry.AddMember(groupId, followerA.AgentId, RoleRank.Follower, initialAllegiance: 75);
            registry.AddMember(groupId, followerB.AgentId, RoleRank.Follower, initialAllegiance: 70);

            // Give followerA very high raw respect, but officer has institutional Officer rank!
            followerA.Memory.SetAffinity(officer.AgentId, new AffinityRecord(warmth: 20, trust: 20, fear: 0, respect: 40, 1, 100));
            followerB.Memory.SetAffinity(officer.AgentId, new AffinityRecord(warmth: 20, trust: 20, fear: 0, respect: 40, 1, 100));

            followerA.Memory.SetAffinity(followerA.AgentId, new AffinityRecord(warmth: 0, trust: 0, fear: 0, respect: 0, 1, 100));
            officer.Memory.SetAffinity(followerA.AgentId, new AffinityRecord(warmth: 20, trust: 20, fear: 0, respect: 70, 1, 100));
            followerB.Memory.SetAffinity(followerA.AgentId, new AffinityRecord(warmth: 20, trust: 20, fear: 0, respect: 70, 1, 100));

            // Leader collapses from exhaustion
            leader.IsExhaustionCollapsed = true;

            Assert(SuccessionEngine.IsLeaderIncapacitatedOrDiscredited(leader.AgentId, groupId, null, registry, 100, id => agentMap[id]),
                "Leader correctly recognized as incapacitated.");

            // Execute succession
            var result = SuccessionEngine.ResolveSuccession(groupId, registry, null, 100, id => agentMap[id]);

            Assert(result.Succeeded, "Succession succeeded.");
            Assert(result.Mode == SuccessionMode.Peaceful, "Mode is Peaceful under Cohesion 80.");
            Assert(result.NewLeaderId == officer.AgentId, "Officer inherits leadership by institutional succession priority.");
            Assert(result.NewCohesion == 90, "Cohesion increased by +10 to 90 after orderly peaceful succession.");

            registry.TryGetGroup(groupId, out var updatedGroup);
            Assert(updatedGroup.LeaderId == officer.AgentId, "Registry leader ID updated to officer.");
            registry.TryGetMembership(leader.AgentId, out var oldLeaderMem);
            Assert(oldLeaderMem.Role == RoleRank.Follower, "Previous leader demoted to Follower.");

            Debug.Log("[PASS] Test 2: Peaceful succession under institutional order verified.");
        }

        private void ValidateDominanceChallengeSuccession()
        {
            var registry = new GroupRegistry();
            var leader = CreateTestAgent();
            var followerA = CreateTestAgent();
            var followerB = CreateTestAgent();

            var agentMap = new Dictionary<GUID, ISocialAgent>
            {
                { leader.AgentId, leader },
                { followerA.AgentId, followerA },
                { followerB.AgentId, followerB }
            };

            // Cohesion 40 => triggers Dominance Challenge (25 <= C < 50)
            byte groupId = registry.CreateGroup(GroupType.Settlement, leader.AgentId, Vector2.zero, initialCohesion: 40);
            registry.AddMember(groupId, followerA.AgentId, RoleRank.Follower, initialAllegiance: 70);
            registry.AddMember(groupId, followerB.AgentId, RoleRank.Follower, initialAllegiance: 70);

            // Mutual standing for contenders A and B:
            // Follower B heavily intimidates Follower A (submission of A to B is high)
            followerA.Memory.SetAffinity(followerB.AgentId, new AffinityRecord(warmth: -20, trust: -10, fear: 70, respect: 20, 1, 100));
            // Follower B does not fear follower A at all (submission of B to A is 0)
            followerB.Memory.SetAffinity(followerA.AgentId, new AffinityRecord(warmth: -20, trust: -10, fear: 0, respect: 0, 1, 100));

            // Submission of A to B = 70 + (20 * 0.8) = 86 => Dom(A) = 14
            // Submission of B to A = 0 => Dom(B) = 100
            // Follower B is overwhelmingly dominant!

            var result = SuccessionEngine.ResolveSuccession(groupId, registry, null, 100, id => agentMap[id]);

            Assert(result.Succeeded, "Succession succeeded.");
            Assert(result.Mode == SuccessionMode.DominanceChallenge, "Mode is DominanceChallenge under Cohesion 40.");
            Assert(result.NewLeaderId == followerB.AgentId, "Dominant contender B claimed leadership.");
            Assert(!result.LoserDeparted, "Loser did not depart because allegiance > 0.");
            Assert(result.NewCohesion == 30, "Cohesion penalized by -10 to 30.");

            registry.TryGetMembership(followerA.AgentId, out var loserMem);
            Assert(loserMem.Allegiance == 60, $"Loser allegiance penalized by -10 to 60 (actual: {loserMem.Allegiance}).");

            Debug.Log("[PASS] Test 3: Dominance challenge succession and loser penalty verified.");
        }

        private void ValidateDominanceLoserDeparture()
        {
            var registry = new GroupRegistry();
            var leader = CreateTestAgent();
            var loser = CreateTestAgent();
            var winner = CreateTestAgent();

            var agentMap = new Dictionary<GUID, ISocialAgent>
            {
                { leader.AgentId, leader },
                { loser.AgentId, loser },
                { winner.AgentId, winner }
            };

            // Loser enters challenge with only 10 allegiance!
            byte groupId = registry.CreateGroup(GroupType.Settlement, leader.AgentId, Vector2.zero, initialCohesion: 35);
            registry.AddMember(groupId, loser.AgentId, RoleRank.Follower, initialAllegiance: 10);
            registry.AddMember(groupId, winner.AgentId, RoleRank.Follower, initialAllegiance: 80);

            // Winner dominates loser
            loser.Memory.SetAffinity(winner.AgentId, new AffinityRecord(warmth: 0, trust: 0, fear: 60, respect: 20, 1, 100));
            winner.Memory.SetAffinity(loser.AgentId, new AffinityRecord(warmth: 0, trust: 0, fear: 0, respect: 0, 1, 100));

            var result = SuccessionEngine.ResolveSuccession(groupId, registry, null, 100, id => agentMap[id]);

            Assert(result.Succeeded, "Succession succeeded.");
            Assert(result.Mode == SuccessionMode.DominanceChallenge, "Mode is DominanceChallenge.");
            Assert(result.NewLeaderId == winner.AgentId, "Winner claimed leadership.");
            Assert(result.LoserDeparted, "Loser departed because allegiance dropped to 0!");

            Assert(!registry.TryGetMembership(loser.AgentId, out _), "Loser removed from group registry roster.");
            Assert(!loser.Membership.HasGroup, "Loser agent Membership reset to None.");

            Debug.Log("[PASS] Test 4: Dominance challenge loser departure on zero allegiance verified.");
        }

        private void ValidateFactionalSchism()
        {
            var registry = new GroupRegistry();
            var leader = CreateTestAgent();
            var contenderA = CreateTestAgent();
            var contenderB = CreateTestAgent();
            var loyalistA = CreateTestAgent();
            var loyalistB = CreateTestAgent();

            var agentMap = new Dictionary<GUID, ISocialAgent>
            {
                { leader.AgentId, leader },
                { contenderA.AgentId, contenderA },
                { contenderB.AgentId, contenderB },
                { loyalistA.AgentId, loyalistA },
                { loyalistB.AgentId, loyalistB }
            };

            // Cohesion 15 => triggers Factional Schism (< 25)
            byte originalGroupId = registry.CreateGroup(GroupType.Settlement, leader.AgentId, new Vector2(5f, 5f), initialCohesion: 15);
            registry.AddMember(originalGroupId, contenderA.AgentId, RoleRank.Follower, initialAllegiance: 80);
            registry.AddMember(originalGroupId, contenderB.AgentId, RoleRank.Follower, initialAllegiance: 80);
            registry.AddMember(originalGroupId, loyalistA.AgentId, RoleRank.Follower, initialAllegiance: 70);
            registry.AddMember(originalGroupId, loyalistB.AgentId, RoleRank.Follower, initialAllegiance: 70);

            // Contender A and B have equal standing across group
            loyalistA.Memory.SetAffinity(contenderA.AgentId, new AffinityRecord(warmth: 60, trust: 50, fear: 0, respect: 50, 1, 100));
            loyalistA.Memory.SetAffinity(contenderB.AgentId, new AffinityRecord(warmth: 10, trust: 0, fear: 0, respect: 10, 1, 100));

            loyalistB.Memory.SetAffinity(contenderA.AgentId, new AffinityRecord(warmth: 10, trust: 0, fear: 0, respect: 10, 1, 100));
            loyalistB.Memory.SetAffinity(contenderB.AgentId, new AffinityRecord(warmth: 70, trust: 60, fear: 0, respect: 50, 1, 100));

            var result = SuccessionEngine.ResolveSuccession(originalGroupId, registry, null, 100, id => agentMap[id]);

            Assert(result.Succeeded, "Succession succeeded.");
            Assert(result.Mode == SuccessionMode.FactionalSchism, "Mode is FactionalSchism under Cohesion 15.");
            Assert(result.SplinterGroupId > 0, "Valid non-zero splinter group ID created.");
            Assert(result.SplinterGroupId != originalGroupId, "Splinter group ID is distinct from original group.");

            // Contender A retained original group
            registry.TryGetGroup(originalGroupId, out var groupA);
            Assert(groupA.LeaderId == contenderA.AgentId, "Contender A leads original group.");
            Assert(groupA.Cohesion == 65, "Original group cohesion reset to 65.");

            // Contender B founded splinter group
            registry.TryGetGroup(result.SplinterGroupId, out var groupB);
            Assert(groupB.LeaderId == contenderB.AgentId, "Contender B leads splinter group.");
            Assert(groupB.Cohesion == 65, "Splinter group cohesion initialized to 65.");

            // Loyalist A remained in original group
            registry.TryGetMembership(loyalistA.AgentId, out var memLoyalistA);
            Assert(memLoyalistA.GroupId == originalGroupId, "Loyalist A stayed in original group with Contender A.");
            Assert(memLoyalistA.Allegiance == 60, "Loyalist A suffered -10 allegiance shock from schism.");

            // Loyalist B defected to splinter group
            registry.TryGetMembership(loyalistB.AgentId, out var memLoyalistB);
            Assert(memLoyalistB.GroupId == result.SplinterGroupId, "Loyalist B moved to splinter group with Contender B.");
            Assert(memLoyalistB.Allegiance == 60, "Loyalist B suffered -10 allegiance shock from schism.");

            Debug.Log("[PASS] Test 5: Factional schism and splinter group partition verified.");
        }

        private void ValidateIncapacitationAndLegitimacyWatchdog()
        {
            var registry = new GroupRegistry();
            var leader = CreateTestAgent();
            var follower = CreateTestAgent();

            var agentMap = new Dictionary<GUID, ISocialAgent>
            {
                { leader.AgentId, leader },
                { follower.AgentId, follower }
            };

            byte groupId = registry.CreateGroup(GroupType.Settlement, leader.AgentId, Vector2.zero, initialCohesion: 80);
            registry.AddMember(groupId, follower.AgentId, RoleRank.Follower, initialAllegiance: 70);

            // 1. Healthy respected leader => NOT incapacitated or discredited
            follower.Memory.SetAffinity(leader.AgentId, new AffinityRecord(warmth: 30, trust: 30, fear: 10, respect: 40, 1, 100));
            Assert(!SuccessionEngine.IsLeaderIncapacitatedOrDiscredited(leader.AgentId, groupId, null, registry, 100, id => agentMap[id]),
                "Healthy, respected leader is not incapacitated or discredited.");

            // 2. Starvation panic => Incapacitated!
            leader.IsStarvationPanicking = true;
            Assert(SuccessionEngine.IsLeaderIncapacitatedOrDiscredited(leader.AgentId, groupId, null, registry, 100, id => agentMap[id]),
                "Starvation panicking leader recognized as incapacitated.");
            leader.IsStarvationPanicking = false;

            // 3. Exhaustion collapse => Incapacitated!
            leader.IsExhaustionCollapsed = true;
            Assert(SuccessionEngine.IsLeaderIncapacitatedOrDiscredited(leader.AgentId, groupId, null, registry, 100, id => agentMap[id]),
                "Exhaustion collapsed leader recognized as incapacitated.");
            leader.IsExhaustionCollapsed = false;

            // 4. Crisis of legitimacy: Follower respect drops below zero (< 0)
            follower.Memory.SetAffinity(leader.AgentId, new AffinityRecord(warmth: -30, trust: -30, fear: 10, respect: -25, 1, 100));
            Assert(SuccessionEngine.IsLeaderIncapacitatedOrDiscredited(leader.AgentId, groupId, null, registry, 100, id => agentMap[id]),
                "Leader with negative follower respect recognized as suffering crisis of legitimacy.");

            // 5. Dead / Missing leader (default or null)
            Assert(SuccessionEngine.IsLeaderIncapacitatedOrDiscredited(default, groupId, null, registry, 100, id => agentMap[id]),
                "Default/null leader recognized as absent.");

            Debug.Log("[PASS] Test 6: Incapacitation and legitimacy crisis watchdog verified.");
        }

        private void ValidateDefensiveNullSafety()
        {
            // Null parameters everywhere
            Assert(SuccessionEngine.DetermineSuccessionMode(100) == SuccessionMode.Peaceful, "Cohesion 100 yields Peaceful.");
            Assert(SuccessionEngine.DetermineSuccessionMode(40) == SuccessionMode.DominanceChallenge, "Cohesion 40 yields DominanceChallenge.");
            Assert(SuccessionEngine.DetermineSuccessionMode(10) == SuccessionMode.FactionalSchism, "Cohesion 10 yields FactionalSchism.");

            Assert(SuccessionEngine.IsLeaderIncapacitatedOrDiscredited(default, 0, null, null, 0), "Null refs return true for leader check.");

            bool formed = SuccessionEngine.TryFormEmergentGroup(Vector2.zero, null, null, 0, out byte newGid);
            Assert(!formed && newGid == 0, "TryFormEmergentGroup with null agents returns false.");

            var res = SuccessionEngine.ResolveSuccession(0, null, null, 0);
            Assert(!res.Succeeded, "ResolveSuccession with null registry returns failed result.");

            var emptyRegistry = new GroupRegistry();
            res = SuccessionEngine.ResolveSuccession(99, emptyRegistry, null, 0);
            Assert(!res.Succeeded, "ResolveSuccession with invalid group ID returns failed result.");

            Debug.Log("[PASS] Test 7: Defensive null safety for test isolation verified.");
        }

        private void ValidateZeroAllocationHotPath()
        {
            // Pre-warm JIT
            for (int i = 0; i < 50; i++)
            {
                SuccessionEngine.DetermineSuccessionMode(60);
                SuccessionEngine.DetermineSuccessionMode(35);
                SuccessionEngine.DetermineSuccessionMode(15);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();

            // Benchmark hot path: 1,000 evaluations
            for (int i = 0; i < 1000; i++)
            {
                var mode1 = SuccessionEngine.DetermineSuccessionMode(60);
                var mode2 = SuccessionEngine.DetermineSuccessionMode(35);
                var mode3 = SuccessionEngine.DetermineSuccessionMode(15);

                if (mode1 != SuccessionMode.Peaceful || mode2 != SuccessionMode.DominanceChallenge || mode3 != SuccessionMode.FactionalSchism)
                {
                    Assert(false, "Benchmark iteration produced incorrect mode.");
                }
            }

            long totalAlloc = GC.GetAllocatedBytesForCurrentThread() - startAlloc;
            Assert(totalAlloc == 0, $"Hot path allocated {totalAlloc} bytes across 1,000 iterations! Must be strictly 0 bytes.");

            Debug.Log("[PASS] Test 8: Zero-allocation hot path verified (0 bytes allocated across 1,000 iterations).");
        }

        private static uint _guidCounter = 1;

        [StructLayout(LayoutKind.Explicit)]
        private struct GuidUnion
        {
            [FieldOffset(0)] public uint v0;
            [FieldOffset(4)] public uint v1;
            [FieldOffset(8)] public uint v2;
            [FieldOffset(12)] public uint v3;
            [FieldOffset(0)] public GUID GuidValue;
        }

        private static GUID CreateGuid()
        {
            var union = new GuidUnion { v0 = _guidCounter++, v1 = 0x11223344, v2 = 0x55667788, v3 = 0x99aabbcc };
            return union.GuidValue;
        }

        private static MockSocialAgent CreateTestAgent()
        {
            return new MockSocialAgent(CreateGuid());
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[SuccessionValidator ASSERTION FAILED] {message}");
            }
        }
    }
}
