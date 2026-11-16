using Simpiens.Cognition;

namespace Simpiens.Simulation.Skills
{
    /// <summary>
    /// Where practice is recorded (Epic 10 Part 3, K9.2): the fight, the harvest, the stations and the rescue each tell the
    /// simulation what work was practised and from what source (K9.8a), and it raises the skill the table gives that work,
    /// logs a level gained and tells a band reached in the chronicle. Main thread only.
    /// </summary>
    public interface ISkillPractice
    {
        /// <summary>
        /// Adds <paramref name="experience"/> of practice at <paramref name="work"/> to <paramref name="agent"/>, no higher
        /// than <paramref name="source"/>'s ceiling; nothing for no agent, no experience or work no skill answers.
        /// </summary>
        void Practise(AgentState agent, in SkillWork work, int experience, in PracticeSource source);
    }

    /// <summary>What practice comes from (Epic 10 Part 3, K9.8a): doing the work itself, today.</summary>
    public enum PracticeKind : byte
    {
        None = 0,
        /// <summary>Doing the work: a blow landed, a shot, a unit harvested, a make, a rescue.</summary>
        Work = 1
    }

    /// <summary>
    /// Where practice comes from and the highest level it can teach (Epic 10 Part 3, K9.8a). The work itself teaches to the
    /// last level. A source that teaches less (a book to the end of Apprentice, a mentor to below its own level) is a kind
    /// and a ceiling, and the simulation's one entry for practice holds the skill to it.
    /// </summary>
    public readonly struct PracticeSource
    {
        public PracticeSource(PracticeKind kind, int ceiling)
        {
            Kind = kind;
            Ceiling = (byte)(ceiling < 0 ? 0 : ceiling > SkillRules.MaxLevel ? SkillRules.MaxLevel : ceiling);
        }

        public PracticeKind Kind { get; }

        /// <summary>The highest level this practice can bring a skill to; experience past that level's is lost.</summary>
        public byte Ceiling { get; }

        /// <summary>Doing the work itself: it teaches to the last level.</summary>
        public static PracticeSource Work => new PracticeSource(PracticeKind.Work, SkillRules.MaxLevel);

        public override string ToString() => $"{Kind} (to {Ceiling})";
    }
}
