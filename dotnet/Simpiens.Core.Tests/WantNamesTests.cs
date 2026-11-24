using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Wants are rows (Epic LIFE-1 plan 2, W.1, D20): production code names a want (Want.X, other than None) only in the table
    /// and the places listed below with their reasons. What makes one arise is its trigger; what it is for, its target.
    /// </summary>
    public class WantNamesTests
    {
        private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
        {
            ["Simpiens.Core/Simulation/Life/WantTable.cs"] = "the table: the wants' rows",
            ["Simpiens.Core/Simulation/Life/WantRules.cs"] = "a want to see a friend comes true when they meet",
        };

        private static readonly string[] Projects = { "Simpiens.Core", "Simpiens.Presentation", "Simpiens.Client", "Simpiens.Hosting" };

        private static readonly Regex WantName = new Regex(@"\bWant\.(" + string.Join("|", Enum.GetNames(typeof(Simpiens.Simulation.Life.Want)).Where(n => n != "None")) + @")\b", RegexOptions.Compiled);

        [Fact]
        public void WantsAreNamedOnlyInTheTableAndItsPlaces()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Simpiens.slnx"))) dir = dir.Parent;
            Assert.NotNull(dir);
            var naming = new Dictionary<string, List<string>>();
            foreach (var project in Projects)
                foreach (var file in Directory.EnumerateFiles(Path.Combine(dir.FullName, project), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(dir.FullName, file).Replace('\\', '/');
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    var names = WantName.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).Distinct().ToList();
                    if (names.Count > 0) naming[relative] = names;
                }
            var offenders = naming.Keys.Where(f => !Allowed.ContainsKey(f)).OrderBy(f => f).Select(f => $"{f} ({string.Join(", ", naming[f])})").ToList();
            var stale = Allowed.Keys.Where(f => !naming.ContainsKey(f)).ToList();
            Assert.True(offenders.Count == 0, "Name a want in its row, and list any other place here: " + string.Join("; ", offenders));
            Assert.True(stale.Count == 0, "These files no longer name wants: " + string.Join("; ", stale));
        }
    }
}
