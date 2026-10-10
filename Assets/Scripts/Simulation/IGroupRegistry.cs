using System.Collections.Generic;
using Simpiens.Cognition.Groups;
using UnityEngine;

namespace Simpiens.Simulation
{
    /// <summary>
    /// Authoritative main-thread registry for all sociopolitical collectives (Epic 8).
    /// Tracks group definitions, member rosters, and directive states.
    /// </summary>
    public interface IGroupRegistry
    {
        int ActiveGroupCount { get; }

        byte CreateGroup(GroupType type, GUID leaderId, Vector2 anchorPosition, byte initialCohesion = 100);
        bool DisbandGroup(byte groupId);
        bool TryGetGroup(byte groupId, out GroupData groupData);

        bool AddMember(byte groupId, GUID agentId, RoleRank role = RoleRank.Follower, byte initialAllegiance = 70);
        bool RemoveMember(byte groupId, GUID agentId);
        bool SetRole(byte groupId, GUID agentId, RoleRank newRole);
        bool UpdateAllegiance(GUID agentId, int delta);
        bool UpdateCohesion(byte groupId, int delta);
        bool SetCohesion(byte groupId, byte cohesion);
        bool TransferLeadership(byte groupId, GUID newLeaderId);
        bool TryGetMembership(GUID agentId, out GroupMembership membership);
        IReadOnlyCollection<GUID> GetMembers(byte groupId);

        bool SetDirective(byte groupId, in GroupDirective directive);
        bool ClearDirective(byte groupId);

        void CopySnapshotData(GroupData[] destinationBuffer, out int activeCount);
        void Tick(uint currentTick);
        void Clear();
    }
}
