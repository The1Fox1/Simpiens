using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The motive model is the one door to a goal's priority (Epic LIFE-1, L0.5, D2): production code calls a goal's
    /// <c>BasePriority</c> only in the model, so every choice is weighed by the shared factors. Validators may call it to pin a
    /// goal's own number; they are not production code.
    /// </summary>
    public class MotiveModelTests
    {
        private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
        {
            ["Simpiens.Core/Cognition/Planning/MotiveModel.cs"] = "the model: base times weight",
        };

        private static readonly string[] Projects = { "Simpiens.Core", "Simpiens.Presentation", "Simpiens.Client", "Simpiens.Hosting" };

        private static readonly Regex Call = new Regex(@"\.BasePriority\(", RegexOptions.Compiled);

        [Fact]
        public void OnlyTheModelReadsAGoalsBase()
        {
            string dotnet = FindDotnetFolder();
            var calling = new List<string>();
            foreach (var project in Projects)
                foreach (var file in Directory.EnumerateFiles(Path.Combine(dotnet, project), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(dotnet, file).Replace('\\', '/');
                    if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                    if (Call.IsMatch(File.ReadAllText(file))) calling.Add(relative);
                }
            var offenders = calling.Where(f => !Allowed.ContainsKey(f)).OrderBy(f => f).ToList();
            var stale = Allowed.Keys.Where(f => !calling.Contains(f)).ToList();
            Assert.True(offenders.Count == 0, "Reach a goal's priority through MotiveModel, not its base: " + string.Join("; ", offenders));
            Assert.True(stale.Count == 0, "These files no longer read a base; take them off the list: " + string.Join("; ", stale));
        }

        private static string FindDotnetFolder()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Simpiens.slnx"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }
    }
}
