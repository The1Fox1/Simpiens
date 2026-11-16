using Simpiens.Cognition;
using Simpiens.Cognition.Inventory;

namespace Simpiens.Simulation.Skills
{
    /// <summary>
    /// How good a crawler is at a kind of work (Epic 10 Part 3, K9.8a): its effective level, the one number every effect and
    /// choice reads, as <see cref="Simpiens.Simulation.Items.CrawlerStats"/> is for what its equipment makes of it. A pure
    /// function of the crawler's skills and what it wears, both of which cognition's copy holds, so the main thread and
    /// cognition read the same number.
    /// <para>
    /// Today it is the learned level of the skill the table gives the work. Whatever later changes how good a crawler is at
    /// its work without being practice (a piece of gear, a talent, a family's share) enters here, and the learned level
    /// (<see cref="SkillSet.Level"/>) stays what practice raises, the save holds and the dossier lists.
    /// </para>
    /// </summary>
    public static class CrawlerSkills
    {
        /// <summary>
        /// The effective level of a crawler with <paramref name="skills"/>, wearing <paramref name="worn"/>, at
        /// <paramref name="work"/>, from 0 to <see cref="SkillRules.MaxLevel"/>; 0 for work no skill answers.
        /// </summary>
        public static int Level(in SkillSet skills, IAgentInventory worn, in SkillWork work) => skills.Level(SkillTable.SkillFor(work));

        /// <summary>As <see cref="Level(in SkillSet, IAgentInventory, in SkillWork)"/>, for a crawler's state on the main thread; 0 for none.</summary>
        public static int Level(AgentState crawler, in SkillWork work) => crawler == null ? 0 : Level(crawler.Skills, crawler.Inventory, work);
    }
}
