using System.Runtime.CompilerServices;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Cognition.Groups
{
    /// <summary>A crawler's memberships, inline (unmanaged).</summary>
    [InlineArray(MembershipSet.Capacity)]
    public struct MembershipSlots
    {
        private GroupMembership _membership;
    }

    /// <summary>The kinds of a crawler's memberships, parallel to <see cref="MembershipSlots"/>.</summary>
    [InlineArray(MembershipSet.Capacity)]
    public struct MembershipKinds
    {
        private GroupType _kind;
    }

    /// <summary>
    /// The groups a crawler belongs to (Epic 10 G4, decision D1: groups are many-to-many, one per kind). At most one
    /// membership per <see cref="GroupType"/>, ordered by kind, which is also the order a crawler obeys directives in:
    /// its party's, then its guild's, then its settlement's. Room for <see cref="Capacity"/>, inline, so copying a set
    /// allocates nothing. The group registry owns the authoritative sets; a crawler holds a copy for cognition.
    /// </summary>
    public struct MembershipSet
    {
        /// <summary>Room for today's three kinds and one more (Part 2's workshop).</summary>
        public const int Capacity = 4;

        private MembershipSlots _memberships;
        private MembershipKinds _kinds;
        private int _count;

        public static MembershipSet Empty => default;

        public readonly int Count => _count;
        public readonly bool HasAny => _count > 0;

        /// <summary>The <paramref name="index"/>-th membership, in kind order.</summary>
        public readonly GroupMembership this[int index] => _memberships[index];

        /// <summary>The kind of the <paramref name="index"/>-th membership's group.</summary>
        public readonly GroupType KindAt(int index) => _kinds[index];

        /// <summary>The first membership in kind order, or <see cref="GroupMembership.None"/>.</summary>
        public readonly GroupMembership First => _count > 0 ? _memberships[0] : GroupMembership.None;

        public readonly bool TryGet(byte groupId, out GroupMembership membership)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_memberships[i].GroupId != groupId) continue;
                membership = _memberships[i];
                return true;
            }
            membership = GroupMembership.None;
            return false;
        }

        public readonly bool TryGet(GroupType kind, out GroupMembership membership)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_kinds[i] != kind) continue;
                membership = _memberships[i];
                return true;
            }
            membership = GroupMembership.None;
            return false;
        }

        public readonly bool Has(GroupType kind) => TryGet(kind, out _);

        /// <summary>
        /// The membership whose orders the crawler follows: the first, in kind order, whose group has a directive active at
        /// <paramref name="tick"/> in <paramref name="snapshot"/>; otherwise the first membership; None without any.
        /// </summary>
        public readonly GroupMembership DirectiveMembership(SharedWorldSnapshot snapshot, uint tick)
        {
            if (snapshot != null)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (snapshot.TryGetGroup(_memberships[i].GroupId, out var group) && group.ActiveDirective.IsActive(tick)) return _memberships[i];
                }
            }
            return First;
        }

        /// <summary>
        /// Puts <paramref name="membership"/> in, as the crawler's group of <paramref name="kind"/>: it replaces the
        /// membership in the same group, or else the one of the same kind (one per kind, D1). Returns the group id it
        /// replaced (0 when none, or when it was the same group).
        /// </summary>
        public byte Set(GroupType kind, in GroupMembership membership)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_memberships[i].GroupId == membership.GroupId || _kinds[i] == kind)
                {
                    byte replaced = _memberships[i].GroupId != membership.GroupId ? _memberships[i].GroupId : (byte)0;
                    RemoveAt(i);
                    Insert(kind, membership);
                    return replaced;
                }
            }
            Insert(kind, membership);
            return 0;
        }

        /// <summary>Replaces the membership in <paramref name="membership"/>'s group, if the crawler has one there.</summary>
        public bool Update(in GroupMembership membership)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_memberships[i].GroupId != membership.GroupId) continue;
                _memberships[i] = membership;
                return true;
            }
            return false;
        }

        public bool Remove(byte groupId)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_memberships[i].GroupId != groupId) continue;
                RemoveAt(i);
                return true;
            }
            return false;
        }

        private void Insert(GroupType kind, in GroupMembership membership)
        {
            if (_count >= Capacity) throw new System.InvalidOperationException($"A crawler belongs to at most {Capacity} groups, one per kind.");
            int at = _count;
            while (at > 0 && _kinds[at - 1] > kind) at--;
            for (int i = _count; i > at; i--)
            {
                _memberships[i] = _memberships[i - 1];
                _kinds[i] = _kinds[i - 1];
            }
            _memberships[at] = membership;
            _kinds[at] = kind;
            _count++;
        }

        private void RemoveAt(int index)
        {
            for (int i = index; i < _count - 1; i++)
            {
                _memberships[i] = _memberships[i + 1];
                _kinds[i] = _kinds[i + 1];
            }
            _count--;
            _memberships[_count] = default;
            _kinds[_count] = default;
        }
    }
}
