namespace Simpiens.Cognition.Groups
{
    /// <summary>
    /// Unmanaged snapshot struct embedded in SharedWorldSnapshot for lock-free worker queries.
    /// Represents the physical and organizational state of a group.
    /// </summary>
    public readonly struct GroupData
    {
        public readonly byte GroupId;
        public readonly GroupType Type;
        public readonly EntityId LeaderId;
        public readonly Vec2 AnchorPosition;
        public readonly GroupDirective ActiveDirective;
        public readonly ushort MemberCount;
        public readonly byte Cohesion; // 0 - 100 group morale/unity

        public GroupData(byte groupId, GroupType type, EntityId leaderId,
                         Vec2 anchorPosition, in GroupDirective activeDirective,
                         ushort memberCount, byte cohesion)
        {
            GroupId = groupId;
            Type = type;
            LeaderId = leaderId;
            AnchorPosition = anchorPosition;
            ActiveDirective = activeDirective;
            MemberCount = memberCount;
            Cohesion = cohesion;
        }

        public static readonly GroupData Empty = new GroupData(0, GroupType.TemporaryParty, default, Vec2.Zero, GroupDirective.None, 0, 0);

        public bool IsActive => GroupId != 0;

        /// <summary>Led by the player as patron (Epic 10 G4): the patron has no place on the roster, and nothing succeeds it.</summary>
        public bool IsPatronLed => LeaderId == Simpiens.Simulation.WorldRegistry.PlayerEntityId;
    }
}
