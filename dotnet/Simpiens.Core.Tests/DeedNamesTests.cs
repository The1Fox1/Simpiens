using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Deeds are counted where they happen (Epic 11, S0.5, A7, D2): production code names a deed (Deed.X, other than None)
    /// only in the table and at the place each is counted, listed below with its reason, and every deed in the table is
    /// counted somewhere. A file that starts naming deeds fails, and so does an entry whose file no longer names any, or a
    /// deed nobody counts. Deeds are never rebuilt from the chronicle.
    /// </summary>
    public class DeedNamesTests
    {
        private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
        {
            ["Simpiens.Core/Simulation/Skills/DeedTable.cs"] = "the table: the deeds' rows",
            ["Simpiens.Core/Simulation/SimulationManager.cs"] = "a kill, a boss's and a named creature's (the creatures' world), a downing survived (the bleeding clock stopped), a crawler taught (a mentor's first lesson)",
            ["Simpiens.Core/Cognition/Planning/Handlers/CarryIntentHandler.cs"] = "a rescue: carried out, or stood up with a medicine",
            ["Simpiens.Core/Simulation/Workshops/WorkshopService.cs"] = "a make finished, and a masterpiece made (Epic 11, S8.2)",
            ["Simpiens.Core/Simulation/Economy/Market.cs"] = "a hunt's share paid",
            ["Simpiens.Core/Simulation/Guild/GuildService.Expeditions.cs"] = "home from a won clear",
            ["Simpiens.Core/Simulation/Skills/EpithetTable.cs"] = "the epithets' table (Epic 11, S6.1): each row names the deed whose count earns it; it counts none",
            ["Simpiens.Core/Simulation/Skills/EarnedTraitTable.cs"] = "the earned traits' table (Epic 11, S6.3): each row names the deed whose count earns it; it counts none",
        };

        private static readonly string[] Projects = { "Simpiens.Core", "Simpiens.Presentation", "Simpiens.Client", "Simpiens.Hosting" };

        private static readonly Regex DeedName = new Regex(@"\bDeed\.(" + string.Join("|", Enum.GetNames(typeof(Simpiens.Simulation.Skills.Deed)).Where(n => n != "None")) + @")\b",
            RegexOptions.Compiled);

        [Fact]
        public void DeedsAreNamedOnlyWhereTheyAreCounted()
        {
            string dotnet = FindDotnetFolder();
            var naming = new Dictionary<string, List<string>>();
            foreach (var project in Projects)
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(dotnet, project), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(dotnet, file).Replace('\\', '/');
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    var names = DeedName.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).Distinct().ToList();
                    if (names.Count > 0) naming[relative] = names;
                }
            }

            var offenders = naming.Keys.Where(f => !Allowed.ContainsKey(f)).OrderBy(f => f).Select(f => $"{f} ({string.Join(", ", naming[f])})").ToList();
            var stale = Allowed.Keys.Where(f => !naming.ContainsKey(f)).OrderBy(f => f).ToList();
            var counted = naming.Where(kv => kv.Key != "Simpiens.Core/Simulation/Skills/DeedTable.cs" && kv.Key != "Simpiens.Core/Simulation/Skills/EpithetTable.cs" && kv.Key != "Simpiens.Core/Simulation/Skills/EarnedTraitTable.cs").SelectMany(kv => kv.Value).ToHashSet(); // the tables name deeds; they count none
            var uncounted = Simpiens.Simulation.Skills.DeedTable.All.Select(d => d.Id.ToString()).Where(n => !counted.Contains(n)).ToList();
            Assert.True(offenders.Count == 0, "Count a deed where it happens, and list the place here: " + string.Join("; ", offenders));
            Assert.True(stale.Count == 0, "These files no longer name deeds; take them off the list: " + string.Join("; ", stale));
            Assert.True(uncounted.Count == 0, "These deeds are counted nowhere: " + string.Join(", ", uncounted));
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
