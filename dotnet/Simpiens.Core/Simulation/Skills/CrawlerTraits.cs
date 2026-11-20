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

    /// <summary>What the record has changed that the crawler's runtime has yet to tell the chronicle (Epic 11, S6.1): told once, on the main thread.</summary>
    [System.Flags]
    public enum LegendNews : byte
    {
        None = 0,
        Epithet = 1,
        Trait = 2,
        Scar = 4
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
    /// <item><b>A calling</b> (plan 2), <b>an epithet</b> with the kind or crawler it names (plan 3, S6.1), earned traits and scars (plan 3), and what is yet to be told.</item>
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
        public const int SpareBytes = 51;

        private PassionBytes _passions;     //   0: 16
        private TechniqueBytes _techniques; //  16: 48
        private TeacherRecords _teachers;   //  64: 80
        private DeedCounts _deeds;          // 144: 32
        private byte _calling;              // 176
        private byte _news;                 // 177: what is yet to be told (S6.1)
        private ushort _epithet;            // 178
        private FourBytes _earnedTraits;    // 180
        private FourBytes _scars;           // 184
        private EntityId _epithetSubject;   // 188: 16, the crawler an epithet names (S6.1)
        private byte _epithetKind;          // 204: the creature kind an epithet names (S6.1)
        private TraitsSpare _spare;         // 205: 51

        // --- Passions (S1) ---

        public readonly Passion PassionFor(Skill skill) => (byte)skill < SkillTable.Capacity ? (Passion)_passions[(byte)skill] : Passion.None;

        public void SetPassion(Skill skill, Passion passion)
        {
            if ((byte)skill == 0 || (byte)skill >= SkillTable.Capacity) return;
            _passions[(byte)skill] = (byte)passion;
        }

        // --- Techniques (Epic 11, S4, D13) ---

        /// <summary>The technique the crawler holds at <paramref name="band"/> of <paramref name="skill"/>; none for a slot not yet picked, Novice or no skill.</summary>
        public readonly Technique TechniqueIn(Skill skill, SkillBand band)
        {
            int slot = TechniqueSlot(skill, band);
            return slot < 0 ? Technique.None : (Technique)_techniques[slot];
        }

        /// <summary>Holds <paramref name="technique"/> at <paramref name="band"/> of <paramref name="skill"/> (none clears it); nothing for Novice or no skill.</summary>
        public void SetTechnique(Skill skill, SkillBand band, Technique technique)
        {
            int slot = TechniqueSlot(skill, band);
            if (slot >= 0) _techniques[slot] = (byte)technique;
        }

        // A band's slot: skill id × 3 + band − 1, for the three bands above Novice
        private static int TechniqueSlot(Skill skill, SkillBand band)
        {
            if (skill == Skill.None || (byte)skill >= SkillTable.Capacity || band == SkillBand.Novice || band > SkillBand.Master) return -1;
            return (byte)skill * 3 + (byte)band - 1;
        }

        // --- The calling (Epic 11, S5.3, D21) ---

        /// <summary>The calling the crawler has earned (<see cref="CallingRules.Earned"/>); none before it stands at Apprentice in anything.</summary>
        public readonly Calling Calling => (Calling)_calling;

        public void SetCalling(Calling calling) => _calling = (byte)calling;

        // --- The epithet (Epic 11, S6.1, D27) ---

        /// <summary>The epithet it holds (the highest rank earned, <see cref="EpithetRules.Judge"/>); none until it earns one.</summary>
        public readonly Epithet Epithet => (Epithet)_epithet;

        /// <summary>The creature kind its epithet names ("Beetlebane": the kind of the kill that earned it); 0 for none.</summary>
        public readonly byte EpithetKind => _epithetKind;

        /// <summary>The crawler its epithet names ("Ama's Avenger"); none for an epithet that names nobody.</summary>
        public readonly EntityId EpithetSubject => _epithetSubject;

        /// <summary>Holds <paramref name="epithet"/>, naming <paramref name="kind"/> and <paramref name="subject"/> where its form does.</summary>
        public void SetEpithet(Epithet epithet, byte kind, EntityId subject)
        {
            _epithet = (ushort)epithet;
            _epithetKind = kind;
            _epithetSubject = subject;
        }

        // --- Scars (Epic 11, S6.2, D28) ---

        /// <summary>A crawler bears at most this many scars: four places on the face.</summary>
        public const int ScarSlots = 4;

        /// <summary>The scar in <paramref name="slot"/> (its place on the face, <see cref="ScarPlace"/>); none for a slot not filled.</summary>
        public readonly Scar ScarAt(int slot) => (uint)slot < ScarSlots ? (Scar)_scars[slot] : Scar.None;

        /// <summary>How many scars it bears.</summary>
        public readonly int ScarCount
        {
            get
            {
                int n = 0;
                while (n < ScarSlots && _scars[n] != 0) n++;
                return n;
            }
        }

        /// <summary>Takes <paramref name="scar"/> in the next free place, with news to tell; false for none or with every place taken.</summary>
        public bool AddScar(Scar scar)
        {
            int slot = ScarCount;
            if (scar == Scar.None || slot >= ScarSlots) return false;
            _scars[slot] = (byte)scar;
            _news |= (byte)LegendNews.Scar;
            return true;
        }

        // --- What is yet to be told (Epic 11, S6.1) ---

        /// <summary>What the record has changed that the chronicle hasn't been told: the crawler's runtime tells it and clears it.</summary>
        public readonly LegendNews News => (LegendNews)_news;

        public void AddNews(LegendNews news) => _news |= (byte)news;

        public void ClearNews(LegendNews news) => _news &= (byte)~news;

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
