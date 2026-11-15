using Simpiens.Simulation.Skills;
using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// A crawler's skills in the dashboard's words (Epic 10 Part 3, K9.5): the best first (the higher level, then the table's
    /// display order), by the table's own names, so a skill added as a row is listed with no code here.
    /// </summary>
    public static class SkillTexts
    {
        public const string None = "Unskilled";

        /// <summary>"Smithing 5 · Foraging 4 · Melee 1": up to <paramref name="most"/> skills above nothing, the best first; <see cref="None"/> for none.</summary>
        public static string Best(in SkillSet skills, int most, string separator = " · ")
        {
            var shown = SkillTable.InDisplayOrder;
            System.Span<int> order = stackalloc int[SkillTable.Capacity];
            int count = 0;
            for (int i = 0; i < shown.Count; i++)
            {
                if (skills.Level(shown[i].Id) <= 0) continue;
                // Insertion by level, the higher first; ties keep display order
                int at = count;
                while (at > 0 && skills.Level(shown[order[at - 1]].Id) < skills.Level(shown[i].Id))
                {
                    order[at] = order[at - 1];
                    at--;
                }
                order[at] = i;
                count++;
            }
            if (count == 0) return None;
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < count && i < most; i++)
            {
                if (i > 0) text.Append(separator);
                var skill = shown[order[i]];
                text.Append(skill.DisplayName).Append(' ').Append(skills.Level(skill.Id));
            }
            return text.ToString();
        }

        /// <summary>"Smithing 4 (Apprentice)": one skill with its band; empty for no such skill.</summary>
        public static string WithBand(in SkillSet skills, Skill skill)
        {
            var definition = SkillTable.Get(skill);
            if (definition == null) return string.Empty;
            int level = skills.Level(skill);
            return Invariant($"{definition.DisplayName} {level} ({SkillRules.NameOf(SkillRules.BandOf(level))})");
        }
    }
}
