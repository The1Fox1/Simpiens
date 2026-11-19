using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Effects and choices read how good a crawler is at its work through the seam (Epic 10 Part 3, K9.8a): production code
    /// reads a skill set's learned level (<c>.Level(</c>, <c>.Band(</c>, <c>.Experience(</c>) only in the files below, each
    /// with its reason. Everything else asks <c>CrawlerSkills.Level</c> by the kind of work, so whatever later changes how good
    /// a crawler is at its work (gear, a talent, a family) enters in one place. The list only shrinks: a file that starts
    /// reading learned levels fails, and so does an entry whose file no longer reads any.
    /// </summary>
    public class SkillReadsTests
    {
        private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
        {
            ["Simpiens.Core/Simulation/Skills/CrawlerSkills.cs"] = "the seam: the effective level, from the learned one",
            ["Simpiens.Core/Simulation/SimulationManager.cs"] = "practice's one entry: a band reached is the learned band",
            ["Simpiens.Presentation/Dashboard/SkillTexts.cs"] = "the dossier's and the roster's list of what a crawler has learned",
            ["Simpiens.Core/Simulation/Skills/SkillRules.cs"] = "the mastery budget (Epic 11, D7): practice's own rule, over the levels practice has raised",
            ["Simpiens.Core/Simulation/Skills/PassionTable.cs"] = "the passions' draw at spawn: a starting talent is loved more often (Epic 11, D3); a draw, not an effect",
            ["Simpiens.Core/Simulation/Skills/TechniqueTable.cs"] = "the techniques' pick (Epic 11, D15): a band reached in practice offers its techniques; a draw, not an effect",
        };

        private static readonly string[] Projects = { "Simpiens.Core", "Simpiens.Presentation", "Simpiens.Client", "Simpiens.Hosting" };

        /// <summary>A learned read: <c>.Level(</c>, <c>.Band(</c> or <c>.Experience(</c> on anything but the seam itself.</summary>
        private static readonly Regex LearnedRead = new Regex(@"(?<!CrawlerSkills)\.(Level|Band|Experience)\(", RegexOptions.Compiled);

        [Fact]
        public void ProductionCodeReadsLearnedLevelsOnlyWhereAllowed()
        {
            string dotnet = FindDotnetFolder();
            var reading = new Dictionary<string, int>();
            foreach (var project in Projects)
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(dotnet, project), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(dotnet, file).Replace('\\', '/');
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    int reads = LearnedRead.Matches(File.ReadAllText(file)).Count;
                    if (reads > 0) reading[relative] = reads;
                }
            }

            var offenders = reading.Keys.Where(f => !Allowed.ContainsKey(f)).OrderBy(f => f).Select(f => $"{f} ({reading[f]})").ToList();
            var stale = Allowed.Keys.Where(f => !reading.ContainsKey(f)).OrderBy(f => f).ToList();
            Assert.True(offenders.Count == 0, "Read how good a crawler is at its work through CrawlerSkills.Level(skills, worn, SkillWork), not its learned level: " + string.Join("; ", offenders));
            Assert.True(stale.Count == 0, "These files no longer read learned levels; take them off the list: " + string.Join("; ", stale));
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
