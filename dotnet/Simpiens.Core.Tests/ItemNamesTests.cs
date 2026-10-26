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
            // Quirks kept on purpose (the plan's §5)
            ["Simpiens.Core/Simulation/ResourceData.cs"] = "§5: a node's default yield is a ration",
            ["Simpiens.Core/Cognition/Planning/Handlers/HarvestResourceIntentHandler.cs"] = "§5: an untyped node yields a ration",
            ["Simpiens.Core/Cognition/AgentState.cs"] = "§5: HasFoodInInventory packs a ration when set",
            // I5, the stall, the caravan and prices
            ["Simpiens.Core/Simulation/Economy/TradeStallData.cs"] = "I5: per-item stall fields",
            ["Simpiens.Core/Simulation/Economy/EconomyTuning.cs"] = "I5: per-item terms",
            ["Simpiens.Core/Simulation/SimulationManager.cs"] = "I5: a Gather bounty's default item",
            ["Simpiens.Core/Cognition/Planning/Handlers/TradeIntentHandler.cs"] = "I5: what the stall sells",
            ["Simpiens.Core/Cognition/Planning/Handlers/DeliverBountyIntentHandler.cs"] = "I5: delivered goods to the stall's shelves",
            ["Simpiens.Core/Cognition/Planning/Goals/PurchaseProvisionsGoal.cs"] = "I5: the food the stall sells",
            ["Simpiens.Core/Cognition/Planning/Actions/BuyProvisionsAction.cs"] = "I5: the food the stall sells",
            ["Simpiens.Core/Cognition/Planning/Goals/OutfitForDangerGoal.cs"] = "I5: the protection the stall sells",
            ["Simpiens.Core/Cognition/Planning/Actions/BuyHazardGearAction.cs"] = "I5: the protection the stall sells",
            // I6, the client
            ["Simpiens.Presentation/ProceduralSprites.cs"] = "I6: sprites by key",
            ["Simpiens.Presentation/Dashboard/BountyBoardModel.cs"] = "I6: the bounty board's items",
            ["Simpiens.Client/Ui/BountyBoardWindow.cs"] = "I6: the bounty board's items",
        };

        private static readonly string[] Projects = { "Simpiens.Core", "Simpiens.Presentation", "Simpiens.Client", "Simpiens.Hosting" };
        private static readonly Regex ItemName = new Regex(@"\bItemType\.(?!None\b)([A-Z]\w*)", RegexOptions.Compiled);

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
