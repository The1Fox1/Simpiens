using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Simpiens.Testing;
using Xunit;

// One GL context at a time, and the simulation's process-wide state (SimLog, the economy tuning): tests run one by one
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Simpiens.Client.Tests
{
    internal static class TestSetup
    {
        /// <summary>
        /// Before SDL starts: the offscreen video driver (no window on anyone's screen, no display needed) and Mesa's
        /// software renderer, so a render here matches one in CI pixel for pixel (epic-client F1). Either can be
        /// overridden from the environment.
        /// </summary>
        [ModuleInitializer]
        internal static void Initialize()
        {
            DefaultNativeEnvironment("SDL_VIDEODRIVER", "offscreen");
            DefaultNativeEnvironment("LIBGL_ALWAYS_SOFTWARE", "1");

            TestLog.InfoSink = Write;
            TestLog.WarningSink = message => Write("warning: " + message);
            TestLog.ErrorSink = message => Write("error: " + message);
        }

        private static void Write(string message) => TestContext.Current?.TestOutputHelper?.WriteLine(message ?? string.Empty);

        /// <summary>
        /// Sets a variable in the process's native environment unless it is already set. SDL and Mesa read it with
        /// getenv, which never sees <see cref="Environment.SetEnvironmentVariable(string, string)"/> on Unix: .NET
        /// keeps its own copy there.
        /// </summary>
        private static void DefaultNativeEnvironment(string name, string value)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name))) return;
            Environment.SetEnvironmentVariable(name, value);
            if (!OperatingSystem.IsWindows()) setenv(name, value, 0);
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int setenv(string name, string value, int overwrite);
    }

    /// <summary>
    /// Games run offscreen in the test process, a frame at a time (<c>RunOneFrame</c>), and are never disposed:
    /// disposing segfaults under the offscreen driver (epic-client F1). They are kept referenced so that no finalizer
    /// tears one down mid-run either.
    /// </summary>
    internal static class Offscreen
    {
        private static readonly List<SimpiensGame> Alive = new List<SimpiensGame>();

        public static SimpiensGame Start(ClientOptions options = null, int frames = 1)
        {
            options ??= new ClientOptions();
            options.Width = 320;
            options.Height = 200;
            options.VSync = false;
            options.ViewClockFromTicks = true; // renders at a given tick are always the same
            var game = new SimpiensGame(options);
            Alive.Add(game);
            for (int i = 0; i < frames; i++) game.RunOneFrame();
            return game;
        }
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
