using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Epithets are a table (Epic 11, S6.1, A2, D27): production code names an epithet (Epithet.X, other than None) only in the
    /// table and the files listed below with their reason. Code judges and words an epithet through its rule
    /// (<c>EpithetRules</c>), never by naming it, so a row added needs no code. A file that starts naming epithets fails, and
    /// so does an entry whose file no longer names any.
    /// </summary>
    public class EpithetNamesTests
    {
        private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
        {
            ["Simpiens.Core/Simulation/Skills/EpithetTable.cs"] = "the table: the epithets' rows",
        };

        private static readonly string[] Projects = { "Simpiens.Core", "Simpiens.Presentation", "Simpiens.Client", "Simpiens.Hosting" };

        private static readonly Regex EpithetName = new Regex(@"\bEpithet\.(" + string.Join("|", Enum.GetNames(typeof(Simpiens.Simulation.Skills.Epithet)).Where(n => n != "None")) + @")\b",
            RegexOptions.Compiled);

        [Fact]
        public void EpithetsAreNamedOnlyInTheirTable()
        {
            string dotnet = FindDotnetFolder();
            var naming = new Dictionary<string, List<string>>();
            foreach (var project in Projects)
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(dotnet, project), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(dotnet, file).Replace('\\', '/');
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    var names = EpithetName.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).Distinct().ToList();
                    if (names.Count > 0) naming[relative] = names;
                }
            }
            var offenders = naming.Keys.Where(f => !Allowed.ContainsKey(f)).OrderBy(f => f).Select(f => $"{f} ({string.Join(", ", naming[f])})").ToList();
            var stale = Allowed.Keys.Where(f => !naming.ContainsKey(f)).OrderBy(f => f).ToList();
            Assert.True(offenders.Count == 0, "Judge and word an epithet through its rule, not by name, or list the file here with its reason: " + string.Join("; ", offenders));
            Assert.True(stale.Count == 0, "These files no longer name epithets; take them off the list: " + string.Join("; ", stale));
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
