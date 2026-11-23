using System;
using System.Collections.Generic;
using Simpiens.Simulation.Skills;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// The technique table (Epic 11, S4.1, A2, A4, A5, D13): every skill with techniques offers two at each band above Novice;
    /// ids and names are unique; each row has an upside and a cost, its stats apply to its skill's work, stay within their
    /// bounds and are named in its words; the traits record holds one technique a band. Runs as a test (ValidatorTests).
    /// </summary>
    public class TechniqueValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[TechniqueValidator] Beginning technique validation...");
            ValidateTheTable();
            ValidateEachRow();
            ValidateTheBounds();
            ValidateTheRecord();
            TestLog.Log("<color=green>[TechniqueValidator] ALL 4 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static readonly SkillBand[] Bands = { SkillBand.Apprentice, SkillBand.Journeyman, SkillBand.Master };

        private static void ValidateTheTable()
        {
            var all = TechniqueTable.All;
            Assert(all.Count == 42, $"42 techniques, two at each band of seven skills (D13): {all.Count}.");
            var names = new HashSet<string>();
            byte last = 0;
            foreach (var row in all)
            {
                Assert((byte)row.Id > last, $"{row.Name}: ids rise.");
                last = (byte)row.Id;
                Assert(names.Add(row.DisplayName) && !string.IsNullOrEmpty(row.DisplayName), $"{row.Name}: a unique name in the player's words.");
                Assert(TechniqueTable.Get(row.Id) == row && TechniqueTable.NameOf(row.Id) == row.Name, $"{row.Name} is found by its id.");
                Assert(SkillTable.IsDefined(row.Skill), $"{row.Name}: its skill is in the table.");
            }
            foreach (Technique id in Enum.GetValues(typeof(Technique)))
                Assert(id == Technique.None || TechniqueTable.Get(id) != null, $"Technique {id} has a row.");
            Assert(TechniqueTable.Get(Technique.None) == null, "No technique has no row.");

            int skillsWith = 0;
            foreach (var skill in SkillTable.All)
            {
                int rows = 0;
                foreach (var band in Bands) rows += TechniqueTable.Options(skill.Id, band).Count;
                Assert(rows == 0 || rows == 6, $"{skill.Name} has techniques at every band or none ({rows}).");
                if (rows == 0) continue;
                skillsWith++;
                foreach (var band in Bands)
                {
                    var options = TechniqueTable.Options(skill.Id, band);
                    Assert(options.Count == 2, $"{skill.Name} offers two at {band} ({options.Count}).");
                    Assert(options[0].Skill == skill.Id && options[0].Band == band && options[1].Skill == skill.Id && options[1].Band == band, $"{skill.Name}'s {band} rows are its own.");
                    Assert(options[0].Lean != options[1].Lean, $"{skill.Name} at {band}: its two lean different ways, so nature decides ({options[0].Lean}).");
                    Assert(TechniqueTable.OtherThan(options[0].Id) == options[1].Id && TechniqueTable.OtherThan(options[1].Id) == options[0].Id, $"{skill.Name} at {band}: each is the other's alternative.");
                }
                Assert(TechniqueTable.Options(skill.Id, SkillBand.Novice).Count == 0, $"{skill.Name}: nothing at Novice.");
            }
            Assert(skillsWith == 7, $"The seven skills of plan 1 have techniques ({skillsWith}).");
            Assert(TechniqueTable.Options(Skill.None, SkillBand.Apprentice).Count == 0 && TechniqueTable.OtherThan(Technique.None) == Technique.None, "No skill offers nothing.");
            TestLog.Log($"[PASS] Test 1: {all.Count} techniques, two at each band of {skillsWith} skills, leaning different ways, ids and names unique.");
        }

        private static void ValidateEachRow()
        {
            foreach (var row in TechniqueTable.All)
            {
                var use = SkillTable.Get(row.Skill).Use;
                Assert(row.Effects.Count >= 2 && row.Effects.Count <= 3, $"{row.Name}: two or three effects ({row.Effects.Count}).");
                bool up = false, down = false;
                var stats = new HashSet<TechniqueStat>();
                foreach (var effect in row.Effects)
                {
                    Assert(stats.Add(effect.Stat), $"{row.Name}: {effect.Stat} once.");
                    Assert(effect.Value != 0, $"{row.Name}: {effect.Stat} changes something.");
                    Assert(TechniqueRules.AppliesTo(effect.Stat, use), $"{row.Name}: {effect.Stat} applies to {use}.");
                    var (min, max) = TechniqueRules.Bounds(effect.Stat);
                    Assert(effect.Value >= min && effect.Value <= max, $"{row.Name}: {effect.Stat} {effect.Value} within {min}..{max}.");
                    string words = TechniqueRules.Describe(effect);
                    Assert(words.Length > 0 && row.Words.Contains(words), $"{row.Name}'s words name {effect.Stat}: \"{row.Words}\".");
                    foreach (char c in words) Assert(c < 256, $"{row.Name}'s words are Latin-1, as the fonts are: '{c}'.");
                    up |= effect.IsUpside;
                    down |= !effect.IsUpside;
                }
                Assert(up && down, $"{row.Name}: an upside and a cost (D13).");
                Assert(!string.IsNullOrWhiteSpace(row.Vow), $"{row.Name}: a vow for its scene.");
                foreach (char c in row.Vow + row.DisplayName) Assert(c < 256, $"{row.Name}: Latin-1 words, as the fonts are: '{c}'.");
                Assert(row.ValueOf(row.Effects[0].Stat) == row.Effects[0].Value && row.ValueOf(TechniqueStat.None) == 0, $"{row.Name}: its values are found by stat.");
            }
            TestLog.Log("[PASS] Test 2: each technique has two or three effects, an upside and a cost, within bounds, applying to its work and named in its words.");
        }

        private static void ValidateTheBounds()
        {
            foreach (TechniqueStat stat in Enum.GetValues(typeof(TechniqueStat)))
            {
                if (stat == TechniqueStat.None) continue;
                var (min, max) = TechniqueRules.Bounds(stat);
                Assert(min <= 0 && max > 0, $"{stat}: bounds around nothing ({min}..{max}).");
                Assert(TechniqueRules.Clamp(stat, max + 1000) == max && TechniqueRules.Clamp(stat, min - 1000) == min && TechniqueRules.Clamp(stat, 0) == 0, $"{stat}: held within its bounds.");
                bool used = false;
                foreach (var row in TechniqueTable.All) used |= row.ValueOf(stat) != 0;
                foreach (var trait in EarnedTraitTable.All) used |= trait.ValueOf(stat) != 0; // or an earned trait (Epic 11, S6.3: Dread is Shaken's)
                Assert(used, $"{stat}: some technique or earned trait uses it.");
            }
            TestLog.Log("[PASS] Test 3: every stat has bounds around nothing, holds within them, and some technique or earned trait uses it.");
        }

        private static void ValidateTheRecord()
        {
            var traits = default(CrawlerTraits);
            foreach (var row in TechniqueTable.All) Assert(traits.TechniqueIn(row.Skill, row.Band) == Technique.None, "A new record holds no technique.");
            foreach (var skill in SkillTable.All)
                foreach (var band in Bands)
                {
                    var options = TechniqueTable.Options(skill.Id, band);
                    if (options.Count > 0) traits.SetTechnique(skill.Id, band, options[(int)band % 2].Id);
                }
            foreach (var skill in SkillTable.All)
                foreach (var band in Bands)
                {
                    var options = TechniqueTable.Options(skill.Id, band);
                    if (options.Count > 0) Assert(traits.TechniqueIn(skill.Id, band) == options[(int)band % 2].Id, $"{skill.Name} at {band} keeps its own technique: no two slots overlap.");
                }
            Assert(!traits.IsEmpty && traits.PassionFor(Skill.Melee) == Passion.None && traits.Count(Deed.Make) == 0, "Techniques write only their slots.");
            traits.SetTechnique(Skill.Melee, SkillBand.Apprentice, Technique.None);
            Assert(traits.TechniqueIn(Skill.Melee, SkillBand.Apprentice) == Technique.None, "None clears a slot.");
            var before = traits;
            traits.SetTechnique(Skill.None, SkillBand.Apprentice, Technique.ShieldWall);
            traits.SetTechnique(Skill.Melee, SkillBand.Novice, Technique.ShieldWall);
            traits.SetTechnique((Skill)SkillTable.Capacity, SkillBand.Master, Technique.ShieldWall);
            Assert(traits == before, "No skill, Novice, or a skill past the room writes nothing.");
            Assert(traits.TechniqueIn(Skill.Melee, SkillBand.Novice) == Technique.None && traits.TechniqueIn((Skill)SkillTable.Capacity, SkillBand.Master) == Technique.None, "And reads none.");
            TestLog.Log("[PASS] Test 4: the traits record holds one technique at each band of each skill, and nothing elsewhere.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[TechniqueValidator] FAILED: " + message);
        }
    }
}
