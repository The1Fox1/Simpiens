using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Looks change no play in Epic UI-Beta (Q5): a crawler's look is drawn, saved and shown, and no decision, path or price
    /// reads it. In the core, only the files below touch a look, each with its reason. Lineage traits, a design of their
    /// own, lift this on purpose: a file that starts reading looks fails, and so does an entry whose file no longer does.
    /// </summary>
    public class CognitionReadsNoLookTests
    {
        private static readonly Dictionary<string, string> Allowed = new Dictionary<string, string>
        {
            ["Simpiens.Core/Cognition/AgentState.cs"] = "holds the look, and copies it into and out of the save record",
            ["Simpiens.Core/Session/SessionHost.cs"] = "draws each crawler's look and name at spawn",
            ["Simpiens.Core/Session/SessionHost.Drifters.cs"] = "keeps departed crawlers' looks, and saves them",
            ["Simpiens.Core/Simulation/Persistence/CrawlerSave.cs"] = "the save record",
            ["Simpiens.Core/Simulation/Persistence/SessionSave.cs"] = "the departed crawlers' save record",
        };

        // A look's type, its namespace, or a read of a member named Look (SimRandomSalt.Look is a salt's name, not a read)
        private static readonly Regex ReadsALook = new Regex(@"\bCrawlerLook\b|\bSimulation\.People\b|(?<!SimRandomSalt)\.Look\b", RegexOptions.Compiled);

        [Fact]
        public void OnlyTheseCoreFilesTouchALook()
        {
            string dotnet = FindDotnetFolder();
            string core = Path.Combine(dotnet, "Simpiens.Core");
            var touching = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(dotnet, file).Replace('\\', '/');
                if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                if (relative.StartsWith("Simpiens.Core/Simulation/People/", StringComparison.Ordinal)) continue; // the look itself
                if (ReadsALook.IsMatch(File.ReadAllText(file))) touching.Add(relative);
            }

            var offenders = touching.Where(f => !Allowed.ContainsKey(f)).ToList();
            var stale = Allowed.Keys.Where(f => !touching.Contains(f)).OrderBy(f => f).ToList();
            Assert.True(offenders.Count == 0, "Looks change no play until lineage traits are designed (Epic UI-Beta Q5): " + string.Join("; ", offenders));
            Assert.True(stale.Count == 0, "These files no longer touch a look; take them off the list: " + string.Join("; ", stale));
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
