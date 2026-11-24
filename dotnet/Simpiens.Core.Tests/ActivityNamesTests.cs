using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Activities are rows (Epic LIFE-1 plan 2, L2.2, D12; the epic's A4): production code names an activity (Activity.X,
    /// other than None) only in the table and the places listed below with their reasons. What a venue offers is its data;
    /// what an activity does is its row. A file that starts naming activities fails, and so does an entry that no longer does.
    /// </summary>
    public class ActivityNamesTests
    {
        private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
        {
            ["Simpiens.Core/Simulation/Life/ActivityTable.cs"] = "the table: the activities' rows",
            ["Simpiens.Core/Cognition/AgentState.cs"] = "the hearth's relax fills fun at the fireside's rate (GatherAtAnchor's, not Recreate's)",
        };

        private static readonly string[] Projects = { "Simpiens.Core", "Simpiens.Presentation", "Simpiens.Client", "Simpiens.Hosting" };

        private static readonly Regex ActivityName = new Regex(@"\bActivity\.(" + string.Join("|", Enum.GetNames(typeof(Simpiens.Simulation.Life.Activity)).Where(n => n != "None")) + @")\b",
            RegexOptions.Compiled);

        [Fact]
        public void ActivitiesAreNamedOnlyInTheTableAndItsPlaces()
        {
            string dotnet = FindDotnetFolder();
            var naming = new Dictionary<string, List<string>>();
            foreach (var project in Projects)
                foreach (var file in Directory.EnumerateFiles(Path.Combine(dotnet, project), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(dotnet, file).Replace('\\', '/');
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    var names = ActivityName.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).Distinct().ToList();
                    if (names.Count > 0) naming[relative] = names;
                }

            var offenders = naming.Keys.Where(f => !Allowed.ContainsKey(f)).OrderBy(f => f).Select(f => $"{f} ({string.Join(", ", naming[f])})").ToList();
            var stale = Allowed.Keys.Where(f => !naming.ContainsKey(f)).OrderBy(f => f).ToList();
            Assert.True(offenders.Count == 0, "Name an activity in its row or a venue's data, and list the place here: " + string.Join("; ", offenders));
            Assert.True(stale.Count == 0, "These files no longer name activities; take them off the list: " + string.Join("; ", stale));
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
