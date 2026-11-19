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

        /// <summary>A skill as the dashboard lists it (Epic 11, S1.6): its words ("Smithing 5") and the passion the crawler holds for it.</summary>
        public readonly struct Shown
        {
            public Shown(string text, Passion passion)
            {
                Text = text;
                Passion = passion;
            }

            public string Text { get; }
            public Passion Passion { get; }

            /// <summary>The UI kit's flame for its passion; null for none.</summary>
            public string Glyph => PassionTable.Get(Passion).Glyph;
        }

        /// <summary>
        /// The skills to list, into <paramref name="into"/> (cleared first), the higher level first and ties in display order
        /// (Epic 11, S1.6): up to <paramref name="most"/> of those above nothing, each with its passion. With
        /// <paramref name="keepPassions"/> every skill the crawler loves is listed even at 0 (its nature: the dossier), taking
        /// the place of the lowest; without, a passion shows only on a skill listed by its level (the roster's two).
        /// </summary>
        public static void BestShown(in SkillSet skills, in CrawlerTraits traits, int most, bool keepPassions, System.Collections.Generic.List<Shown> into)
        {
            into.Clear();
            var shown = SkillTable.InDisplayOrder;
            System.Span<int> order = stackalloc int[SkillTable.Capacity];
            int count = 0;
            for (int i = 0; i < shown.Count; i++)
            {
                bool loved = keepPassions && traits.PassionFor(shown[i].Id) != Passion.None;
                if (skills.Level(shown[i].Id) <= 0 && !loved) continue;
                int at = count;
                while (at > 0 && skills.Level(shown[order[at - 1]].Id) < skills.Level(shown[i].Id))
                {
                    order[at] = order[at - 1];
                    at--;
                }
                order[at] = i;
                count++;
            }
            // Keep the loved within the most shown: drop the lowest unloved first
            System.Span<bool> keep = stackalloc bool[SkillTable.Capacity];
            int kept = 0;
            for (int i = 0; i < count; i++)
            {
                if (!keepPassions || traits.PassionFor(shown[order[i]].Id) == Passion.None) continue;
                keep[i] = true;
                kept++;
            }
            for (int i = 0; i < count && kept < most; i++)
            {
                if (keep[i]) continue;
                keep[i] = true;
                kept++;
            }
            for (int i = 0; i < count; i++)
            {
                if (!keep[i]) continue;
                var skill = shown[order[i]];
                into.Add(new Shown(Invariant($"{skill.DisplayName} {skills.Level(skill.Id)}"), traits.PassionFor(skill.Id)));
            }
        }

        /// <summary>
        /// The first skill the mastery budget holds, in the table's display order (Epic 11, S2.5, D7): "Foraging held at 5:
        /// two crafts past Apprentice", "Smithing held at 8: a Master already"; empty when none is held.
        /// </summary>
        public static string Held(in SkillSet skills)
        {
            var shown = SkillTable.InDisplayOrder;
            for (int i = 0; i < shown.Count; i++)
            {
                if (!SkillRules.HeldByBudget(skills, shown[i].Id, out int ceiling, out var hold)) continue;
                string why = hold == BudgetHold.Master ? "a Master already" : "two crafts past Apprentice";
                return Invariant($"{shown[i].DisplayName} held at {ceiling}: {why}");
            }
            return string.Empty;
        }

        /// <summary>"asks 7g" (Epic 11, S2.5, D8): an asking wage above the wage paid; empty when it asks no more.</summary>
        public static string Asks(int wage, int asking) => asking > wage ? Invariant($"asks {asking}g") : string.Empty;

        /// <summary>
        /// "Smithing 4 (Apprentice)": how good a crawler is at <paramref name="work"/> (its effective level, Epic 10 Part 3,
        /// K9.8a), named by the skill that answers for it, with its band; empty for work no skill answers.
        /// </summary>
        public static string WithBand(in SkillSet skills, Simpiens.Cognition.Inventory.IAgentInventory worn, in SkillWork work)
        {
            var definition = SkillTable.Get(SkillTable.SkillFor(work));
            if (definition == null) return string.Empty;
            int level = CrawlerSkills.Level(skills, worn, work);
            return Invariant($"{definition.DisplayName} {level} ({SkillRules.NameOf(SkillRules.BandOf(level))})");
        }
    }
}
