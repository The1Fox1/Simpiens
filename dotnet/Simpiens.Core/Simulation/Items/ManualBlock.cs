using Simpiens.Simulation.Skills;

namespace Simpiens.Simulation.Items
{
    /// <summary>
    /// What a manual teaches (Epic 11, S3.2, Q10, D11): the work it is about (the table finds its skill, so the row names a
    /// work, never a skill), the experience a whole reading gives, and the level it teaches to. Read at rest over two game
    /// hours (<c>ReadManualIntent</c>), at practice's source Manual, and used up. Plan 1 ships primers, teaching to 3; a
    /// treatise teaching to 5 is the same block with another ceiling.
    /// </summary>
    public sealed class ManualBlock
    {
        public ManualBlock(SkillWork work, int experience, int ceiling)
        {
            Work = work;
            Experience = experience;
            Ceiling = ceiling;
        }

        /// <summary>The work it teaches: its skill is <see cref="SkillTable.SkillFor"/>'s.</summary>
        public SkillWork Work { get; }

        /// <summary>The experience a whole reading gives, before the reader's passion.</summary>
        public int Experience { get; }

        /// <summary>The highest level it teaches to.</summary>
        public int Ceiling { get; }

        /// <summary>A primer: 300 experience, to the top of Novice (3: the first Apprentice level).</summary>
        public static ManualBlock Primer(SkillWork work) => new ManualBlock(work, PrimerExperience, PrimerCeiling);

        public const int PrimerExperience = 300;
        public const int PrimerCeiling = 3;
    }
}
