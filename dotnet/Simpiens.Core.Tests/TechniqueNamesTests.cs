using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Techniques are a table (Epic 11, S4.1, A2, D13): production code names a technique (Technique.X, other than None) only
    /// in the table and the files listed below with their reason. Code reads a technique's effects by stat through the
    /// door (<c>CrawlerTechniques</c>), never by naming the technique, so a row added needs no code. A file that starts
    /// naming techniques fails, and so does an entry whose file no longer names any.
    /// </summary>
    public class TechniqueNamesTests
    {
        private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
        {
            ["Simpiens.Core/Simulation/Skills/TechniqueTable.cs"] = "the table: the techniques' rows",
        };

        private static readonly string[] Projects = { "Simpiens.Core", "Simpiens.Presentation", "Simpiens.Client", "Simpiens.Hosting" };

        private static readonly Regex TechniqueName = new Regex(@"\bTechnique\.(" + string.Join("|", Enum.GetNames(typeof(Simpiens.Simulation.Skills.Technique)).Where(n => n != "None")) + @")\b",
            RegexOptions.Compiled);

        [Fact]
        public void TechniquesAreNamedOnlyInTheirTable()
        {
            string dotnet = FindDotnetFolder();
            var naming = new Dictionary<string, List<string>>();
            foreach (var project in Projects)
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(dotnet, project), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(dotnet, file).Replace('\\', '/');
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    var names = TechniqueName.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).Distinct().ToList();
                    if (names.Count > 0) naming[relative] = names;
                }
            }
            var offenders = naming.Keys.Where(f => !Allowed.ContainsKey(f)).OrderBy(f => f).Select(f => $"{f} ({string.Join(", ", naming[f])})").ToList();
            var stale = Allowed.Keys.Where(f => !naming.ContainsKey(f)).OrderBy(f => f).ToList();
            Assert.True(offenders.Count == 0, "Read a technique's effects by stat, not by name, or list the file here with its reason: " + string.Join("; ", offenders));
            Assert.True(stale.Count == 0, "These files no longer name techniques; take them off the list: " + string.Join("; ", stale));
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
