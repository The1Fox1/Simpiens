using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Effects read a crawler's techniques through the one door (Epic 11, S4.3, A1, D14): production code reads what a
    /// crawler holds (<c>.TechniqueIn(</c>) or what a technique does (<c>.ValueOf(</c>) only in the files
    /// below, each with its reason. Every effect asks <c>CrawlerTechniques.Get</c> for a stat by the kind of work, so a
    /// technique added as a row changes what it says everywhere at once, within its bounds. A file that starts reading
    /// techniques fails, and so does an entry whose file no longer reads any.
    /// </summary>
    public class TechniqueReadsTests
    {
        private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
        {
            ["Simpiens.Core/Simulation/Skills/TechniqueTable.cs"] = "the table and its rules: a row's values and words, the pick filling empty slots",
            ["Simpiens.Core/Simulation/Skills/CrawlerTechniques.cs"] = "the door: a stat summed over the techniques held",
            ["Simpiens.Core/Simulation/Guild/SponsorRules.cs"] = "sponsorship's rule: whether the crawler holds the technique already",
        };

        private static readonly string[] Projects = { "Simpiens.Core", "Simpiens.Presentation", "Simpiens.Client", "Simpiens.Hosting" };

        /// <summary>A technique read: what a crawler holds, or a row's values.</summary>
        private static readonly Regex TechniqueRead = new Regex(@"\.(TechniqueIn|ValueOf)\(", RegexOptions.Compiled);

        [Fact]
        public void ProductionCodeReadsTechniquesOnlyThroughTheDoor()
        {
            string dotnet = FindDotnetFolder();
            var reading = new Dictionary<string, int>();
            foreach (var project in Projects)
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(dotnet, project), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(dotnet, file).Replace('\\', '/');
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    int reads = TechniqueRead.Matches(File.ReadAllText(file)).Count;
                    if (reads > 0) reading[relative] = reads;
                }
            }
            var offenders = reading.Keys.Where(f => !Allowed.ContainsKey(f)).OrderBy(f => f).Select(f => $"{f} ({reading[f]})").ToList();
            var stale = Allowed.Keys.Where(f => !reading.ContainsKey(f)).OrderBy(f => f).ToList();
            Assert.True(offenders.Count == 0, "Read a technique's effect through CrawlerTechniques.Get(traits, skills, work, stat), or list the file here with its reason: " + string.Join("; ", offenders));
            Assert.True(stale.Count == 0, "These files no longer read techniques; take them off the list: " + string.Join("; ", stale));
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
