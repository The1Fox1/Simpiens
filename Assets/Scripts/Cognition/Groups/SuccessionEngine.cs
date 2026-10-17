using System;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Memory;
using Simpiens.Simulation;
using UnityEngine;

namespace Simpiens.Cognition.Groups
{
    /// <summary>
    /// Pure C# domain engine for bottom-up group emergence, leader incapacitation detection,
    /// and tripartite leadership succession (Peaceful, Dominance Challenge, Factional Schism) (Epic 8 Phase 3).
    /// Engineered for defensive null-safety and strictly zero GC allocations in hot paths.
    /// </summary>
    public static class SuccessionEngine
    {
        private struct CandidateScore
        {
            public EntityId Id;
            public bool IsOfficer;
            public int TotalRespect;
            public byte Allegiance;
        }

        private static readonly List<EntityId> _candidateBuffer = new List<EntityId>(64);
        private static readonly List<CandidateScore> _scoreBuffer = new List<CandidateScore>(64);
        private static readonly List<EntityId> _followersToMove = new List<EntityId>(64);
        private static readonly List<ISocialAgent> _unalignedCluster = new List<ISocialAgent>(16);

        /// <summary>
        /// Determines succession mode based on group cohesion:
        /// Cohesion >= 50 => Peaceful
        /// 25 <= Cohesion < 50 => DominanceChallenge
        /// Cohesion < 25 => FactionalSchism
        /// </summary>
        public static SuccessionMode DetermineSuccessionMode(byte cohesion)
        {
            if (cohesion >= 50) return SuccessionMode.Peaceful;
            if (cohesion >= 25) return SuccessionMode.DominanceChallenge;
            return SuccessionMode.FactionalSchism;
        }

        /// <summary>
        /// Evaluates whether an active group leader is incapacitated (exhaustion collapsed, starvation panicking),
        /// dead/missing from the world, or suffering a crisis of legitimacy (average follower respect < 0).
        /// </summary>
        public static bool IsLeaderIncapacitatedOrDiscredited(
            EntityId leaderId,
            byte groupId,
            IWorldRegistry worldRegistry,
            IGroupRegistry groupRegistry,
            uint currentTick,
            Func<EntityId, ISocialAgent> agentLookup = null)
        {
            if (leaderId == default) return true;

            ISocialAgent GetAgent(EntityId id)
            {
                if (agentLookup != null) return agentLookup(id);
                return worldRegistry?.GetNode(id)?.Agent;
            }

            var leaderAgent = GetAgent(leaderId);
            if (leaderAgent == null) return true; // Leader missing or dead
            if (leaderAgent.IsExhaustionCollapsed || leaderAgent.IsStarvationPanicking) return true;

            // Check crisis of legitimacy: Average follower respect < 0
            if (groupRegistry != null && groupRegistry.TryGetGroup(groupId, out var groupData))
            {
                if (groupData.MemberCount >= 2)
                {
                    var members = groupRegistry.GetMembers(groupId);
                    if (members != null && members.Count >= 2)
                    {
                        int totalFollowerRespect = 0;
                        int followerCount = 0;

                        foreach (var m in members)
                        {
                            if (m == leaderId) continue;
                            var follower = GetAgent(m);
                            if (follower != null && follower.Memory != null)
                            {
                                totalFollowerRespect += follower.Memory.GetAffinity(leaderId, currentTick).Respect;
                                followerCount++;
                            }
                        }

                        if (followerCount > 0 && totalFollowerRespect < 0)
                        {
                            return true; // Discredited: average follower respect is negative
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Evaluates bottom-up group emergence around a gathering anchor.
        /// When >= 3 unaligned agents spend continuous ticks around the anchor accumulating Warmth >= 40 and Trust >= 30,
        /// a consensus candidate with the highest collective (Respect - Fear) standing is elected Leader.
        /// </summary>
        public static bool TryFormEmergentGroup(
            Vector2 anchorPosition,
            IReadOnlyList<ISocialAgent> agentsAtAnchor,
            IGroupRegistry groupRegistry,
            uint currentTick,
            out byte newGroupId,
            int minAgents = 3,
            sbyte minWarmth = 40,
            sbyte minTrust = 30)
        {
            newGroupId = 0;
            if (agentsAtAnchor == null || groupRegistry == null || agentsAtAnchor.Count < minAgents)
            {
                return false;
            }

            _unalignedCluster.Clear();
            int totalAgents = agentsAtAnchor.Count;
            for (int i = 0; i < totalAgents; i++)
            {
                var agent = agentsAtAnchor[i];
                if (agent != null && !agent.Membership.HasGroup && !agent.IsExhaustionCollapsed)
                {
                    _unalignedCluster.Add(agent);
                }
            }

            if (_unalignedCluster.Count < minAgents)
            {
                return false;
            }

            // Verify mutual dyadic bonds across the unaligned cluster: Warmth >= minWarmth, Trust >= minTrust
            int clusterSize = _unalignedCluster.Count;
            for (int i = 0; i < clusterSize; i++)
            {
                var agentA = _unalignedCluster[i];
                if (agentA.Memory == null) return false;

                for (int j = i + 1; j < clusterSize; j++)
                {
                    var agentB = _unalignedCluster[j];
                    if (agentB.Memory == null) return false;

                    var affA = agentA.Memory.GetAffinity(agentB.AgentId, currentTick);
                    var affB = agentB.Memory.GetAffinity(agentA.AgentId, currentTick);

                    if (affA.Warmth < minWarmth || affA.Trust < minTrust ||
                        affB.Warmth < minWarmth || affB.Trust < minTrust)
                    {
                        return false;
                    }
                }
            }

            // Consensus election: Candidate with highest collective (Respect - Fear) standing across peers
            int bestScore = int.MinValue;
            ISocialAgent leaderCandidate = null;

            for (int i = 0; i < clusterSize; i++)
            {
                var candidate = _unalignedCluster[i];
                int candidateScore = 0;

                for (int j = 0; j < clusterSize; j++)
                {
                    if (i == j) continue;
                    var peer = _unalignedCluster[j];
                    var aff = peer.Memory.GetAffinity(candidate.AgentId, currentTick);
                    candidateScore += (aff.Respect - aff.Fear);
                }

                if (candidateScore > bestScore || leaderCandidate == null)
                {
                    bestScore = candidateScore;
                    leaderCandidate = candidate;
                }
            }

            if (leaderCandidate == null)
            {
                return false;
            }

            // Register new group
            byte groupId = groupRegistry.CreateGroup(GroupType.Settlement, leaderCandidate.AgentId, anchorPosition, initialCohesion: 80);
            if (groupId == 0) return false;

            // Set leader membership & events
            leaderCandidate.Membership = new GroupMembership(groupId, RoleRank.Leader, 100, currentTick);
            leaderCandidate.Memory.RecordEvent(MemoryEventType.GroupFormed, leaderCandidate.AgentId, Vector2Int.RoundToInt(anchorPosition), currentTick);
            leaderCandidate.Memory.RecordEvent(MemoryEventType.JoinedGroup, leaderCandidate.AgentId, Vector2Int.RoundToInt(anchorPosition), currentTick);
            leaderCandidate.Memory.RecordEvent(MemoryEventType.LeadershipAssumed, leaderCandidate.AgentId, Vector2Int.RoundToInt(anchorPosition), currentTick);

            // Add followers
            for (int i = 0; i < clusterSize; i++)
            {
                var follower = _unalignedCluster[i];
                if (follower == leaderCandidate) continue;

                groupRegistry.AddMember(groupId, follower.AgentId, RoleRank.Follower, initialAllegiance: 70);
                follower.Membership = new GroupMembership(groupId, RoleRank.Follower, 70, currentTick);
                follower.Memory.RecordEvent(MemoryEventType.GroupFormed, leaderCandidate.AgentId, Vector2Int.RoundToInt(anchorPosition), currentTick);
                follower.Memory.RecordEvent(MemoryEventType.JoinedGroup, leaderCandidate.AgentId, Vector2Int.RoundToInt(anchorPosition), currentTick);
            }

            newGroupId = groupId;
            return true;
        }

        /// <summary>
        /// Resolves leadership succession for a group with zero GC allocations.
        /// Executes Peaceful succession, Dominance Challenge, or Factional Schism according to group cohesion.
        /// </summary>
        public static SuccessionResult ResolveSuccession(
            byte groupId,
            IGroupRegistry groupRegistry,
            IWorldRegistry worldRegistry,
            uint currentTick,
            Func<EntityId, ISocialAgent> agentLookup = null)
        {
            if (groupRegistry == null || !groupRegistry.TryGetGroup(groupId, out var groupData))
            {
                return SuccessionResult.Failed;
            }

            ISocialAgent GetAgent(EntityId id)
            {
                if (agentLookup != null) return agentLookup(id);
                return worldRegistry?.GetNode(id)?.Agent;
            }

            var members = groupRegistry.GetMembers(groupId);
            if (members == null || members.Count <= 1)
            {
                // Group cannot sustain leadership with 0 or 1 member
                groupRegistry.DisbandGroup(groupId);
                return SuccessionResult.Failed;
            }

            _candidateBuffer.Clear();
            foreach (var m in members)
            {
                if (m != groupData.LeaderId)
                {
                    _candidateBuffer.Add(m);
                }
            }

            if (_candidateBuffer.Count == 0)
            {
                groupRegistry.DisbandGroup(groupId);
                return SuccessionResult.Failed;
            }

            // Score candidates: officer priority, group-wide incoming respect, allegiance
            _scoreBuffer.Clear();
            for (int i = 0; i < _candidateBuffer.Count; i++)
            {
                EntityId candidateId = _candidateBuffer[i];
                groupRegistry.TryGetMembership(candidateId, out var mem);

                int totalRespect = 0;
                foreach (var m in members)
                {
                    if (m == candidateId) continue;
                    var agent = GetAgent(m);
                    if (agent != null && agent.Memory != null)
                    {
                        totalRespect += agent.Memory.GetAffinity(candidateId, currentTick).Respect;
                    }
                }

                _scoreBuffer.Add(new CandidateScore
                {
                    Id = candidateId,
                    IsOfficer = mem.Role == RoleRank.Officer,
                    TotalRespect = totalRespect,
                    Allegiance = mem.Allegiance
                });
            }

            // Sort candidate scores descending: Officer first, then TotalRespect, then Allegiance
            _scoreBuffer.Sort((a, b) =>
            {
                if (a.IsOfficer != b.IsOfficer) return b.IsOfficer.CompareTo(a.IsOfficer);
                if (a.TotalRespect != b.TotalRespect) return b.TotalRespect.CompareTo(a.TotalRespect);
                return b.Allegiance.CompareTo(a.Allegiance);
            });

            SuccessionMode mode = DetermineSuccessionMode(groupData.Cohesion);

            // Fall back to Peaceful if only 1 viable candidate exists
            if (_scoreBuffer.Count == 1 && mode != SuccessionMode.Peaceful)
            {
                mode = SuccessionMode.Peaceful;
            }

            switch (mode)
            {
                case SuccessionMode.Peaceful:
                {
                    EntityId winnerId = _scoreBuffer[0].Id;
                    groupRegistry.TransferLeadership(groupId, winnerId);
                    groupRegistry.UpdateCohesion(groupId, +10);

                    groupRegistry.TryGetGroup(groupId, out var updatedGroup);

                    // Sync memberships & log memory events
                    var winnerAgent = GetAgent(winnerId);
                    if (winnerAgent != null)
                    {
                        groupRegistry.TryGetMembership(winnerId, out var winMem);
                        winnerAgent.Membership = winMem;
                        winnerAgent.Memory?.RecordEvent(MemoryEventType.LeadershipAssumed, winnerId, Vector2Int.zero, currentTick);
                        winnerAgent.Memory?.RecordEvent(MemoryEventType.Promoted, winnerId, Vector2Int.zero, currentTick);
                    }

                    var oldLeaderAgent = GetAgent(groupData.LeaderId);
                    if (oldLeaderAgent != null && groupRegistry.TryGetMembership(groupData.LeaderId, out var oldMem))
                    {
                        oldLeaderAgent.Membership = oldMem;
                        oldLeaderAgent.Memory?.RecordEvent(MemoryEventType.Demoted, winnerId, Vector2Int.zero, currentTick);
                    }

                    return SuccessionResult.Peaceful(groupId, winnerId, groupData.LeaderId, updatedGroup.Cohesion);
                }

                case SuccessionMode.DominanceChallenge:
                {
                    EntityId contenderA = _scoreBuffer[0].Id;
                    EntityId contenderB = _scoreBuffer[1].Id;

                    var agentA = GetAgent(contenderA);
                    var agentB = GetAgent(contenderB);

                    float subAtoB = agentA != null && agentA.Memory != null
                        ? agentA.Memory.CalculateSubmissionScore(contenderB, currentTick)
                        : 0f;
                    float subBtoA = agentB != null && agentB.Memory != null
                        ? agentB.Memory.CalculateSubmissionScore(contenderA, currentTick)
                        : 0f;

                    float domA = 100f - subAtoB;
                    float domB = 100f - subBtoA;

                    EntityId winnerId;
                    EntityId loserId;
                    if (domA >= domB)
                    {
                        winnerId = contenderA;
                        loserId = contenderB;
                    }
                    else
                    {
                        winnerId = contenderB;
                        loserId = contenderA;
                    }

                    groupRegistry.TransferLeadership(groupId, winnerId);
                    groupRegistry.SetRole(groupId, loserId, RoleRank.Follower);
                    groupRegistry.UpdateAllegiance(loserId, -10);

                    groupRegistry.TryGetMembership(loserId, out var loserMem);
                    bool loserDeparted = (loserMem.Allegiance == 0);

                    if (loserDeparted)
                    {
                        groupRegistry.RemoveMember(groupId, loserId);
                        var loserAgent = GetAgent(loserId);
                        if (loserAgent != null)
                        {
                            loserAgent.Membership = GroupMembership.None;
                            loserAgent.Memory?.RecordEvent(MemoryEventType.DesertedGroup, winnerId, Vector2Int.zero, currentTick);
                        }
                    }
                    else
                    {
                        var loserAgent = GetAgent(loserId);
                        if (loserAgent != null)
                        {
                            loserAgent.Membership = loserMem;
                            loserAgent.Memory?.RecordEvent(MemoryEventType.DominanceChallenged, winnerId, Vector2Int.zero, currentTick);
                        }
                    }

                    groupRegistry.UpdateCohesion(groupId, -10);
                    groupRegistry.TryGetGroup(groupId, out var updatedGroup);

                    var winnerAgent = GetAgent(winnerId);
                    if (winnerAgent != null)
                    {
                        groupRegistry.TryGetMembership(winnerId, out var winMem);
                        winnerAgent.Membership = winMem;
                        winnerAgent.Memory?.RecordEvent(MemoryEventType.LeadershipAssumed, winnerId, Vector2Int.zero, currentTick);
                        winnerAgent.Memory?.RecordEvent(MemoryEventType.DominanceChallenged, loserId, Vector2Int.zero, currentTick);
                    }

                    var oldLeaderAgent = GetAgent(groupData.LeaderId);
                    if (oldLeaderAgent != null && groupRegistry.TryGetMembership(groupData.LeaderId, out var oldMem))
                    {
                        oldLeaderAgent.Membership = oldMem;
                        oldLeaderAgent.Memory?.RecordEvent(MemoryEventType.Demoted, winnerId, Vector2Int.zero, currentTick);
                    }

                    return SuccessionResult.Dominance(groupId, winnerId, loserId, loserDeparted, groupData.LeaderId, updatedGroup.Cohesion);
                }

                case SuccessionMode.FactionalSchism:
                {
                    EntityId contenderA = _scoreBuffer[0].Id;
                    EntityId contenderB = _scoreBuffer[1].Id;

                    // Contender A leads original group
                    groupRegistry.TransferLeadership(groupId, contenderA);

                    // Contender B forms splinter group
                    byte splinterGroupId = groupRegistry.CreateGroup(groupData.Type, contenderB, groupData.AnchorPosition, initialCohesion: 65);

                    // Split followers based on dyadic affinity (Warmth + Trust)
                    _followersToMove.Clear();
                    foreach (var m in members)
                    {
                        if (m == contenderA || m == contenderB || m == groupData.LeaderId) continue;

                        var mAgent = GetAgent(m);
                        float affA = 0f;
                        float affB = 0f;
                        if (mAgent != null && mAgent.Memory != null)
                        {
                            var recA = mAgent.Memory.GetAffinity(contenderA, currentTick);
                            var recB = mAgent.Memory.GetAffinity(contenderB, currentTick);
                            affA = recA.Warmth + recA.Trust;
                            affB = recB.Warmth + recB.Trust;
                        }

                        if (affB > affA)
                        {
                            _followersToMove.Add(m);
                        }
                        else
                        {
                            // Stays in Group 1, suffers -10 allegiance from schism
                            groupRegistry.UpdateAllegiance(m, -10);
                            groupRegistry.TryGetMembership(m, out var updatedMem);
                            if (mAgent != null)
                            {
                                mAgent.Membership = updatedMem;
                                mAgent.Memory?.RecordEvent(MemoryEventType.GroupSchism, contenderB, Vector2Int.zero, currentTick);
                            }
                        }
                    }

                    // Move followers to splinter group
                    for (int i = 0; i < _followersToMove.Count; i++)
                    {
                        EntityId followerId = _followersToMove[i];
                        groupRegistry.TryGetMembership(followerId, out var curMem);
                        byte remainingAllegiance = (byte)Mathf.Max(0, curMem.Allegiance - 10);

                        groupRegistry.AddMember(splinterGroupId, followerId, RoleRank.Follower, remainingAllegiance);
                        var fAgent = GetAgent(followerId);
                        if (fAgent != null)
                        {
                            groupRegistry.TryGetMembership(followerId, out var newMem);
                            fAgent.Membership = newMem;
                            fAgent.Memory?.RecordEvent(MemoryEventType.LeftGroup, contenderA, Vector2Int.zero, currentTick);
                            fAgent.Memory?.RecordEvent(MemoryEventType.JoinedGroup, contenderB, Vector2Int.zero, currentTick);
                            fAgent.Memory?.RecordEvent(MemoryEventType.GroupSchism, contenderA, Vector2Int.zero, currentTick);
                        }
                    }

                    // Reset cohesion on Group 1
                    groupRegistry.SetCohesion(groupId, 65);

                    // Sync contender A & B agent instances
                    var agentA = GetAgent(contenderA);
                    if (agentA != null)
                    {
                        groupRegistry.TryGetMembership(contenderA, out var memA);
                        agentA.Membership = memA;
                        agentA.Memory?.RecordEvent(MemoryEventType.LeadershipAssumed, contenderA, Vector2Int.zero, currentTick);
                        agentA.Memory?.RecordEvent(MemoryEventType.GroupSchism, contenderB, Vector2Int.zero, currentTick);
                    }

                    var agentB = GetAgent(contenderB);
                    if (agentB != null)
                    {
                        groupRegistry.TryGetMembership(contenderB, out var memB);
                        agentB.Membership = memB;
                        agentB.Memory?.RecordEvent(MemoryEventType.LeadershipAssumed, contenderB, Vector2Int.zero, currentTick);
                        agentB.Memory?.RecordEvent(MemoryEventType.GroupSchism, contenderA, Vector2Int.zero, currentTick);
                    }

                    return SuccessionResult.Schism(groupId, splinterGroupId, contenderA, contenderB, groupData.LeaderId, 65);
                }

                default:
                    return SuccessionResult.Failed;
            }
        }
    }
}
