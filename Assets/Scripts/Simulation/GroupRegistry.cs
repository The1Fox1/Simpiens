using System;
using System.Collections.Generic;
using Simpiens.Cognition.Groups;
using Simpiens.Simulation.Persistence;
using UnityEngine;
using Simpiens.Diagnostics;
using ZLogger;

namespace Simpiens.Simulation
{
    /// <summary>
    /// Thread-safe main-thread implementation of IGroupRegistry.
    /// Manages fixed-capacity group slots (up to 64) and agent memberships with zero GC allocations.
    /// </summary>
    public class GroupRegistry : IGroupRegistry
    {
        public const int MaxGroups = 64;

        private struct GroupSlot
        {
            public bool IsActive;
            public GroupType Type;
            public GUID LeaderId;
            public Vector2 AnchorPosition;
            public GroupDirective ActiveDirective;
            public byte Cohesion;
            public HashSet<GUID> Members;
        }

        private readonly GroupSlot[] _slots;
        private readonly Dictionary<GUID, GroupMembership> _memberships;
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
                    AnchorPosition = Vector2.zero,
                    ActiveDirective = GroupDirective.None,
                    Cohesion = 100,
                    Members = new HashSet<GUID>()
                };
            }
            _memberships = new Dictionary<GUID, GroupMembership>(256);
            _activeGroupCount = 0;
        }

        public byte CreateGroup(GroupType type, GUID leaderId, Vector2 anchorPosition, byte initialCohesion = 100)
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

            // If a valid leader ID was provided, register them automatically
            if (leaderId != default)
            {
                slot.Members.Add(leaderId);
                _memberships[leaderId] = new GroupMembership(groupId, RoleRank.Leader, 100, 0);
            }

            return groupId;
        }

        public bool DisbandGroup(byte groupId)
        {
            if (groupId == 0 || groupId > MaxGroups) return false;

            int index = groupId - 1;
            ref var slot = ref _slots[index];
            if (!slot.IsActive) return false;

            // Remove all member registrations
            foreach (var memberId in slot.Members)
            {
                _memberships.Remove(memberId);
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

        public bool AddMember(byte groupId, GUID agentId, RoleRank role = RoleRank.Follower, byte initialAllegiance = 70)
        {
            if (groupId == 0 || groupId > MaxGroups || agentId == default) return false;

            int index = groupId - 1;
            ref var slot = ref _slots[index];
            if (!slot.IsActive) return false;

            // If agent already in another group, remove from previous group
            if (_memberships.TryGetValue(agentId, out var existing))
            {
                if (existing.GroupId != groupId)
                {
                    RemoveMember(existing.GroupId, agentId);
                }
            }

            slot.Members.Add(agentId);
            _memberships[agentId] = new GroupMembership(groupId, role, initialAllegiance, 0);

            if (role == RoleRank.Leader)
            {
                slot.LeaderId = agentId;
            }

            return true;
        }

        public bool RemoveMember(byte groupId, GUID agentId)
        {
            if (groupId == 0 || groupId > MaxGroups || agentId == default) return false;

            int index = groupId - 1;
            ref var slot = ref _slots[index];
            if (!slot.IsActive) return false;

            bool removed = slot.Members.Remove(agentId);
            _memberships.Remove(agentId);

            if (slot.LeaderId == agentId)
            {
                slot.LeaderId = default;
            }

            return removed;
        }

        public bool SetRole(byte groupId, GUID agentId, RoleRank newRole)
        {
            if (groupId == 0 || groupId > MaxGroups || agentId == default) return false;

            int index = groupId - 1;
            ref var slot = ref _slots[index];
            if (!slot.IsActive || !slot.Members.Contains(agentId)) return false;

            if (_memberships.TryGetValue(agentId, out var current))
            {
                _memberships[agentId] = new GroupMembership(groupId, newRole, current.Allegiance, current.JoinTick);
                if (newRole == RoleRank.Leader)
                {
                    slot.LeaderId = agentId;
                }
                return true;
            }

            return false;
        }

        public bool UpdateAllegiance(GUID agentId, int delta)
        {
            if (agentId == default) return false;

            if (_memberships.TryGetValue(agentId, out var mem))
            {
                int newAllegiance = Mathf.Clamp(mem.Allegiance + delta, 0, 100);
                _memberships[agentId] = new GroupMembership(mem.GroupId, mem.Role, (byte)newAllegiance, mem.JoinTick);
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

            int newCohesion = Mathf.Clamp(slot.Cohesion + delta, 0, 100);
            slot.Cohesion = (byte)newCohesion;
            return true;
        }

        public bool SetCohesion(byte groupId, byte cohesion)
        {
            if (groupId == 0 || groupId > MaxGroups) return false;

            int index = groupId - 1;
            ref var slot = ref _slots[index];
            if (!slot.IsActive) return false;

            slot.Cohesion = (byte)Mathf.Clamp(cohesion, 0, 100);
            return true;
        }

        public bool TransferLeadership(byte groupId, GUID newLeaderId)
        {
            if (groupId == 0 || groupId > MaxGroups || newLeaderId == default) return false;

            int index = groupId - 1;
            ref var slot = ref _slots[index];
            if (!slot.IsActive || !slot.Members.Contains(newLeaderId)) return false;

            GUID oldLeaderId = slot.LeaderId;

            // Demote old leader to Follower if still in the group
            if (oldLeaderId != default && oldLeaderId != newLeaderId && slot.Members.Contains(oldLeaderId))
            {
                if (_memberships.TryGetValue(oldLeaderId, out var oldMem))
                {
                    _memberships[oldLeaderId] = new GroupMembership(groupId, RoleRank.Follower, oldMem.Allegiance, oldMem.JoinTick);
                }
            }

            // Promote new leader
            if (_memberships.TryGetValue(newLeaderId, out var newMem))
            {
                _memberships[newLeaderId] = new GroupMembership(groupId, RoleRank.Leader, newMem.Allegiance, newMem.JoinTick);
            }

            slot.LeaderId = newLeaderId;
            // Clear prior active directive so new leadership can issue their own commands
            slot.ActiveDirective = GroupDirective.None;

            return true;
        }

        public bool TryGetMembership(GUID agentId, out GroupMembership membership)
        {
            if (agentId == default)
            {
                membership = GroupMembership.None;
                return false;
            }

            return _memberships.TryGetValue(agentId, out membership);
        }

        public IReadOnlyCollection<GUID> GetMembers(byte groupId)
        {
            if (groupId == 0 || groupId > MaxGroups) return Array.Empty<GUID>();

            int index = groupId - 1;
            ref readonly var slot = ref _slots[index];
            if (!slot.IsActive) return Array.Empty<GUID>();

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
            var members = new List<GUID>(_memberships.Count);
            var roster = new List<GUID>(16);
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

            var memberships = new MembershipRecord[_memberships.Count];
            int n = 0;
            foreach (var kvp in _memberships) memberships[n++] = new MembershipRecord { AgentId = kvp.Key, Membership = kvp.Value };
            Array.Sort(memberships, (a, b) => a.AgentId.CompareTo(b.AgentId));
            save.Memberships = memberships;
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
                for (int i = 0; i < save.Memberships.Length; i++) _memberships[save.Memberships[i].AgentId] = save.Memberships[i].Membership;
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
