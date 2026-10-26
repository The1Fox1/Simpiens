using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Code asks for item groups, not items (.agents/item_system_plan.md §3.10): production code names an item
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
            // Quirks kept on purpose (the plan's §5)
            ["Simpiens.Core/Simulation/ResourceData.cs"] = "§5: a node's default yield is a ration",
            ["Simpiens.Core/Cognition/Planning/Handlers/HarvestResourceIntentHandler.cs"] = "§5: an untyped node yields a ration",
            ["Simpiens.Core/Cognition/AgentState.cs"] = "§5: HasFoodInInventory packs a ration when set",
            // I6, the client
            ["Simpiens.Presentation/ProceduralSprites.cs"] = "I6: sprites by key",
            ["Simpiens.Presentation/Dashboard/MerchantModel.cs"] = "I6: the merchant's rows",
            ["Simpiens.Presentation/Dashboard/HeaderModel.cs"] = "I6: the header's stock",
            ["Simpiens.Presentation/Dashboard/BountyBoardModel.cs"] = "I6: the bounty board's items",
            ["Simpiens.Client/Ui/BountyBoardWindow.cs"] = "I6: the bounty board's items",
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
