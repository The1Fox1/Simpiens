using System.Collections.Generic;
using Simpiens.Cognition.Groups;
namespace Simpiens.Simulation
{
    /// <summary>
    /// Authoritative main-thread registry for all sociopolitical collectives (Epic 8).
    /// Tracks group definitions, member rosters, and directive states. Groups are many-to-many, one per kind (Epic 10 G4,
    /// D1): a crawler belongs to at most one group of each <see cref="GroupType"/>, and the registry owns its memberships.
    /// </summary>
    public interface IGroupRegistry
    {
        int ActiveGroupCount { get; }

        byte CreateGroup(GroupType type, EntityId leaderId, Vec2 anchorPosition, byte initialCohesion = 100);
        bool DisbandGroup(byte groupId);
        bool TryGetGroup(byte groupId, out GroupData groupData);

        bool AddMember(byte groupId, EntityId agentId, RoleRank role = RoleRank.Follower, byte initialAllegiance = 70);
        bool RemoveMember(byte groupId, EntityId agentId);
        bool SetRole(byte groupId, EntityId agentId, RoleRank newRole);
        bool UpdateAllegiance(EntityId agentId, byte groupId, int delta);
        bool UpdateCohesion(byte groupId, int delta);
        bool SetCohesion(byte groupId, byte cohesion);
        bool TransferLeadership(byte groupId, EntityId newLeaderId);
        bool TryGetMembership(EntityId agentId, byte groupId, out GroupMembership membership);
        bool TryGetMembership(EntityId agentId, GroupType kind, out GroupMembership membership);

        /// <summary>Every group the agent belongs to, in kind order (party, guild, settlement); empty for none.</summary>
        MembershipSet GetMemberships(EntityId agentId);

        /// <summary>Takes the agent off every roster it is on.</summary>
        void LeaveAllGroups(EntityId agentId);
        IReadOnlyCollection<EntityId> GetMembers(byte groupId);

        bool SetDirective(byte groupId, in GroupDirective directive);
        bool ClearDirective(byte groupId);

        void CopySnapshotData(GroupData[] destinationBuffer, out int activeCount);
        void Tick(uint currentTick);
        void Clear();

        /// <summary>Writes the active groups, their members and every membership into a save (Milestone 1 save/load).</summary>
        void CaptureTo(Simpiens.Simulation.Persistence.GroupSave save);

        /// <summary>Replaces every group and membership with a save's (a load).</summary>
        void RestoreFrom(Simpiens.Simulation.Persistence.GroupSave save);
    }
}
