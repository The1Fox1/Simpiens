using System;
using System.Collections.Generic;
using Simpiens.Simulation.Skills;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// The calling table (Epic 11, S5.2, A2, Q11, D20, D23): ids and words unique and Latin-1; every Apprentice technique of every
    /// skill earns exactly one calling of that skill; a skill without techniques has one calling earned by none (Leadership's
    /// Captain); the fighters', medics' and captain's roles; every Master title and capstone sound. Runs as a test
    /// (ValidatorTests).
    /// </summary>
    public class CallingValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[CallingValidator] Beginning calling validation...");
            ValidateTheTable();
            ValidateWhatEarnsThem();
            ValidateRolesTitlesAndCapstones();
            TestLog.Log("<color=green>[CallingValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateTheTable()
        {
            var all = CallingTable.All;
            Assert(all.Count == 15, $"Fifteen callings (D20): {all.Count}.");
            var names = new HashSet<string>();
            byte last = 0;
            foreach (var row in all)
            {
                Assert((byte)row.Id > last, $"{row.Name}: ids rise.");
                last = (byte)row.Id;
                Assert(!string.IsNullOrEmpty(row.DisplayName) && names.Add(row.DisplayName) && names.Add(row.MasterName), $"{row.Name}: its word and Master title its own.");
                foreach (char c in row.DisplayName + row.MasterName) Assert(c < 256, $"{row.Name}: Latin-1, as the fonts are.");
                Assert(SkillTable.IsDefined(row.Skill), $"{row.Name}: its skill is in the table.");
                Assert(CallingTable.Get(row.Id) == row && CallingTable.NameOf(row.Id) == row.Name, $"{row.Name} is found by its id.");
            }
            foreach (Calling id in Enum.GetValues(typeof(Calling)))
                Assert(id == Calling.None || CallingTable.Get(id) != null, $"Calling {id} has a row.");
            Assert(CallingTable.Get(Calling.None) == null && CallingTable.RoleOf(Calling.None) == PartyRole.None, "No calling, no row, no role.");
            TestLog.Log($"[PASS] Test 1: {all.Count} callings, ids and words unique, in Latin-1.");
        }

        private static void ValidateWhatEarnsThem()
        {
            foreach (var skill in SkillTable.All)
            {
                var options = TechniqueTable.Options(skill.Id, SkillBand.Apprentice);
                if (options.Count == 0)
                {
                    var any = CallingTable.For(skill.Id, Technique.None);
                    Assert(any != Calling.None && CallingTable.Get(any).EarnedBy == Technique.None, $"{skill.Name}, with no techniques, has a calling earned by none.");
                    continue;
                }
                var seen = new HashSet<Calling>();
                foreach (var technique in options)
                {
                    var calling = CallingTable.For(skill.Id, technique.Id);
                    Assert(calling != Calling.None, $"{technique.Name} earns a calling of {skill.Name}.");
                    var row = CallingTable.Get(calling);
                    Assert(row.Skill == skill.Id && row.EarnedBy == technique.Id, $"{technique.Name}'s calling, {row.Name}, names it.");
                    Assert(seen.Add(calling), $"{skill.Name}'s two Apprentice techniques earn two callings.");
                }
                Assert(CallingTable.For(skill.Id, Technique.None) == Calling.None, $"{skill.Name} with no technique at Apprentice earns none.");
            }
            foreach (var row in CallingTable.All)
            {
                if (row.EarnedBy == Technique.None) continue;
                var technique = TechniqueTable.Get(row.EarnedBy);
                Assert(technique != null && technique.Skill == row.Skill && technique.Band == SkillBand.Apprentice, $"{row.Name} is earned by an Apprentice technique of its own skill.");
            }
            TestLog.Log("[PASS] Test 2: each Apprentice technique earns one calling of its skill; Leadership's Captain is earned by none.");
        }

        private static void ValidateRolesTitlesAndCapstones()
        {
            Assert(CallingTable.RoleOf(Calling.Warden) == PartyRole.Front && CallingTable.RoleOf(Calling.Berserker) == PartyRole.Front, "The melee callings hold the front.");
            Assert(CallingTable.RoleOf(Calling.Marksman) == PartyRole.Back && CallingTable.RoleOf(Calling.Skirmisher) == PartyRole.Back, "The ranged callings shoot from behind.");
            Assert(CallingTable.RoleOf(Calling.FieldSurgeon) == PartyRole.Carrier && CallingTable.RoleOf(Calling.Bearer) == PartyRole.Carrier, "The medics carry the fallen.");
            Assert(CallingTable.RoleOf(Calling.Captain) == PartyRole.Leader, "The Captain leads.");
            foreach (var row in CallingTable.All)
            {
                var use = SkillTable.Get(row.Skill).Use;
                Assert((row.Role == PartyRole.Front) == (use == SkillUse.MeleeFight) && (row.Role == PartyRole.Back) == (use == SkillUse.RangedFight)
                    && (row.Role == PartyRole.Carrier) == (use == SkillUse.Tend) && (row.Role == PartyRole.Leader) == (use == SkillUse.Lead), $"{row.Name}'s role follows its skill's work.");
                Assert(row.TitleAt(SkillBand.Master) == row.MasterName && row.TitleAt(SkillBand.Journeyman) == row.DisplayName && row.TitleAt(SkillBand.Apprentice) == row.DisplayName, $"{row.Name}: its Master title at Master only.");
                var cap = row.Capstone;
                if (cap.Stat == TechniqueStat.None) continue;
                Assert(cap.IsUpside && TechniqueRules.AppliesTo(cap.Stat, use), $"{row.Name}'s capstone is an upside in its own work ({cap.Stat}).");
                var (min, max) = TechniqueRules.Bounds(cap.Stat);
                Assert(cap.Value >= min && cap.Value <= max, $"{row.Name}'s capstone within bounds.");
            }
            TestLog.Log("[PASS] Test 3: roles follow the skill's work (front, back, carrier, leader); Master titles at Master only; capstones are upsides in their own work, within bounds.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[CallingValidator] FAILED: " + message);
        }
    }
}
