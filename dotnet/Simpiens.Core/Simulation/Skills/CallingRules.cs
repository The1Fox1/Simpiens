namespace Simpiens.Simulation.Skills
{
    /// <summary>
    /// A calling earned (Epic 11, S5.3, Q11, D21): from the crawler's best skill and the technique it took at Apprentice there.
    /// Pure functions over the skills and the traits record. The calling is kept in the record and judged again at spawn, at a
    /// band reached and at a technique changed; the chronicle tells a change.
    /// </summary>
    public static class CallingRules
    {
        /// <summary>
        /// The calling a crawler with <paramref name="skills"/> and <paramref name="traits"/> holds now (D21): its best skill's
        /// (the highest learned level, then the most experience, then the table's display order), at Apprentice or above, by the
        /// Apprentice technique it holds there; none below Apprentice in everything. Kept: while the skill of the calling it holds
        /// stands at the highest level, that skill stays its calling's (ties never flicker), by the technique it holds there now.
        /// </summary>
        public static Calling Earned(in SkillSet skills, in CrawlerTraits traits)
        {
            int bestLevel = 0;
            var shown = SkillTable.InDisplayOrder;
            for (int i = 0; i < shown.Count; i++)
            {
                int level = skills.Level(shown[i].Id);
                if (level > bestLevel) bestLevel = level;
            }
            if (SkillRules.BandOf(bestLevel) < SkillBand.Apprentice) return Calling.None;

            var held = CallingTable.Get(traits.Calling);
            if (held != null && skills.Level(held.Skill) == bestLevel) return CallingTable.For(held.Skill, traits.TechniqueIn(held.Skill, SkillBand.Apprentice));

            Skill best = Skill.None;
            int bestExperience = -1;
            for (int i = 0; i < shown.Count; i++)
            {
                var id = shown[i].Id;
                if (skills.Level(id) != bestLevel) continue;
                int experience = skills.Experience(id);
                if (experience <= bestExperience) continue;
                best = id;
                bestExperience = experience;
            }
            return CallingTable.For(best, traits.TechniqueIn(best, SkillBand.Apprentice));
        }

        /// <summary>
        /// Judges <paramref name="traits"/>' calling again (at a band reached, a technique picked or sponsored); true when it
        /// changed, with the one it held in <paramref name="was"/>.
        /// </summary>
        public static bool Rejudge(in SkillSet skills, ref CrawlerTraits traits, out Calling was)
        {
            was = traits.Calling;
            var now = Earned(skills, traits);
            if (now == was) return false;
            traits.SetCalling(now);
            return true;
        }

        /// <summary>The title a crawler holding <paramref name="calling"/> goes by (D23): its Master title at Master in the calling's skill, its word below; empty for none.</summary>
        public static string TitleOf(Calling calling, in SkillSet skills)
        {
            var row = CallingTable.Get(calling);
            return row == null ? string.Empty : row.TitleAt(skills.Band(row.Skill));
        }
    }
}
