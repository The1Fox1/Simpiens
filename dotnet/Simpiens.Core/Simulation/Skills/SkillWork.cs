using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Simulation.Skills
{
    /// <summary>
    /// A kind of work a skill answers for (Epic 10 Part 3, K9.8a): a fight in reach or from range, a harvest of an item, a
    /// station's makes, tending the fallen. Code names the work it does, never a skill. The table decides which skill
    /// answers (<see cref="SkillTable.SkillFor"/>), <see cref="CrawlerSkills.Level(in SkillSet, Simpiens.Cognition.Inventory.IAgentInventory, in SkillWork)"/>
    /// reads how good a crawler is at the work, and <see cref="ISkillPractice.Practise"/> records practice in it. A family
    /// over several skills, or a skill for one kind of weapon, then changes the table and the seam, never the work's callers.
    /// </summary>
    public readonly struct SkillWork : System.IEquatable<SkillWork>
    {
        /// <summary>The kind of work; <see cref="SkillUse.None"/> for no work, which no skill answers.</summary>
        public readonly SkillUse Use;

        /// <summary>For a harvest: the item harvested.</summary>
        public readonly ItemType Item;

        /// <summary>For making: the station worked.</summary>
        public readonly StationKind Station;

        private SkillWork(SkillUse use, ItemType item = ItemType.None, StationKind station = StationKind.None)
        {
            Use = use;
            Item = item;
            Station = station;
        }

        /// <summary>A fight: from range with a weapon that has one, in reach otherwise.</summary>
        public static SkillWork Fight(bool ranged) => new SkillWork(ranged ? SkillUse.RangedFight : SkillUse.MeleeFight);

        /// <summary>Harvesting <paramref name="item"/> from a node.</summary>
        public static SkillWork Harvest(ItemType item) => new SkillWork(SkillUse.Harvest, item: item);

        /// <summary>Making at a station of <paramref name="station"/>.</summary>
        public static SkillWork Make(StationKind station) => new SkillWork(SkillUse.Make, station: station);

        /// <summary>Tending the fallen: carrying one out of danger, or standing one up with a medicine.</summary>
        public static SkillWork Tend() => new SkillWork(SkillUse.Tend);

        /// <summary>Leading a party out and home (Epic 11, S5.1, D19).</summary>
        public static SkillWork Lead() => new SkillWork(SkillUse.Lead);

        public bool Equals(SkillWork other) => Use == other.Use && Item == other.Item && Station == other.Station;

        public override bool Equals(object obj) => obj is SkillWork other && Equals(other);

        public override int GetHashCode() => (int)Use | ((int)Item << 8) | ((int)Station << 16);

        public static bool operator ==(SkillWork a, SkillWork b) => a.Equals(b);
        public static bool operator !=(SkillWork a, SkillWork b) => !a.Equals(b);

        public override string ToString() => Use switch
        {
            SkillUse.Harvest => $"Harvest {Item}",
            SkillUse.Make => $"Make at {Station}",
            _ => Use.ToString()
        };
    }
}
