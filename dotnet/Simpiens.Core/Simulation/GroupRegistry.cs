using System;
using System.Collections.Generic;
using Simpiens.Cognition.Groups;
using Simpiens.Simulation.Persistence;
using Simpiens.Diagnostics;
using ZLogger;

namespace Simpiens.Simulation
{
    /// <summary>
    /// Thread-safe main-thread implementation of IGroupRegistry.
    /// Manages fixed-capacity group slots (up to 64) and agent memberships with zero GC allocations. Groups are
    /// many-to-many, one per kind (Epic 10 G4, D1): each crawler's memberships are a <see cref="MembershipSet"/>, and
    /// joining a group leaves the crawler's other group of the same kind. The registry is the memberships' one owner.
    /// </summary>
    public class GroupRegistry : IGroupRegistry
    {
        public const int MaxGroups = 64;

        private struct GroupSlot
        {
            public bool IsActive;
            public GroupType Type;
            public EntityId LeaderId;
            public Vec2 AnchorPosition;
            public GroupDirective ActiveDirective;
            public byte Cohesion;
            public HashSet<EntityId> Members;
        }

        private readonly GroupSlot[] _slots;
        private readonly Dictionary<EntityId, MembershipSet> _memberships;
        private int _activeGroupCount;

        public int ActiveGroupCount => _activeGroupCount;

        public GroupRegistry()
        {
            _slots = new GroupSlot[MaxGroups];
            for (int i = 0; i < MaxGroups; i++)
            {
                _slots[i] = new GroupSlot
                {
                    IsActive = false,
                    Type = GroupType.TemporaryParty,
                    LeaderId = default,
                    AnchorPosition = Vec2.Zero,
                    ActiveDirective = GroupDirective.None,
                    Cohesion = 100,
                    Members = new HashSet<EntityId>()
                };
            }
            _memberships = new Dictionary<EntityId, MembershipSet>(256);
            _activeGroupCount = 0;
        }

        public byte CreateGroup(GroupType type, EntityId leaderId, Vec2 anchorPosition, byte initialCohesion = 100)
        {
            // Find lowest available 1-indexed group ID
            int freeIndex = -1;
            for (int i = 0; i < MaxGroups; i++)
            {
                if (!_slots[i].IsActive)
                {
                    freeIndex = i;
                    break;
                }
            }

            if (freeIndex == -1)
            {
                SimLog.Groups.ZLogWarning($"Group capacity ({MaxGroups}) reached; cannot create a new group");
                return 0;
            }

            byte groupId = (byte)(freeIndex + 1);
            ref var slot = ref _slots[freeIndex];
            slot.IsActive = true;
            slot.Type = type;
            slot.LeaderId = leaderId;
            slot.AnchorPosition = anchorPosition;
            slot.ActiveDirective = GroupDirective.None;
            slot.Cohesion = initialCohesion;
            slot.Members.Clear();

            _activeGroupCount++;

            // If a valid leader ID was provided, register them automatically. Its group of the same kind is replaced, but it
            // stays on that group's roster (Epic 8 finding 1, fixed at Epic 10 G4.4)
            if (leaderId != default)
            {
                slot.Members.Add(leaderId);
                var set = MembershipsOf(leaderId);
                set.Set(type, new GroupMembership(groupId, RoleRank.Leader, 100, 0));
                _memberships[leaderId] = set;
            }

            return groupId;
        }

        public bool DisbandGroup(byte groupId)
        {
            if (groupId == 0 || groupId > MaxGroups) return false;

            int index = groupId - 1;
            ref var slot = ref _slots[index];
            if (!slot.IsActive) return false;

            // Remove this group from every member's memberships
            foreach (var memberId in slot.Members)
            {
                LeaveGroup(memberId, groupId);
            }

            slot.Members.Clear();
            slot.IsActive = false;
            slot.LeaderId = default;
            slot.ActiveDirective = GroupDirective.None;
            slot.Cohesion = 0;

            _activeGroupCount--;
            return true;
        }

        public bool TryGetGroup(byte groupId, out GroupData groupData)
        {
            if (groupId == 0 || groupId > MaxGroups)
            {
                groupData = GroupData.Empty;
                return false;
            }

            int index = groupId - 1;
            ref readonly var slot = ref _slots[index];
            if (!slot.IsActive)
            {
                groupData = GroupData.Empty;
                return false;
            }

            groupData = new GroupData(
                groupId,
                slot.Type,
                slot.LeaderId,
                slot.AnchorPosition,
                slot.ActiveDirective,
                (ushort)slot.Members.Count,
                slot.Cohesion
            );
            return true;
        }

        public bool AddMember(byte groupId, EntityId agentId, RoleRank role = RoleRank.Follower, byte initialAllegiance = 70)
        {
            if (groupId == 0 || groupId > MaxGroups || agentId == default) return false;

            int index = groupId - 1;
            ref var slot = ref _slots[index];
            if (!slot.IsActive) return false;

            // One group per kind: joining leaves the agent's other group of this kind
            var set = MembershipsOf(agentId);
            if (set.TryGet(slot.Type, out var existing) && existing.GroupId != groupId)
            {
                RemoveMember(existing.GroupId, agentId);
                set = MembershipsOf(agentId);
            }

            slot.Members.Add(agentId);
            set.Set(slot.Type, new GroupMembership(groupId, role, initialAllegiance, 0));
            _memberships[agentId] = set;

            if (role == RoleRank.Leader)
            {
                slot.LeaderId = agentId;
            }

            return true;
        }

        public bool RemoveMember(byte groupId, EntityId agentId)
        {
            if (groupId == 0 || groupId > MaxGroups || agentId == default) return false;

            int index = groupId - 1;
            ref var slot = ref _slots[index];
            if (!slot.IsActive) return false;

            bool removed = slot.Members.Remove(agentId);
            LeaveGroup(agentId, groupId);

            if (slot.LeaderId == agentId)
            {
                slot.LeaderId = default;
            }

            return removed;
        }

        public bool SetRole(byte groupId, EntityId agentId, RoleRank newRole)
        {
            if (groupId == 0 || groupId > MaxGroups || agentId == default) return false;

            int index = groupId - 1;
            ref var slot = ref _slots[index];
            if (!slot.IsActive || !slot.Members.Contains(agentId)) return false;

            if (TryGetMembership(agentId, groupId, out var current))
            {
                Replace(agentId, new GroupMembership(groupId, newRole, current.Allegiance, current.JoinTick));
                if (newRole == RoleRank.Leader)
                {
                    slot.LeaderId = agentId;
                }
                return true;
            }

            return false;
        }

        public bool UpdateAllegiance(EntityId agentId, byte groupId, int delta)
        {
            if (agentId == default) return false;

            if (TryGetMembership(agentId, groupId, out var mem))
            {
                int newAllegiance = SimMath.Clamp(mem.Allegiance + delta, 0, 100);
                Replace(agentId, new GroupMembership(mem.GroupId, mem.Role, (byte)newAllegiance, mem.JoinTick));
                return true;
            }

            return false;
        }

        public bool UpdateCohesion(byte groupId, int delta)
        {
            if (groupId == 0 || groupId > MaxGroups) return false;

            int index = groupId - 1;
            ref var slot = ref _slots[index];
            if (!slot.IsActive) return false;

            int newCohesion = SimMath.Clamp(slot.Cohesion + delta, 0, 100);
            slot.Cohesion = (byte)newCohesion;
            return true;
        }

        public bool SetCohesion(byte groupId, byte cohesion)
        {
            if (groupId == 0 || groupId > MaxGroups) return false;

            int index = groupId - 1;
            ref var slot = ref _slots[index];
            if (!slot.IsActive) return false;

            slot.Cohesion = (byte)SimMath.Clamp(cohesion, 0, 100);
            return true;
        }

        public bool TransferLeadership(byte groupId, EntityId newLeaderId)
        {
            if (groupId == 0 || groupId > MaxGroups || newLeaderId == default) return false;

            int index = groupId - 1;
            ref var slot = ref _slots[index];
            if (!slot.IsActive || !slot.Members.Contains(newLeaderId)) return false;

            EntityId oldLeaderId = slot.LeaderId;

            // Demote old leader to Follower if still in the group
            if (oldLeaderId != default && oldLeaderId != newLeaderId && slot.Members.Contains(oldLeaderId))
            {
                if (TryGetMembership(oldLeaderId, groupId, out var oldMem))
                {
                    Replace(oldLeaderId, new GroupMembership(groupId, RoleRank.Follower, oldMem.Allegiance, oldMem.JoinTick));
                }
            }

            // Promote new leader
            if (TryGetMembership(newLeaderId, groupId, out var newMem))
            {
                Replace(newLeaderId, new GroupMembership(groupId, RoleRank.Leader, newMem.Allegiance, newMem.JoinTick));
            }

            slot.LeaderId = newLeaderId;
            // Clear prior active directive so new leadership can issue their own commands
            slot.ActiveDirective = GroupDirective.None;

            return true;
        }

        public bool TryGetMembership(EntityId agentId, byte groupId, out GroupMembership membership)
        {
            membership = GroupMembership.None;
            return agentId != default && _memberships.TryGetValue(agentId, out var set) && set.TryGet(groupId, out membership);
        }

        public bool TryGetMembership(EntityId agentId, GroupType kind, out GroupMembership membership)
        {
            membership = GroupMembership.None;
            return agentId != default && _memberships.TryGetValue(agentId, out var set) && set.TryGet(kind, out membership);
        }

        public MembershipSet GetMemberships(EntityId agentId) => MembershipsOf(agentId);

        public void LeaveAllGroups(EntityId agentId)
        {
            if (agentId == default || !_memberships.TryGetValue(agentId, out var set)) return;
            for (int i = set.Count - 1; i >= 0; i--) RemoveMember(set[i].GroupId, agentId);
            _memberships.Remove(agentId);
        }

        private MembershipSet MembershipsOf(EntityId agentId) => agentId != default && _memberships.TryGetValue(agentId, out var set) ? set : MembershipSet.Empty;

        // The agent's membership in membership's group, replaced in place
        private void Replace(EntityId agentId, in GroupMembership membership)
        {
            var set = MembershipsOf(agentId);
            if (set.Update(membership)) _memberships[agentId] = set;
        }

        // The agent's membership in groupId removed; an agent with none left has no entry
        private void LeaveGroup(EntityId agentId, byte groupId)
        {
            if (!_memberships.TryGetValue(agentId, out var set) || !set.Remove(groupId)) return;
            if (set.HasAny) _memberships[agentId] = set;
            else _memberships.Remove(agentId);
        }

        public IReadOnlyCollection<EntityId> GetMembers(byte groupId)
        {
            if (groupId == 0 || groupId > MaxGroups) return Array.Empty<EntityId>();

            int index = groupId - 1;
            ref readonly var slot = ref _slots[index];
            if (!slot.IsActive) return Array.Empty<EntityId>();

            return slot.Members;
        }

        public bool SetDirective(byte groupId, in GroupDirective directive)
        {
            if (groupId == 0 || groupId > MaxGroups) return false;

            int index = groupId - 1;
            ref var slot = ref _slots[index];
            if (!slot.IsActive) return false;

            slot.ActiveDirective = directive;
            return true;
        }

        public bool ClearDirective(byte groupId)
        {
            if (groupId == 0 || groupId > MaxGroups) return false;

            int index = groupId - 1;
            ref var slot = ref _slots[index];
            if (!slot.IsActive) return false;

            slot.ActiveDirective = GroupDirective.None;
            return true;
        }

        public void CopySnapshotData(GroupData[] destinationBuffer, out int activeCount)
        {
            if (destinationBuffer == null)
            {
                activeCount = 0;
                return;
            }

            int count = 0;
            int bufferLen = destinationBuffer.Length;

            for (int i = 0; i < MaxGroups && count < bufferLen; i++)
            {
                ref readonly var slot = ref _slots[i];
                if (slot.IsActive)
                {
                    destinationBuffer[count] = new GroupData(
                        (byte)(i + 1),
                        slot.Type,
                        slot.LeaderId,
                        slot.AnchorPosition,
                        slot.ActiveDirective,
                        (ushort)slot.Members.Count,
                        slot.Cohesion
                    );
                    count++;
                }
            }

            activeCount = count;
        }

        public void Tick(uint currentTick)
        {
            for (int i = 0; i < MaxGroups; i++)
            {
                ref var slot = ref _slots[i];
                if (!slot.IsActive) continue;

                // Check directive expiration
                if (slot.ActiveDirective.Type != DirectiveType.None)
                {
                    if (currentTick > slot.ActiveDirective.ExpirationTick)
                    {
                        slot.ActiveDirective = GroupDirective.None;
                    }
                }
            }
        }

        public void CaptureTo(GroupSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            var groups = new List<GroupRecord>(_activeGroupCount);
            var members = new List<EntityId>(_memberships.Count);
            var roster = new List<EntityId>(16);
            for (int i = 0; i < MaxGroups; i++)
            {
                ref readonly var slot = ref _slots[i];
                if (!slot.IsActive) continue;

                roster.Clear();
                roster.AddRange(slot.Members);
                roster.Sort((a, b) => a.CompareTo(b));
                groups.Add(new GroupRecord((byte)(i + 1), slot.Type, slot.LeaderId, slot.AnchorPosition, slot.ActiveDirective, slot.Cohesion, members.Count, roster.Count));
                members.AddRange(roster);
            }
            save.Groups = groups.ToArray();
            save.Members = members.ToArray();

            // Every membership, several per crawler, sorted by crawler and then group
            var memberships = new List<MembershipRecord>(_memberships.Count);
            foreach (var kvp in _memberships)
            {
                for (int i = 0; i < kvp.Value.Count; i++) memberships.Add(new MembershipRecord { AgentId = kvp.Key, Membership = kvp.Value[i] });
            }
            memberships.Sort((a, b) =>
            {
                int byAgent = a.AgentId.CompareTo(b.AgentId);
                return byAgent != 0 ? byAgent : a.Membership.GroupId.CompareTo(b.Membership.GroupId);
            });
            save.Memberships = memberships.ToArray();
        }

        public void RestoreFrom(GroupSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));

            Clear();
            if (save.Groups != null)
            {
                for (int g = 0; g < save.Groups.Length; g++)
                {
                    var record = save.Groups[g];
                    if (record.GroupId == 0 || record.GroupId > MaxGroups) continue;
                    ref var slot = ref _slots[record.GroupId - 1];
                    slot.IsActive = true;
                    slot.Type = record.Type;
                    slot.LeaderId = record.LeaderId;
                    slot.AnchorPosition = record.AnchorPosition;
                    slot.ActiveDirective = record.ActiveDirective;
                    slot.Cohesion = record.Cohesion;
                    for (int m = 0; m < record.MemberCount; m++) slot.Members.Add(save.Members[record.MemberStart + m]);
                    _activeGroupCount++;
                }
            }
            if (save.Memberships != null)
            {
                for (int i = 0; i < save.Memberships.Length; i++)
                {
                    var record = save.Memberships[i];
                    byte groupId = record.Membership.GroupId;
                    if (groupId == 0 || groupId > MaxGroups || !_slots[groupId - 1].IsActive) continue;
                    var set = MembershipsOf(record.AgentId);
                    set.Set(_slots[groupId - 1].Type, record.Membership);
                    _memberships[record.AgentId] = set;
                }
            }
        }

        public void Clear()
        {
            for (int i = 0; i < MaxGroups; i++)
            {
                _slots[i].Members.Clear();
                _slots[i].IsActive = false;
                _slots[i].LeaderId = default;
                _slots[i].ActiveDirective = GroupDirective.None;
                _slots[i].Cohesion = 100;
            }
            _memberships.Clear();
            _activeGroupCount = 0;
        }
    }
}
