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

    /// <summary>
    /// One want held (Epic LIFE-1 plan 2, W.2, D20): which want, the activity and the venue (its index plus one) it is for, the
    /// tick it runs out at, and the friend it names. 24 bytes; an empty slot is all zeros.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WantSlot
    {
        public ushort Want;
        public byte Activity;
        public byte Venue;
        public uint ExpiresAt;
        public EntityId Friend;
    }

    [InlineArray(CrawlerLife.WantCapacity)]
    public struct WantSlots
    {
        private WantSlot _first;
    }

    /// <summary>Variety (plan 2's L2.7, D15): a byte a variety group, how much of it a crawler has had of late.</summary>
    [InlineArray(CrawlerLife.VarietyGroups)]
    public struct LifeVariety
    {
        private byte _first;
    }

    /// <summary>The record's room for LIFE-2 and LIFE-3 (D6's table), at 296: zeros until a field is laid in it.</summary>
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
    /// <item><b>Wants</b> (plan 2's W.2): three slots of 24 bytes, at 192, reserved until then.</item>
    /// <item><b>Boredom</b> (plan 2's L2.1, D11): a float at 264, 0 to 100; fun is 100 less it. Held this way round so a record
    /// written before fun reads entertained.</item>
    /// <item><b>Rhythm</b> (plan 3): weariness, a float at 268, and the last windfall's tick at 272 (R.5, D33); the day's
    /// intention at 276.</item>
    /// <item><b>Curiosity</b> (plan 2's L2.7, Q8): a sixth trait at 277, drawn at spawn (0 unset, read as neutral); the venues
    /// a crawler has tried at 278, a bit a venue.</item>
    /// <item><b>Chronotype</b> (plan 3's R.1, D28): a lark, an average sleeper or an owl, at 279, drawn at spawn (0 reads as
    /// average, so a record written before it keeps today's hours).</item>
    /// <item><b>Variety</b> (L2.7, D15): eight bytes at 280, a variety group each.</item>
    /// <item><b>The intention's day</b> (plan 3's R.7, D34): a <c>ushort</c> at 288, the routine day the intention at 276 was drawn
    /// for, plus one (0: never drawn, read as a work day).</item>
    /// <item><b>When it woke</b> (Epic PLACES plan 1, G.3): a <c>uint</c> at 292, the tick the intention was drawn, when the crawler
    /// was first up outside its night (0: unknown, a record before it; the day log falls back to the waking hour). 290 and 291
    /// are padding.</item>
    /// <item><b>Spare</b>, at 296: taste and keepsakes (LIFE-2) and an aspiration (LIFE-3), with about 100 bytes left. A field
    /// laid here later reads 0 as "none", so a save made before it loads unchanged.</item>
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

        /// <summary>The wants: three slots of 24 bytes (D6).</summary>
        public const int WantCapacity = 3;
        public const int WantBytes = WantCapacity * 24;

        /// <summary>Variety's groups: a byte each (<c>VarietyGroup</c>).</summary>
        public const int VarietyGroups = 8;

        /// <summary>What's left of <see cref="Size"/> after the thoughts, their subjects, the wants, boredom, the rhythm's room,
        /// the intention, curiosity, the tried venues, the chronotype, variety, the intention's day, its padding and when it woke.</summary>
        public const int SpareBytes = Size - ThoughtCapacity * 8 - SubjectCapacity * 16 - WantBytes - sizeof(float) - sizeof(float) - sizeof(uint) - 4 - VarietyGroups - sizeof(ushort) - sizeof(ushort) - sizeof(uint);

        /// <summary>The most weariness holds (D33).</summary>
        public const float MaxWeariness = 100f;

        /// <summary>The most boredom holds: fun at 0.</summary>
        public const float MaxBoredom = 100f;

        private ThoughtSlots _thoughts;    //   0: 128
        private ThoughtSubjects _subjects; // 128: 64
        private WantSlots _wants;          // 192: 72
        private float _boredom;            // 264: 4
        private float _weariness;          // 268: 4 (plan 3's R.5)
        private uint _lastWindfall;        // 272: 4 (plan 3's R.5)
        private byte _intention;           // 276 (plan 3's R.7)
        private byte _curiosity;           // 277
        private byte _venuesTried;         // 278
        private byte _chronotype;          // 279 (plan 3's R.1)
        private LifeVariety _variety;      // 280: 8
        private ushort _intentionDay;      // 288: 2 (plan 3's R.7)
        private ushort _padding290;        // 290: 2
        private uint _wokeAt;              // 292: 4 (Epic PLACES plan 1, G.3)
        private LifeSpare _spare;          // 296: 216

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

        // --- Wants (plan 2's W.2, D20) ---

        /// <summary>The want slot at <paramref name="index"/> (0 to 2); an empty one out of range.</summary>
        public readonly WantSlot WantAt(int index) => (uint)index < WantCapacity ? _wants[index] : default;

        public void SetWant(int index, in WantSlot slot)
        {
            if ((uint)index < WantCapacity) _wants[index] = slot;
        }

        // --- Fun (plan 2's L2.1, D11) ---

        /// <summary>How bored it is, 0 to 100: rises awake, falls with what entertains it.</summary>
        public readonly float Boredom => _boredom;

        /// <summary>Its fun, 0 to 100: 100 less its boredom, so a record written before fun reads entertained.</summary>
        public readonly float Fun => MaxBoredom - _boredom;

        /// <summary>Adds <paramref name="amount"/> to its boredom (a negative amount entertains it), held within 0 and 100.</summary>
        public void Bore(float amount) => _boredom = Math.Clamp((float)(_boredom + amount), 0f, MaxBoredom);

        // --- Curiosity and variety (plan 2's L2.7, D15, Q8) ---

        /// <summary>Its curiosity, -1 to 1: 0 for a record with none drawn (0 is unset, read as neutral).</summary>
        public readonly float Curiosity => _curiosity == 0 ? 0f : (_curiosity - 128) / 127f;

        /// <summary>Sets its curiosity, -1 to 1, as a byte from 1 to 255.</summary>
        public void SetCuriosity(float curiosity) => _curiosity = (byte)(128 + (int)MathF.Round(Math.Clamp(curiosity, -1f, 1f) * 127f));

        /// <summary>Whether it has tried the map's venue <paramref name="venue"/> (0 to 7; venues past 7 count as tried).</summary>
        public readonly bool HasTried(int venue) => venue >= 8 || venue < 0 || (_venuesTried & (1 << venue)) != 0;

        public void MarkTried(int venue)
        {
            if (venue >= 0 && venue < 8) _venuesTried |= (byte)(1 << venue);
        }

        /// <summary>How much of variety group <paramref name="group"/> it has had of late, 0 to 255.</summary>
        public readonly byte HadOf(int group) => (uint)group < VarietyGroups ? _variety[group] : (byte)0;

        /// <summary>A spell of group <paramref name="group"/> done: <paramref name="amount"/> more of it, at most 255.</summary>
        public void Tire(int group, int amount)
        {
            if ((uint)group < VarietyGroups) _variety[group] = (byte)Math.Min(255, _variety[group] + amount);
        }

        /// <summary>A day passed (it woke): every group eased by <paramref name="amount"/>.</summary>
        public void EaseVariety(int amount)
        {
            for (int g = 0; g < VarietyGroups; g++) _variety[g] = (byte)Math.Max(0, _variety[g] - amount);
        }

        // --- Rhythm (plan 3) ---

        /// <summary>Its chronotype (plan 3's R.1, D28): when its day starts. 0, a record with none drawn, reads as average.</summary>
        public readonly Chronotype Chronotype => (Chronotype)_chronotype;

        public void SetChronotype(Chronotype chronotype) => _chronotype = (byte)chronotype;

        /// <summary>How weary it is of work, 0 to 100 (plan 3's R.5, D33): it rises at work and only a day off clears it.</summary>
        public readonly float Weariness => _weariness;

        /// <summary>Adds <paramref name="amount"/> to its weariness (negative eases it), held within 0 and 100.</summary>
        public void Weary(float amount) => _weariness = Math.Clamp((float)(_weariness + amount), 0f, MaxWeariness);

        /// <summary>The tick of its last windfall, a single payment of half the comfortable purse or more (D33); 0 for none.</summary>
        public readonly uint LastWindfall => _lastWindfall;

        public void RecordWindfall(uint tick) => _lastWindfall = tick == 0 ? 1u : tick;

        /// <summary>The day's intention as drawn (plan 3's R.7, D34), whatever day it was drawn for; a work day in a record without one.</summary>
        public readonly DayIntention Intention => (DayIntention)_intention;

        /// <summary>The routine day the intention was drawn for, plus one; 0 for never.</summary>
        public readonly ushort IntentionDay => _intentionDay;

        /// <summary>The intention for routine day <paramref name="day"/> (0 for day 1): the one drawn for it, or a work day.</summary>
        public readonly DayIntention IntentionFor(long day) => day >= 0 && _intentionDay == day + 1 ? (DayIntention)_intention : DayIntention.Work;

        /// <summary>Draws <paramref name="intention"/> for routine day <paramref name="day"/> (0 for day 1).</summary>
        public void SetIntention(DayIntention intention, long day)
        {
            _intention = (byte)intention;
            _intentionDay = (ushort)Math.Clamp(day + 1, 0, ushort.MaxValue);
        }

        /// <summary>The tick the last intention was drawn: when the crawler got up that day (0: unknown).</summary>
        public readonly uint WokeAt => _wokeAt;

        /// <summary>When the crawler got up on routine day <paramref name="day"/>: the tick its intention was drawn, or 0 when that
        /// day's wasn't drawn or a record before G.3 doesn't know.</summary>
        public readonly uint WokeOn(long day) => day >= 0 && _intentionDay == day + 1 ? _wokeAt : 0u;

        /// <summary>Keeps <paramref name="tick"/> as when the crawler got up (Epic PLACES plan 1, G.3).</summary>
        public void Woke(uint tick) => _wokeAt = tick;

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
