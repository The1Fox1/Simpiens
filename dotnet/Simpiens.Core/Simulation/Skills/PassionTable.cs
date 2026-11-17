using System;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;

namespace Simpiens.Simulation.Skills
{
    /// <summary>One step of the passions' scale: a row of <see cref="PassionTable"/>.</summary>
    public sealed class PassionDefinition
    {
        public PassionDefinition(Passion id, string name, string displayName, int practicePercent, float frustrationEased, string glyph)
        {
            Id = id;
            Name = name;
            DisplayName = displayName;
            PracticePercent = practicePercent;
            FrustrationEased = frustrationEased;
            Glyph = glyph;
        }

        public Passion Id { get; }

        /// <summary>The code name, for logs.</summary>
        public string Name { get; }

        /// <summary>The player's word: "keen", "burning"; empty for none.</summary>
        public string DisplayName { get; }

        /// <summary>Practice at the skill counts this many hundredths of itself (Q7: ×1, ×1.5, ×2).</summary>
        public int PracticePercent { get; }

        /// <summary>Frustration each act of practice at the skill eases (Q7: "work at a passion relieves frustration").</summary>
        public float FrustrationEased { get; }

        /// <summary>The UI kit's glyph the dashboard marks it with; null for none.</summary>
        public string Glyph { get; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Passions, as a table (Epic 11, A2, Q7, D3): none, keen and burning, what each does for practice and frustration, and
    /// the draw at spawn. Static and immutable, as the skills' table is. A step added is a <see cref="Passion"/> value and a
    /// row (its odds in the draw are its own code).
    /// </summary>
    public static class PassionTable
    {
        private static readonly PassionDefinition[] ById =
        {
            new PassionDefinition(Passion.None, "None", "", 100, 0f, null),
            new PassionDefinition(Passion.Keen, "Keen", "keen", 150, 2f, "glyph/passion-keen"),
            new PassionDefinition(Passion.Burning, "Burning", "burning", 200, 4f, "glyph/passion-burning")
        };

        /// <summary>Every step, in id order.</summary>
        public static IReadOnlyList<PassionDefinition> All => ById;

        /// <summary>The step's row; none's for an id past the table.</summary>
        public static PassionDefinition Get(Passion id) => (byte)id < ById.Length ? ById[(byte)id] : ById[0];

        // --- The draw at spawn (D3) ---

        /// <summary>No keen passion with this chance, one up to <see cref="OneKeenBelow"/>, two above it.</summary>
        public const float NoKeenBelow = 0.4f, OneKeenBelow = 0.8f;

        /// <summary>A burning passion with this chance.</summary>
        public const float BurningChance = 0.25f;

        /// <summary>A starting talent is this many times as likely to be loved as a skill its personality leans to as much.</summary>
        public const float TalentWeight = 2f;

        /// <summary>
        /// The passions of <paramref name="entityId"/> under a session seed (D3), on <see cref="SimRandomSalt.Passions"/>, so
        /// no earlier draw moves: a burning passion with a quarter's chance, then none, one or two keen ones. Each is picked
        /// among the skills at odds of the row's lean (as talents are) and doubled for a starting talent in
        /// <paramref name="skills"/>; a skill holds one passion at most. No look enters the draw.
        /// </summary>
        public static CrawlerTraits Draw(ulong seed, EntityId entityId, in AgentPersonality personality, in SkillSet skills, CrawlerTraits traits = default)
        {
            var rng = SimRandom.For(seed, entityId, 0, SimRandomSalt.Passions);
            var all = SkillTable.All;
            Span<float> weights = stackalloc float[SkillTable.Capacity];
            for (int i = 0; i < all.Count; i++)
            {
                float weight = (float)(1f + (float)(SkillRules.LeanWeight * Math.Max(0f, SkillSet.Lean(all[i].Lean, personality))));
                if (skills.Level(all[i].Id) >= SkillRules.TalentLevel) weight = (float)(weight * TalentWeight);
                weights[i] = weight;
            }
            bool burning = rng.NextFloat() < BurningChance;
            float keenRoll = rng.NextFloat();
            int keen = keenRoll < NoKeenBelow ? 0 : keenRoll < OneKeenBelow ? 1 : 2;
            if (burning) Pick(ref rng, all, weights, ref traits, Passion.Burning);
            for (int k = 0; k < keen; k++) Pick(ref rng, all, weights, ref traits, Passion.Keen);
            return traits;
        }

        private static void Pick(ref SimRandom rng, IReadOnlyList<SkillDefinition> all, Span<float> weights, ref CrawlerTraits traits, Passion passion)
        {
            float total = 0f;
            for (int i = 0; i < all.Count; i++) total += weights[i];
            if (total <= 0f) return;
            float pick = (float)(rng.NextFloat() * total);
            int chosen = -1;
            for (int i = 0; i < all.Count; i++)
            {
                if (weights[i] <= 0f) continue;
                chosen = i;
                if (pick < weights[i]) break;
                pick -= weights[i];
            }
            if (chosen < 0) return;
            traits.SetPassion(all[chosen].Id, passion);
            weights[chosen] = 0f; // one passion a skill
        }
    }
}
