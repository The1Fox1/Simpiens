using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Code asks for item groups, not items (the item system refactor, 2026-10-26): production code names an item
    /// (ItemType.X, other than None) only in the files below, each with its reason. The list only shrinks: a file that
    /// starts naming items fails, and so does an entry whose file no longer names any (take it off the list).
    /// </summary>
    public class ItemNamesTests
    {
        private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
        {
            // Content: where items are defined and placed
            ["Simpiens.Core/Simulation/Items/ItemCatalog.cs"] = "the catalog: the items' definitions",
            ["Simpiens.Core/Session/Layouts/Floor1Layout.cs"] = "the map: what each node yields",
            ["Simpiens.Core/Session/OutpostSession.cs"] = "the session: its seeded Gather bounty's item",
            ["Simpiens.Core/Simulation/Economy/EconomyTuning.cs"] = "the default economy: each item's base value and stall terms",
            ["Simpiens.Core/Simulation/Economy/SupplyTuning.cs"] = "the default caravan: what it exports and supplies",
            ["Simpiens.Hosting/EconomySoak.cs"] = "the soak's player profiles: the prices each one sets",
            ["Simpiens.Presentation/ItemTexts.cs"] = "the client's words for items: merchant rows, stock and header",
            // Quirks kept on purpose (.agents/core_refactor_plan.md, R7)
            ["Simpiens.Core/Simulation/ResourceData.cs"] = "quirk: a node's default yield is a ration",
            ["Simpiens.Core/Cognition/Planning/Handlers/HarvestResourceIntentHandler.cs"] = "quirk: an untyped node yields a ration",
            ["Simpiens.Core/Cognition/AgentState.cs"] = "quirk: HasFoodInInventory packs a ration when set",
        };

        private static readonly string[] Projects = { "Simpiens.Core", "Simpiens.Presentation", "Simpiens.Client", "Simpiens.Hosting" };
        // ItemType's members (None aside), so a property named ItemType (trade.ItemType.IsSalvage()) doesn't match and a
        // qualified name (Simpiens.Simulation.Economy.ItemType.Ration) does
        private static readonly Regex ItemName = new Regex(@"\bItemType\.(" + string.Join("|",
            Enum.GetNames(typeof(Simpiens.Simulation.Economy.ItemType)).Where(n => n != "None")) + @")\b", RegexOptions.Compiled);

        [Fact]
        public void ProductionCodeNamesItemsOnlyWhereAllowed()
        {
            string dotnet = FindDotnetFolder();
            var naming = new Dictionary<string, List<string>>();
            foreach (var project in Projects)
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(dotnet, project), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(dotnet, file).Replace('\\', '/');
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    var names = ItemName.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).Distinct().ToList();
                    if (names.Count > 0) naming[relative] = names;
                }
            }

            var offenders = naming.Keys.Where(f => !Allowed.ContainsKey(f)).OrderBy(f => f).Select(f => $"{f} ({string.Join(", ", naming[f])})").ToList();
            var stale = Allowed.Keys.Where(f => !naming.ContainsKey(f)).OrderBy(f => f).ToList();
            Assert.True(offenders.Count == 0, "Name item groups, not items (ItemCatalog.InCategory, InventoryGroups): " + string.Join("; ", offenders));
            Assert.True(stale.Count == 0, "These files no longer name items; take them off the list: " + string.Join("; ", stale));
        }

        private static string FindDotnetFolder()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Simpiens.slnx"))) return dir.FullName;
            }
            throw new InvalidOperationException("dotnet/Simpiens.slnx not found above the test's folder.");
        }
    }
}
