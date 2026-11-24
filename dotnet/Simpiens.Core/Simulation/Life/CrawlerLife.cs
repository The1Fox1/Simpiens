using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Simpiens.Simulation.Life
{
    /// <summary>
    /// One thought held (Epic LIFE-1, D6, D7): which thought, how many times over, whom it names (a slot of the record's
    /// subjects, 0 for none) and the tick it began. 8 bytes; an empty slot is all zeros.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ThoughtSlot
    {
        public ushort Thought;
        public byte Stacks;
        public byte Subject;
        public uint BegunAt;
    }

    [InlineArray(CrawlerLife.ThoughtCapacity)]
    public struct ThoughtSlots
    {
        private ThoughtSlot _first;
    }

    [InlineArray(CrawlerLife.SubjectCapacity)]
    public struct ThoughtSubjects
    {
        private EntityId _first;
    }

    /// <summary>The record's room for plans 2 and 3 and for LIFE-2 and LIFE-3 (D6's table): zeros until a field is laid in it.</summary>
    [InlineArray(CrawlerLife.SpareBytes)]
    public struct LifeSpare
    {
        private byte _first;
    }

    /// <summary>
    /// Who a crawler is living as (Epic LIFE-1, L0.8, D6; the epic's A3): a fixed record of <see cref="Size"/> bytes in the
    /// crawler's record after its traits, saved whole (save format 15, the life core's one raise) and copied for cognition
    /// (<c>AgentContext.Life</c>). It lives here, not in <c>People/</c>, because cognition reads it.
    /// <list type="bullet">
    /// <item><b>Thoughts</b> (L1.2): 16 slots of 8 bytes, at 0.</item>
    /// <item><b>Subjects</b> (L1.2): the four crawlers the thoughts name, an <see cref="EntityId"/> each, at 128. An id is 16
    /// bytes, so a thought names a subject by its slot here, and a slot frees when no thought names it.</item>
    /// <item><b>Spare</b>, at 192: laid out for wants (3 of 24 bytes, at 192), fun, weariness, the last windfall, the day's
    /// intention and curiosity (16, at 264) and variety (8, at 280) in plans 2 and 3; then taste and keepsakes (LIFE-2) and an
    /// aspiration (LIFE-3), with about 100 bytes left. A field laid here later reads 0 as "none", so a save made before it
    /// loads unchanged.</item>
    /// </list>
    /// Its size is pinned (<c>EconomySerializationValidator</c>).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CrawlerLife : IEquatable<CrawlerLife>
    {
        /// <summary>The record's size in bytes: a 17th thought slot or a 5th subject changes the save format.</summary>
        public const int Size = 512;

        public const int ThoughtCapacity = 16;
        public const int SubjectCapacity = 4;

        /// <summary>What's left of <see cref="Size"/> after the thoughts and their subjects.</summary>
        public const int SpareBytes = Size - ThoughtCapacity * 8 - SubjectCapacity * 16;

        private ThoughtSlots _thoughts;    //   0: 128
        private ThoughtSubjects _subjects; // 128: 64
        private LifeSpare _spare;          // 192: 320

        // --- Thoughts (L1.2) ---

        /// <summary>The thought slot at <paramref name="index"/> (0 to 15); an empty one for an index out of range.</summary>
        public readonly ThoughtSlot ThoughtAt(int index) => (uint)index < ThoughtCapacity ? _thoughts[index] : default;

        /// <summary>Puts <paramref name="slot"/> at <paramref name="index"/>; nothing for an index out of range.</summary>
        public void SetThought(int index, in ThoughtSlot slot)
        {
            if ((uint)index < ThoughtCapacity) _thoughts[index] = slot;
        }

        /// <summary>The crawler subject slot <paramref name="slot"/> (1 to 4) names; default for 0 or out of range.</summary>
        public readonly EntityId SubjectAt(int slot) => slot >= 1 && slot <= SubjectCapacity ? _subjects[slot - 1] : default;

        /// <summary>Names <paramref name="id"/> in subject slot <paramref name="slot"/> (1 to 4); nothing for 0 or out of range.</summary>
        public void SetSubject(int slot, EntityId id)
        {
            if (slot >= 1 && slot <= SubjectCapacity) _subjects[slot - 1] = id;
        }

        // --- The record as bytes ---

        public static ReadOnlySpan<byte> BytesOf(in CrawlerLife life) => MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in life, 1));

        /// <summary>True while nothing has been written: the world hash mixes the record only once it isn't.</summary>
        public readonly bool IsEmpty => BytesOf(this).IndexOfAnyExcept((byte)0) < 0;

        public readonly bool Equals(CrawlerLife other) => BytesOf(this).SequenceEqual(BytesOf(other));

        public override readonly bool Equals(object obj) => obj is CrawlerLife other && Equals(other);

        public override readonly int GetHashCode()
        {
            var hash = new HashCode();
            hash.AddBytes(BytesOf(this));
            return hash.ToHashCode();
        }

        public static bool operator ==(CrawlerLife a, CrawlerLife b) => a.Equals(b);

        public static bool operator !=(CrawlerLife a, CrawlerLife b) => !a.Equals(b);
    }
}
