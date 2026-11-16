using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Code asks the slot table, not a slot (Epic 10 Part 3, K1.6): production code names an equipment slot or a slot kind
    /// (EquipmentSlot.X, SlotKind.X, other than None) only in the files below, each with its reason, so a new slot is a row
    /// and its items, with no code of its own (AGENTS.md, "Adding an equipment slot"). The list only shrinks: a file that
    /// starts naming slots fails, and so does an entry whose file no longer names any.
    /// </summary>
    public class SlotNamesTests
    {
        private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
        {
            ["Simpiens.Core/Simulation/Items/EquipmentSlots.cs"] = "the table: the slots' rows",
            ["Simpiens.Core/Simulation/Items/ItemCatalog.cs"] = "the catalog: the kind of slot each piece fits",
        };

        private static readonly string[] Projects = { "Simpiens.Core", "Simpiens.Presentation", "Simpiens.Client", "Simpiens.Hosting" };

        private static readonly Regex SlotName = new Regex(@"\b(EquipmentSlot\.(" + string.Join("|", Enum.GetNames(typeof(Simpiens.Simulation.Items.EquipmentSlot)))
            + @")|SlotKind\.(" + string.Join("|", Enum.GetNames(typeof(Simpiens.Simulation.Items.SlotKind)).Where(n => n != "None")) + @"))\b", RegexOptions.Compiled);

        [Fact]
        public void ProductionCodeNamesSlotsOnlyWhereAllowed()
        {
            string dotnet = FindDotnetFolder();
            var naming = new Dictionary<string, List<string>>();
            foreach (var project in Projects)
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(dotnet, project), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(dotnet, file).Replace('\\', '/');
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    var names = SlotName.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).Distinct().ToList();
                    if (names.Count > 0) naming[relative] = names;
                }
            }

            var offenders = naming.Keys.Where(f => !Allowed.ContainsKey(f)).OrderBy(f => f).Select(f => $"{f} ({string.Join(", ", naming[f])})").ToList();
            var stale = Allowed.Keys.Where(f => !naming.ContainsKey(f)).OrderBy(f => f).ToList();
            Assert.True(offenders.Count == 0, "Ask the slot table (EquipmentSlots, WornGear, Outfitting), not a slot: " + string.Join("; ", offenders));
            Assert.True(stale.Count == 0, "These files no longer name slots; take them off the list: " + string.Join("; ", stale));
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
