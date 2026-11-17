using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Simpiens.Simulation.Skills
{
    /// <summary>How much a crawler loves a skill's work (Epic 11, Q7, D3): its row in the passions' table. Ids are saved; never reuse one.</summary>
    public enum Passion : byte
    {
        None = 0,
        Keen = 1,
        Burning = 2
    }

    /// <summary>A passion for each of <see cref="SkillTable.Capacity"/> skills, by skill id.</summary>
    [InlineArray(SkillTable.Capacity)]
    public struct PassionBytes
    {
        private byte _first;
    }

    /// <summary>A technique id for each band above Novice of each skill (plan 2's), by skill id × 3 + band − 1.</summary>
    [InlineArray(CrawlerTraits.TechniqueSlots)]
    public struct TechniqueBytes
    {
        private byte _first;
    }

    /// <summary>Who first taught a crawler a skill (Epic 11, S3, D9): the skill and the teacher, 20 bytes.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TeacherRecord
    {
        public EntityId Teacher;
        public Skill Skill;
        private byte _pad0;
        private ushort _pad1;
    }

    /// <summary>The first teachers of up to <see cref="CrawlerTraits.TeacherSlots"/> skills.</summary>
    [InlineArray(CrawlerTraits.TeacherSlots)]
    public struct TeacherRecords
    {
        private TeacherRecord _first;
    }

    /// <summary>A count for each of <see cref="DeedTable.Capacity"/> deeds, by deed id.</summary>
    [InlineArray(DeedTable.Capacity)]
    public struct DeedCounts
    {
        private ushort _first;
    }

    /// <summary>Four one-byte ids: earned traits, or scars (plan 3's).</summary>
    [InlineArray(4)]
    public struct FourBytes
    {
        private byte _first;
    }

    /// <summary>The record's unused room, so later fields fit without changing its size.</summary>
    [InlineArray(CrawlerTraits.SpareBytes)]
    public struct TraitsSpare
    {
        private byte _first;
    }

    /// <summary>
    /// Who a crawler has become (Epic 11, A3, D1): beside its <see cref="SkillSet"/>, a fixed record of 256 bytes, saved whole
    /// in its record and copied for cognition, as its skills are. Plain data, written on the main thread.
    /// <list type="bullet">
    /// <item><b>Passions</b> (S1): one for each skill id.</item>
    /// <item><b>Techniques</b> (plan 2): one id for each band above Novice of each skill, reserved.</item>
    /// <item><b>Teachers</b> (S3): who first taught each of up to four skills.</item>
    /// <item><b>Deeds</b> (S0.5): a count for each deed id, where the deed happens (A7).</item>
    /// <item><b>A calling, an epithet, earned traits and scars</b> (plans 2 and 3), reserved.</item>
    /// </list>
    /// The room left over (<see cref="SpareBytes"/>) takes later fields without changing its size or the save's format. Its
    /// size is pinned (<c>EconomySerializationValidator</c>).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CrawlerTraits : IEquatable<CrawlerTraits>
    {
        /// <summary>The record's size in bytes: a 17th skill, a 5th teacher or a 17th deed changes the save format.</summary>
        public const int Size = 256;

        /// <summary>Three bands above Novice for each of 16 skills.</summary>
        public const int TechniqueSlots = SkillTable.Capacity * 3;

        /// <summary>A crawler keeps the first teachers of this many skills: an id is 16 bytes, and few crawlers are taught more.</summary>
        public const int TeacherSlots = 4;

        /// <summary>What's left of <see cref="Size"/> after the fields.</summary>
        public const int SpareBytes = 68;

        private PassionBytes _passions;     //   0: 16
        private TechniqueBytes _techniques; //  16: 48
        private TeacherRecords _teachers;   //  64: 80
        private DeedCounts _deeds;          // 144: 32
        private byte _calling;              // 176
        private byte _reserved;             // 177
        private ushort _epithet;            // 178
        private FourBytes _earnedTraits;    // 180
        private FourBytes _scars;           // 184
        private TraitsSpare _spare;         // 188: 68

        // --- Passions (S1) ---

        public readonly Passion PassionFor(Skill skill) => (byte)skill < SkillTable.Capacity ? (Passion)_passions[(byte)skill] : Passion.None;

        public void SetPassion(Skill skill, Passion passion)
        {
            if ((byte)skill == 0 || (byte)skill >= SkillTable.Capacity) return;
            _passions[(byte)skill] = (byte)passion;
        }

        // --- Teachers (S3) ---

        /// <summary>Who first taught <paramref name="skill"/>, or no one.</summary>
        public readonly EntityId TeacherOf(Skill skill)
        {
            if (skill == Skill.None) return default;
            for (int i = 0; i < TeacherSlots; i++) if (_teachers[i].Skill == skill) return _teachers[i].Teacher;
            return default;
        }

        /// <summary>The teacher record in slot <paramref name="index"/>, for the save's checks and the dossier.</summary>
        public readonly TeacherRecord TeacherAt(int index) => (uint)index < TeacherSlots ? _teachers[index] : default;

        /// <summary>
        /// Records <paramref name="teacher"/> as the first to teach <paramref name="skill"/>, in the first free slot; false when
        /// the skill already has its first teacher, all four slots are taken, or there is no skill or teacher.
        /// </summary>
        public bool RecordTeacher(Skill skill, EntityId teacher)
        {
            if (skill == Skill.None || (byte)skill >= SkillTable.Capacity || teacher == default || TeacherOf(skill) != default) return false;
            for (int i = 0; i < TeacherSlots; i++)
            {
                if (_teachers[i].Skill != Skill.None) continue;
                _teachers[i] = new TeacherRecord { Teacher = teacher, Skill = skill };
                return true;
            }
            return false;
        }

        // --- Deeds (S0.5) ---

        public readonly int Count(Deed deed) => (byte)deed < DeedTable.Capacity ? _deeds[(byte)deed] : 0;

        /// <summary>Counts one <paramref name="deed"/>, up to the counter's most.</summary>
        public void Add(Deed deed)
        {
            if ((byte)deed == 0 || (byte)deed >= DeedTable.Capacity || _deeds[(byte)deed] == ushort.MaxValue) return;
            _deeds[(byte)deed]++;
        }

        // --- The whole record ---

        /// <summary>The record as bytes, for the hash and equality.</summary>
        public static ReadOnlySpan<byte> BytesOf(in CrawlerTraits traits) => MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in traits, 1));

        /// <summary>True for a record nothing has been written into.</summary>
        public readonly bool IsEmpty => BytesOf(this).IndexOfAnyExcept((byte)0) < 0;

        public readonly bool Equals(CrawlerTraits other) => BytesOf(this).SequenceEqual(BytesOf(other));

        public override readonly bool Equals(object obj) => obj is CrawlerTraits other && Equals(other);

        public override readonly int GetHashCode()
        {
            var hash = new HashCode();
            hash.AddBytes(BytesOf(this));
            return hash.ToHashCode();
        }

        public static bool operator ==(CrawlerTraits a, CrawlerTraits b) => a.Equals(b);
        public static bool operator !=(CrawlerTraits a, CrawlerTraits b) => !a.Equals(b);
    }
}
