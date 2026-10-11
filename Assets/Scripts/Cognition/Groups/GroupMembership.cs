namespace Simpiens.Cognition.Groups
{
    /// <summary>
    /// Unmanaged 8-byte stack struct representing an individual agent's standing within a group.
    /// </summary>
    public readonly struct GroupMembership
    {
        public readonly byte GroupId;
        public readonly RoleRank Role;
        public readonly byte Allegiance; // 0 to 100 loyalty metric
        public readonly uint JoinTick;

        public GroupMembership(byte groupId, RoleRank role, byte allegiance, uint joinTick)
        {
            GroupId = groupId;
            Role = role;
            Allegiance = allegiance;
            JoinTick = joinTick;
        }

        public static readonly GroupMembership None = new GroupMembership(0, RoleRank.None, 0, 0);

        public bool HasGroup => GroupId != 0;

        public bool Equals(GroupMembership other) => GroupId == other.GroupId && Role == other.Role && Allegiance == other.Allegiance && JoinTick == other.JoinTick;
        public override bool Equals(object obj) => obj is GroupMembership other && Equals(other);
        public override int GetHashCode() => (GroupId << 24) ^ ((int)Role << 16) ^ (Allegiance << 8) ^ (int)JoinTick;
        public static bool operator ==(GroupMembership left, GroupMembership right) => left.Equals(right);
        public static bool operator !=(GroupMembership left, GroupMembership right) => !left.Equals(right);
    }
}
