using System.Runtime.InteropServices;
using Simpiens.Simulation.Economy;

namespace Simpiens.Simulation.Guild
{
    /// <summary>Where an expedition is (Epic 10 G5, D9), in order. The guild moves it on, judging the members' states.</summary>
    public enum ExpeditionStage : byte
    {
        None = 0,
        /// <summary>At the stall, until every member is there, fed and well, or the muster's time is up.</summary>
        Muster = 1,
        /// <summary>To the target sector, together.</summary>
        March = 2,
        /// <summary>Each member works the node it claimed, until the goal is met, the nodes are spent or the party turns back.</summary>
        Work = 3,
        /// <summary>Back to the stall.</summary>
        Return = 4,
        /// <summary>The haul goes into the stall's stock and the members are paid their shares; then the expedition ends.</summary>
        Deliver = 5,
        /// <summary>A clear's fight (Epic 10 Part 3, K7.1): the party fights the lair's creatures, its focus one at a time, until the lair is empty or it turns back.</summary>
        Fight = 6
    }

    /// <summary>What an expedition is sent to do (Epic 10 Part 3, K7.1, Q8): gather a sector's yield, or clear a lair. Saved by value.</summary>
    public enum ExpeditionKind : byte
    {
        Gather = 0,
        Clear = 1
    }

    /// <summary>How hurt a member gets before the party turns back (D10): below health 70, 55 or 40.</summary>
    public enum RetreatRule : byte
    {
        Cautious = 0,
        Steady = 1,
        Bold = 2
    }

    /// <summary>A member's part in an expedition.</summary>
    public enum ExpeditionMemberStatus : byte
    {
        Going = 0,
        /// <summary>Downed on the way or at work: it limps home with the party (D10).</summary>
        Downed = 1,
        /// <summary>Not at the muster, fed and well, in time; or it judged the order and stayed (D8).</summary>
        StayedBehind = 2,
        /// <summary>It judged the order and left the party (D8).</summary>
        Deserted = 3,
        /// <summary>It left the guild, or the outpost, while the party was out.</summary>
        Left = 4
    }

    /// <summary>One member of an expedition's party. Saved as raw memory (format 9).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ExpeditionMember
    {
        public EntityId CrawlerId;

        /// <summary>The node it works, claimed when the party reaches the sector, and where it is (cognition's snapshot can't look an entity up).</summary>
        public EntityId NodeId;
        public Vec2 NodePosition;

        /// <summary>What it carried of the haul's item when sent: that isn't the haul.</summary>
        public ushort CarriedAtDispatch;

        public ExpeditionMemberStatus Status;
        private byte _pad;

        public ExpeditionMember(EntityId crawlerId, ushort carriedAtDispatch)
        {
            CrawlerId = crawlerId;
            NodeId = default;
            NodePosition = default;
            CarriedAtDispatch = carriedAtDispatch;
            Status = ExpeditionMemberStatus.Going;
            _pad = 0;
        }

        /// <summary>Still with the party: going, or downed and limping home with it.</summary>
        public readonly bool IsWithParty => Status == ExpeditionMemberStatus.Going || Status == ExpeditionMemberStatus.Downed;
    }

    /// <summary>
    /// An expedition's order and where it is (Epic 10 G5, D7 and D9). Each of the guild's parties holds one
    /// (<see cref="GuildParty"/>, Epic 10 Part 2, W5, D60); the party is a
    /// <see cref="Simpiens.Cognition.Groups.GroupType.TemporaryParty"/> group inside the guild. Saved as raw memory
    /// (format 9); no stage means no expedition.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ExpeditionRecord
    {
        public uint DispatchTick;
        public uint StageTick;
        public byte PartyGroupId;
        public byte TargetSectorId;

        /// <summary>What the target sector's nodes yield: the haul.</summary>
        public ItemType HaulItem;

        /// <summary>How many of it to bring back.</summary>
        public byte Goal;

        public RetreatRule Rule;
        public ExpeditionStage Stage;

        /// <summary>Set when the party turned back (the retreat rule, a downed member, the player's recall, time up).</summary>
        public bool TurnedBack;

        /// <summary>Draughts each member is given to carry at the muster (Epic 10 Part 2, W2, D44): the record's spare byte.</summary>
        public byte Draughts;

        /// <summary>A gathering or a clear (Epic 10 Part 3, K7.1), and a clear's lair, its index in the map's lairs (inside format 11: 16 to 20 bytes).</summary>
        public ExpeditionKind Kind;
        public byte LairIndex;

        /// <summary>A clear that emptied its lair (K7.1, D43): its members are thanked as for a haul, whatever they picked up.</summary>
        public bool Cleared;
        private byte _pad;

        public readonly bool IsClear => Kind == ExpeditionKind.Clear;

        public readonly bool IsActive => Stage != ExpeditionStage.None;
    }
}
