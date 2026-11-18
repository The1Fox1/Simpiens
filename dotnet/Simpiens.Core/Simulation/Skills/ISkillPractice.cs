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

    /// <summary>What practice comes from (Epic 10 Part 3, K9.8a): doing the work itself, and teachers (Epic 11, S3).</summary>
    public enum PracticeKind : byte
    {
        None = 0,
        /// <summary>Doing the work: a blow landed, a shot, a unit harvested, a make, a rescue.</summary>
        Work = 1,
        /// <summary>A better hand at the same work nearby (Epic 11, S3.1, D9): half the act, to the mentor's level less two.</summary>
        Mentor = 2,
        /// <summary>A manual read (Epic 11, S3.3, D11): to its ceiling.</summary>
        Manual = 3,
        /// <summary>The drill yard (Epic 11, S3.4, D10): to 5.</summary>
        Yard = 4
    }

    /// <summary>
    /// Where practice comes from, the highest level it can teach (Epic 10 Part 3, K9.8a) and the level the act demands (Epic
    /// 11, S1.3, Q8). The work itself teaches to the last level. A source that teaches less (a book to the end of Apprentice,
    /// a mentor to below its own level) is a kind and a ceiling, and the simulation's one entry for practice holds the skill
    /// to it. A crawler whose level stands above the act's challenge learns less from it (<see cref="SkillRules.ChallengePercent"/>).
    /// </summary>
    public readonly struct PracticeSource
    {
        public PracticeSource(PracticeKind kind, int ceiling, int challenge = SkillRules.MaxLevel)
        {
            Kind = kind;
            Ceiling = Clamp(ceiling);
            Challenge = Clamp(challenge);
        }

        public PracticeKind Kind { get; }

        /// <summary>The highest level this practice can bring a skill to; experience past that level's is lost.</summary>
        public byte Ceiling { get; }

        /// <summary>The level the act demands (Q8): a rat a Novice's, a beetle a Journeyman's; full practice at or above it.</summary>
        public byte Challenge { get; }

        /// <summary>Doing the work itself, at the highest challenge: it teaches to the last level, fully.</summary>
        public static PracticeSource Work => new PracticeSource(PracticeKind.Work, SkillRules.MaxLevel);

        /// <summary>A mentor's lesson (Epic 11, S3.1, D9): to <paramref name="ceiling"/>, the mentor's level less two, in full whatever the act demanded.</summary>
        public static PracticeSource Mentor(int ceiling) => new PracticeSource(PracticeKind.Mentor, ceiling);

        /// <summary>Doing the work itself on something that demands <paramref name="challenge"/> (Epic 11, S1.3): a creature, a node, a recipe.</summary>
        public static PracticeSource WorkAt(int challenge) => new PracticeSource(PracticeKind.Work, SkillRules.MaxLevel, challenge);

        private static byte Clamp(int level) => (byte)(level < 0 ? 0 : level > SkillRules.MaxLevel ? SkillRules.MaxLevel : level);

        public override string ToString() => $"{Kind} (to {Ceiling}, challenge {Challenge})";
    }
}
