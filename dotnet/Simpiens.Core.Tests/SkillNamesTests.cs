using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Code asks the skill table, not a skill (Epic 10 Part 3, K9.6): production code names a skill (Skill.X, other than
    /// None) only in the files below, each with its reason, so a new skill is a row with no code of its own (AGENTS.md,
    /// "Adding a skill"): code names the work it does (SkillWork) and the table finds its skill (SkillFor). The list
    /// grows only by tables whose rows belong to a skill (Epic 11's techniques); a file that starts naming skills
    /// otherwise fails, and so does an entry whose file no longer names any.
    /// </summary>
    public class SkillNamesTests
    {
        private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
        {
            ["Simpiens.Core/Simulation/Skills/SkillTable.cs"] = "the table: the skills' rows",
            ["Simpiens.Core/Simulation/Skills/TechniqueTable.cs"] = "the techniques' table: each row names the skill whose band offers it (Epic 11, S4.1)",
            ["Simpiens.Core/Simulation/Skills/CallingTable.cs"] = "the callings' table: each row names the skill it is a calling of (Epic 11, S5.2)",
        };

        private static readonly string[] Projects = { "Simpiens.Core", "Simpiens.Presentation", "Simpiens.Client", "Simpiens.Hosting" };

        private static readonly Regex SkillName = new Regex(@"\b(Skill\.(" + string.Join("|", Enum.GetNames(typeof(Simpiens.Simulation.Skills.Skill)).Where(n => n != "None")) + @"))\b",
            RegexOptions.Compiled);

        [Fact]
        public void ProductionCodeNamesSkillsOnlyWhereAllowed()
        {
            string dotnet = FindDotnetFolder();
            var naming = new Dictionary<string, List<string>>();
            foreach (var project in Projects)
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(dotnet, project), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(dotnet, file).Replace('\\', '/');
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    var names = SkillName.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).Distinct().ToList();
                    if (names.Count > 0) naming[relative] = names;
                }
            }

            var offenders = naming.Keys.Where(f => !Allowed.ContainsKey(f)).OrderBy(f => f).Select(f => $"{f} ({string.Join(", ", naming[f])})").ToList();
            var stale = Allowed.Keys.Where(f => !naming.ContainsKey(f)).OrderBy(f => f).ToList();
            Assert.True(offenders.Count == 0, "Name the work (SkillWork), not a skill: " + string.Join("; ", offenders));
            Assert.True(stale.Count == 0, "These files no longer name skills; take them off the list: " + string.Join("; ", stale));
        }

        private static string FindDotnetFolder()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Simpiens.slnx"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }
    }
}
