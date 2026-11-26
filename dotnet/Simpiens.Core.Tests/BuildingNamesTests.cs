using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Building kinds are a table (Epic PLACES plan 1, B.1, D4): production code names a building kind (BuildingKind.X, other than
    /// None) only in the table and the layouts that stand buildings on the map, listed below with their reason. Everything else
    /// asks a building's row. A file that starts naming kinds fails, and so does an entry whose file no longer names any.
    /// </summary>
    public class BuildingNamesTests
    {
        private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
        {
            ["Simpiens.Core/Simulation/Places/BuildingTable.cs"] = "the table: the kinds' rows",
        };

        private static readonly string[] Projects = { "Simpiens.Core", "Simpiens.Presentation", "Simpiens.Client", "Simpiens.Hosting" };

        private static readonly Regex KindName = new Regex(@"\bBuildingKind\.(" + string.Join("|", Enum.GetNames(typeof(Simpiens.Simulation.Places.BuildingKind)).Where(n => n != "None")) + @")\b",
            RegexOptions.Compiled);

        [Fact]
        public void BuildingKindsAreNamedOnlyInTheirTableAndTheLayouts()
        {
            string dotnet = FindDotnetFolder();
            var naming = new Dictionary<string, List<string>>();
            foreach (var project in Projects)
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(dotnet, project), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(dotnet, file).Replace('\\', '/');
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    var names = KindName.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).Distinct().ToList();
                    if (names.Count > 0) naming[relative] = names;
                }
            }
            var offenders = naming.Keys.Where(f => !Allowed.ContainsKey(f)).OrderBy(f => f).Select(f => $"{f} ({string.Join(", ", naming[f])})").ToList();
            var stale = Allowed.Keys.Where(f => !naming.ContainsKey(f)).OrderBy(f => f).ToList();
            Assert.True(offenders.Count == 0, "Ask a building's row, not its kind, or list the file here with its reason: " + string.Join("; ", offenders));
            Assert.True(stale.Count == 0, "These files no longer name building kinds; take them off the list: " + string.Join("; ", stale));
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
