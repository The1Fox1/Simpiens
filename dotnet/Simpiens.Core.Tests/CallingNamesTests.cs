using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Callings are a table (Epic 11, S5.2, A2, D20): production code names a calling (Calling.X, other than None) only in the
    /// table and the files listed below with their reason. The guild's orders and the creatures' rule read a calling's role
    /// (<c>PartyRole</c>), never its name, so a calling added as a row needs no code. A file that starts naming callings fails,
    /// and so does an entry whose file no longer names any.
    /// </summary>
    public class CallingNamesTests
    {
        private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
        {
            ["Simpiens.Core/Simulation/Skills/CallingTable.cs"] = "the table: the callings' rows",
        };

        private static readonly string[] Projects = { "Simpiens.Core", "Simpiens.Presentation", "Simpiens.Client", "Simpiens.Hosting" };

        private static readonly Regex CallingName = new Regex(@"\bCalling\.(" + string.Join("|", Enum.GetNames(typeof(Simpiens.Simulation.Skills.Calling)).Where(n => n != "None")) + @")\b",
            RegexOptions.Compiled);

        [Fact]
        public void CallingsAreNamedOnlyInTheirTable()
        {
            string dotnet = FindDotnetFolder();
            var naming = new Dictionary<string, List<string>>();
            foreach (var project in Projects)
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(dotnet, project), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(dotnet, file).Replace('\\', '/');
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    var names = CallingName.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).Distinct().ToList();
                    if (names.Count > 0) naming[relative] = names;
                }
            }
            var offenders = naming.Keys.Where(f => !Allowed.ContainsKey(f)).OrderBy(f => f).Select(f => $"{f} ({string.Join(", ", naming[f])})").ToList();
            var stale = Allowed.Keys.Where(f => !naming.ContainsKey(f)).OrderBy(f => f).ToList();
            Assert.True(offenders.Count == 0, "Read a calling's role or row, not its name, or list the file here with its reason: " + string.Join("; ", offenders));
            Assert.True(stale.Count == 0, "These files no longer name callings; take them off the list: " + string.Join("; ", stale));
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
