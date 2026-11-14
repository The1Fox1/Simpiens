using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Simpiens.Cognition.Contracts;

namespace Simpiens.Simulation.Skills
{
    /// <summary>The experience of each of <see cref="SkillTable.Capacity"/> skills, by id.</summary>
    [InlineArray(SkillTable.Capacity)]
    public struct SkillExperiences
    {
        private ushort _first;
    }

    /// <summary>
    /// A crawler's skills (Epic 10 Part 3, K9.1): the experience of each, by the skill's id; its level and band are read from
    /// the experience (<see cref="SkillRules"/>). Plain data, 32 bytes, saved whole in the crawler's record with room for 16,
    /// so a skill added later loads at nothing from an older save. Drawn at spawn (<see cref="Generate"/>), raised by
    /// practice on the main thread; cognition reads a copy.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SkillSet : IEquatable<SkillSet>
    {
        private SkillExperiences _experience;

        public readonly int Experience(Skill skill) => (byte)skill < SkillTable.Capacity ? _experience[(byte)skill] : 0;

        public readonly int Level(Skill skill) => SkillRules.LevelOf(Experience(skill));

        public readonly SkillBand Band(Skill skill) => SkillRules.BandOf(Level(skill));

        /// <summary>The experience by raw index, for the save's checks and the world's hash.</summary>
        public readonly int ExperienceAt(int index) => (uint)index < SkillTable.Capacity ? _experience[index] : 0;

        /// <summary>Sets <paramref name="skill"/> to exactly <paramref name="level"/> (its first experience).</summary>
        public void Set(Skill skill, int level)
        {
            if ((byte)skill == 0 || (byte)skill >= SkillTable.Capacity) return;
            _experience[(byte)skill] = (ushort)SkillRules.ExperienceFor(level);
        }

        /// <summary>
        /// Adds <paramref name="experience"/> to <paramref name="skill"/>, up to the last level's. True when a level was
        /// gained, with the level it now has.
        /// </summary>
        public bool Practise(Skill skill, int experience, out int level)
        {
            level = 0;
            if ((byte)skill == 0 || (byte)skill >= SkillTable.Capacity || experience <= 0) return false;
            int before = _experience[(byte)skill];
            int after = Math.Min(SkillRules.MaxExperience, before + experience);
            _experience[(byte)skill] = (ushort)after;
            level = SkillRules.LevelOf(after);
            return level > SkillRules.LevelOf(before);
        }

        /// <summary>The skill it is best at (the highest experience, the lowest id on a tie); none with none above nothing.</summary>
        public readonly Skill Best()
        {
            var all = SkillTable.All;
            var best = Skill.None;
            int most = 0;
            for (int i = 0; i < all.Count; i++)
            {
                int experience = Experience(all[i].Id);
                if (experience <= most) continue;
                most = experience;
                best = all[i].Id;
            }
            return best;
        }

        /// <summary>
        /// The starting skills of <paramref name="entityId"/> under a session seed (Q19), on <see cref="SimRandomSalt.Skills"/>:
        /// each skill of the table, in id order, a Novice's 0, 1 or 2; then one talent or two at an Apprentice's 3 to 5, each
        /// picked among the skills at odds its row's lean and <paramref name="personality"/> weight ("the bold lean toward
        /// melee"). No look enters the draw.
        /// </summary>
        public static SkillSet Generate(ulong seed, EntityId entityId, in AgentPersonality personality)
        {
            var rng = SimRandom.For(seed, entityId, 0, SimRandomSalt.Skills);
            var all = SkillTable.All;
            var set = default(SkillSet);
            for (int i = 0; i < all.Count; i++)
            {
                float roll = rng.NextFloat();
                set.Set(all[i].Id, roll < SkillRules.StartsAtZero ? 0 : roll < SkillRules.StartsAtOneOrLess ? 1 : 2);
            }

            Span<float> weights = stackalloc float[SkillTable.Capacity];
            for (int i = 0; i < all.Count; i++) weights[i] = (float)(1f + (float)(SkillRules.LeanWeight * Math.Max(0f, Lean(all[i].Lean, personality))));
            int talents = rng.NextFloat() < SkillRules.SecondTalent ? 2 : 1;
            for (int t = 0; t < talents; t++)
            {
                float total = 0f;
                for (int i = 0; i < all.Count; i++) total += weights[i];
                if (total <= 0f) break;
                float pick = (float)(rng.NextFloat() * total);
                int chosen = -1;
                for (int i = 0; i < all.Count; i++)
                {
                    if (weights[i] <= 0f) continue;
                    chosen = i;
                    if (pick < weights[i]) break;
                    pick -= weights[i];
                }
                if (chosen < 0) break;
                int spread = Math.Min(SkillRules.TalentSpread - 1, (int)(rng.NextFloat() * SkillRules.TalentSpread));
                set.Set(all[chosen].Id, SkillRules.TalentLevel + spread);
                weights[chosen] = 0f; // a talent once
            }
            return set;
        }

        /// <summary>How far <paramref name="personality"/> leans the way of <paramref name="lean"/>, from -1 to 1.</summary>
        public static float Lean(SkillLean lean, in AgentPersonality personality) => lean switch
        {
            SkillLean.Bold => personality.RiskTolerance,
            SkillLean.Cautious => -personality.RiskTolerance,
            SkillLean.Diligent => personality.Diligence,
            SkillLean.Sociable => personality.Sociability,
            SkillLean.Loyal => personality.Loyalty,
            SkillLean.Greedy => personality.Greed,
            _ => 0f
        };

        public readonly bool Equals(SkillSet other)
        {
            for (int i = 0; i < SkillTable.Capacity; i++) if (_experience[i] != other._experience[i]) return false;
            return true;
        }

        public override readonly bool Equals(object obj) => obj is SkillSet other && Equals(other);

        public override readonly int GetHashCode()
        {
            var hash = new HashCode();
            for (int i = 0; i < SkillTable.Capacity; i++) hash.Add(_experience[i]);
            return hash.ToHashCode();
        }

        public static bool operator ==(SkillSet a, SkillSet b) => a.Equals(b);
        public static bool operator !=(SkillSet a, SkillSet b) => !a.Equals(b);

        /// <summary>"Melee 4, Mining 1": every skill above nothing, in the table's display order; "unskilled" for none.</summary>
        public override readonly string ToString()
        {
            var text = new System.Text.StringBuilder();
            var shown = SkillTable.InDisplayOrder;
            for (int i = 0; i < shown.Count; i++)
            {
                int level = Level(shown[i].Id);
                if (level == 0) continue;
                if (text.Length > 0) text.Append(", ");
                text.Append(shown[i].DisplayName).Append(' ').Append(level);
            }
            return text.Length > 0 ? text.ToString() : "unskilled";
        }
    }
}
