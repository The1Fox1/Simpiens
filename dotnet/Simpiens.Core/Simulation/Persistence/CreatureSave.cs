using System.Runtime.InteropServices;
using MemoryPack;
using Simpiens.Simulation.Creatures;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// The creatures in a save (Epic 10 Part 3, K2.4), inside the world's block, written and read by
    /// <see cref="CreatureService"/>: the id sequence's counter, which lairs are charted, each lair's pack and who holds
    /// each of its places (or when a slain member returns), and every creature, sorted by id. Not saved: a creature's path
    /// (planned again after a load) and when it last swung (the view's strike pose).
    /// </summary>
    [MemoryPackable]
    public sealed partial class CreatureSave
    {
        /// <summary>Creature ids handed out since the session started: with the seed, the sequence continues from it.</summary>
        public long IdCounter { get; set; }

        /// <summary>Bit i: lair i (in the map's order) is charted (K2.3, D17).</summary>
        public ulong ChartedLairs { get; set; }

        /// <summary>Each lair's pack when full, in the map's order.</summary>
        public byte[] Packs { get; set; }

        /// <summary>Every lair's places, lair by lair in the map's order, then slot by slot.</summary>
        public LairSlotRecord[] Slots { get; set; }

        /// <summary>Every creature in the world, sorted by id.</summary>
        public CreatureRecord[] Creatures { get; set; }
    }

    /// <summary>One place in a lair's pack: who holds it (empty while it is empty), and when a slain member returns (0 for none).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LairSlotRecord
    {
        public EntityId Member;
        public uint ReturnAt;
        public byte Lair;
        public byte Slot;
        private ushort _pad;

        public LairSlotRecord(byte lair, byte slot, EntityId member, uint returnAt)
        {
            Member = member;
            ReturnAt = returnAt;
            Lair = lair;
            Slot = slot;
            _pad = 0;
        }
    }

    /// <summary>
    /// A creature as saved: who it is, of what kind, its lair and place; where it is and its health; what it is doing (its
    /// mode, its quarry, its goal, until when it rests) and when it may swing again (the combat rule's D8: saved, so a save
    /// mid-fight loads byte for byte, K11.1), the last tick a crawler stood in its lair (K10.3c), and its name (Epic 11, S7.1,
    /// D31: the crawler whose downing named it, its rank, the crawlers it downed) and its foe (D33). 112 bytes since format 13
    /// (80 before), with no implicit padding: saved as raw memory.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CreatureRecord
    {
        public EntityId Id;
        public EntityId Target;
        public long NextSwingAt;
        public long RestUntil;
        public Vec2 Position;
        public Vec2 Goal;
        public float Health;
        public CreatureKind Kind;
        public CreatureMode Mode;
        public byte Lair;
        public byte Slot;
        /// <summary>The last tick a living crawler stood in its lair's reach (Epic 10 Part 3, K10.3c); 0 for never.</summary>
        public uint LastVisitAt;
        /// <summary>Its rank (Epic 11, S7.1, D31): 0 unnamed.</summary>
        public byte Rank;
        /// <summary>The crawlers it has downed since it was named (D31).</summary>
        public byte Downed;
        private ushort _pad;
        /// <summary>The crawler whose downing named it (D31); empty for none.</summary>
        public EntityId Victim;
        /// <summary>The last crawler that struck it and lived (Epic 11, S7.3, D33); empty for none.</summary>
        public EntityId Foe;

        public CreatureRecord(CreatureState creature)
        {
            Id = creature.Id;
            Target = creature.Target;
            NextSwingAt = creature.NextSwingAt;
            RestUntil = creature.RestUntil;
            Position = creature.Position;
            Goal = creature.Goal;
            Health = creature.Health;
            Kind = creature.Kind;
            Mode = creature.Mode;
            Lair = (byte)creature.LairIndex;
            Slot = creature.Slot;
            LastVisitAt = creature.LastVisitAt;
            Rank = creature.Rank;
            Downed = creature.Downed;
            _pad = 0;
            Victim = creature.Victim;
            Foe = creature.Foe;
        }
    }
}
