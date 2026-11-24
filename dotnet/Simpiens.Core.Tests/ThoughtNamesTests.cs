using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Thoughts are thought where things happen (Epic LIFE-1, L1.2, the epic's A5): production code names a thought
    /// (Thought.X, other than None) only in the table and at the place each is thought, listed below with its reason, and
    /// every thought in the table is thought somewhere. A file that starts naming thoughts fails, and so does an entry whose
    /// file no longer names any, or a thought nobody thinks. Thoughts are never rebuilt from the chronicle.
    /// </summary>
    public class ThoughtNamesTests
    {
        private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
        {
            ["Simpiens.Core/Simulation/Life/ThoughtTable.cs"] = "the table: the thoughts' rows",
            ["Simpiens.Core/Cognition/AgentConsequenceService.cs"] = "a good meal, a meal at the Garden, waking in a bunk",
            ["Simpiens.Core/Cognition/AgentRuntime.cs"] = "a collapse from exhaustion, a name earned (told with the legend)",
            ["Simpiens.Core/Simulation/Social/SocialDynamics.cs"] = "a good talk, for both",
            ["Simpiens.Core/Cognition/Planning/Handlers/ShareResourceIntentHandler.cs"] = "food shared: the giver's and the fed's",
            ["Simpiens.Core/Simulation/Guild/GuildService.cs"] = "a member's wage: paid in full, below its ask, unpaid",
            ["Simpiens.Core/Simulation/Workshops/WorkshopService.cs"] = "an artisan's wage, as a member's; a masterpiece made",
            ["Simpiens.Core/Cognition/Planning/Handlers/DeliverBountyIntentHandler.cs"] = "a bounty paid",
            ["Simpiens.Core/Simulation/Economy/Market.cs"] = "a hunt's share paid",
            ["Simpiens.Core/Simulation/Guild/GuildService.Expeditions.cs"] = "an expedition's share paid",
            ["Simpiens.Core/Cognition/Planning/Handlers/HarvestResourceIntentHandler.cs"] = "a contested node lost to a rival",
            ["Simpiens.Core/Simulation/SimulationManager.cs"] = "a downing survived, a kill, a band reached, a creature's blow",
            ["Simpiens.Core/Simulation/SimulationManager.Lodging.cs"] = "a friend's death, for each who grieves",
            ["Simpiens.Core/Cognition/Planning/Handlers/CarryIntentHandler.cs"] = "a rescue: the rescuer's and the rescued's",
            ["Simpiens.Core/Simulation/Life/WantTable.cs"] = "each want's thoughts, thought when it comes true or runs out (plan 2, W.1, D20)",
            ["Simpiens.Core/Cognition/AgentState.cs"] = "a want to see a friend come true, naming it (plan 2, W.3)",
            ["Simpiens.Core/Simulation/Life/ActivityTable.cs"] = "each activity's thought, thought at a spell's end (plan 2, L2.2, D12)",
            ["Simpiens.Presentation/Art/People/MoodRule.cs"] = "reads a friend's death, held, for the grieving face (Epic LIFE-1, L1.6); thinks nothing",
        };

        private static readonly string[] Projects = { "Simpiens.Core", "Simpiens.Presentation", "Simpiens.Client", "Simpiens.Hosting" };

        private static readonly Regex ThoughtName = new Regex(@"\bThought\.(" + string.Join("|", Enum.GetNames(typeof(Simpiens.Simulation.Life.Thought)).Where(n => n != "None")) + @")\b",
            RegexOptions.Compiled);

        [Fact]
        public void ThoughtsAreNamedOnlyWhereTheyAreThought()
        {
            string dotnet = FindDotnetFolder();
            var naming = new Dictionary<string, List<string>>();
            foreach (var project in Projects)
                foreach (var file in Directory.EnumerateFiles(Path.Combine(dotnet, project), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(dotnet, file).Replace('\\', '/');
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    var names = ThoughtName.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).Distinct().ToList();
                    if (names.Count > 0) naming[relative] = names;
                }

            var offenders = naming.Keys.Where(f => !Allowed.ContainsKey(f)).OrderBy(f => f).Select(f => $"{f} ({string.Join(", ", naming[f])})").ToList();
            var stale = Allowed.Keys.Where(f => !naming.ContainsKey(f)).OrderBy(f => f).ToList();
            var thought = naming.Where(kv => kv.Key != "Simpiens.Core/Simulation/Life/ThoughtTable.cs" && kv.Key != "Simpiens.Presentation/Art/People/MoodRule.cs").SelectMany(kv => kv.Value).ToHashSet(); // the table and the face read thoughts; they think none
            var unthought = Simpiens.Simulation.Life.ThoughtTable.All.Select(t => t.Id.ToString()).Where(n => !thought.Contains(n)).ToList();
            Assert.True(offenders.Count == 0, "Think a thought where its event happens, and list the place here: " + string.Join("; ", offenders));
            Assert.True(stale.Count == 0, "These files no longer name thoughts; take them off the list: " + string.Join("; ", stale));
            Assert.True(unthought.Count == 0, "These thoughts are thought nowhere: " + string.Join(", ", unthought));
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
