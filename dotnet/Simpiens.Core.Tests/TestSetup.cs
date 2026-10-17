using System;
using System.IO;
using System.Runtime.CompilerServices;
using Simpiens.Testing;
using Xunit;

// The simulation has process-wide state (SimLog, the agent-name table, the economy tuning cognition reads), as it
// does in Unity, where tests also run one at a time
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Simpiens.Core.Tests
{
    internal static class TestSetup
    {
        /// <summary>Validator output goes to the test's own output (shown for a failing test).</summary>
        [ModuleInitializer]
        internal static void RouteTestLog()
        {
            TestLog.InfoSink = Write;
            TestLog.WarningSink = message => Write("warning: " + message);
            TestLog.ErrorSink = message => Write("error: " + message);
        }

        private static void Write(string message) => TestContext.Current?.TestOutputHelper?.WriteLine(message ?? string.Empty);
    }

    /// <summary>Paths in the repository (tests run from dotnet/*/bin/...).</summary>
    internal static class Repo
    {
        private static readonly Lazy<string> RootPath = new Lazy<string>(() =>
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Baselines", "golden-world-hash.txt"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Can't find the repository root (Baselines/golden-world-hash.txt) above " + AppContext.BaseDirectory);
        });

        public static string Root => RootPath.Value;

        public static string GoldenBaseline => Path.Combine(Root, Simpiens.Testing.Environment.GoldenWorldHash.RelativePath);
    }
}
