using Simpiens.Cognition;

namespace Simpiens.Simulation.Skills
{
    /// <summary>
    /// What a crawler's techniques make of its work (Epic 11, S4.3, A1, A4, A5, D14): the one door through which every effect of
    /// a technique is read, beside <see cref="CrawlerSkills"/> (how good it is) and <see cref="Simpiens.Simulation.Items.CrawlerStats"/>
    /// (what its equipment makes of it). A pure function of the traits record and the skills, both of which cognition's copy
    /// holds too, so the main thread and cognition read the same numbers.
    /// <para>
    /// A technique changes only its own skill's work: Shield Wall's defense counts while its holder fights in reach, not
    /// while it shoots or mines. Every stat is summed over the techniques the crawler holds in the work's skill and held within
    /// its bounds (<see cref="TechniqueRules.Bounds"/>), so no combination runs away. Code asks for a stat, never a technique by
    /// name (<c>TechniqueNamesTests</c>), and reads techniques nowhere else (<c>TechniqueReadsTests</c>).
    /// </para>
    /// </summary>
    public static class CrawlerTechniques
    {
        /// <summary>
        /// <paramref name="stat"/> for a crawler with <paramref name="traits"/> and <paramref name="skills"/> at
        /// <paramref name="work"/>: the sum over the techniques it holds in the work's skill, within the stat's bounds; 0 for
        /// work no skill answers.
        /// </summary>
        public static int Get(in CrawlerTraits traits, in SkillSet skills, in SkillWork work, TechniqueStat stat) => Get(traits, skills, SkillTable.SkillFor(work), stat);

        /// <summary>As <see cref="Get(in CrawlerTraits, in SkillSet, in SkillWork, TechniqueStat)"/>, for a crawler's state on the main thread; 0 for none.</summary>
        public static int Get(AgentState crawler, in SkillWork work, TechniqueStat stat) => crawler == null ? 0 : Get(crawler.Traits, crawler.Skills, work, stat);

        /// <summary><paramref name="stat"/> over the techniques held in <paramref name="skill"/>, within its bounds.</summary>
        public static int Get(in CrawlerTraits traits, in SkillSet skills, Skill skill, TechniqueStat stat)
        {
            if (skill == Skill.None || stat == TechniqueStat.None) return 0;
            int sum = 0;
            for (var band = SkillBand.Apprentice; band <= SkillBand.Master; band++)
            {
                var held = traits.TechniqueIn(skill, band);
                if (held == Technique.None) continue;
                var row = TechniqueTable.Get(held);
                if (row != null && row.Skill == skill) sum += row.ValueOf(stat);
            }
            return TechniqueRules.Clamp(stat, sum);
        }

        /// <summary>
        /// The techniques <paramref name="traits"/> holds, into <paramref name="into"/>, in the skills' display order and by
        /// band within a skill (the dossier's list); returns how many, as many as the span holds.
        /// </summary>
        public static int Held(in CrawlerTraits traits, System.Span<Technique> into)
        {
            int count = 0;
            var shown = SkillTable.InDisplayOrder;
            for (int i = 0; i < shown.Count && count < into.Length; i++)
                for (var band = SkillBand.Apprentice; band <= SkillBand.Master && count < into.Length; band++)
                {
                    var held = traits.TechniqueIn(shown[i].Id, band);
                    if (held != Technique.None) into[count++] = held;
                }
            return count;
        }

        /// <summary>A percentage stat as a factor: 1 + <paramref name="percent"/> / 100, exactly 1 for none.</summary>
        public static float Factor(int percent) => percent == 0 ? 1f : (float)(1f + (float)(percent / 100f));
    }
}
