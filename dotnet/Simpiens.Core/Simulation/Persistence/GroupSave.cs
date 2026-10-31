using System.Runtime.InteropServices;
using MemoryPack;
using Simpiens.Cognition.Groups;
namespace Simpiens.Simulation.Persistence
{
    /// <summary>The group registry in a save, written and read by <see cref="GroupRegistry"/>.</summary>
    [MemoryPackable]
    public sealed partial class GroupSave
    {
        /// <summary>Active groups by id. Each group's members are Members[MemberStart .. MemberStart + MemberCount).</summary>
        public GroupRecord[] Groups { get; set; }

        /// <summary>Each group's members, sorted by id.</summary>
        public EntityId[] Members { get; set; }

        /// <summary>By crawler id.</summary>
        public MembershipRecord[] Memberships { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroupRecord
    {
        public EntityId LeaderId;
        public Vec2 AnchorPosition;
        public GroupDirective ActiveDirective;
        public int MemberStart;
        public int MemberCount;
        public byte GroupId;
        public GroupType Type;
        public byte Cohesion;
        /// <summary>The group it belongs to, or 0 (Epic 10 G5, format 9; the byte was padding before).</summary>
        public byte ParentGroupId;

        public GroupRecord(byte groupId, GroupType type, EntityId leaderId, Vec2 anchorPosition, in GroupDirective directive, byte cohesion, int memberStart, int memberCount, byte parentGroupId = 0)
        {
            LeaderId = leaderId;
            AnchorPosition = anchorPosition;
            ActiveDirective = directive;
            MemberStart = memberStart;
            MemberCount = memberCount;
            GroupId = groupId;
            Type = type;
            Cohesion = cohesion;
            ParentGroupId = parentGroupId;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MembershipRecord
    {
        public EntityId AgentId;
        public GroupMembership Membership;
    }
}
