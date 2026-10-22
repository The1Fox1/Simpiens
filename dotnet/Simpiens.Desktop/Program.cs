using System;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Simpiens.Client;
using Simpiens.Diagnostics;
using Simpiens.Hosting;

namespace Simpiens.Desktop
{
    /// <summary>The desktop game: <c>dotnet run --project dotnet/Simpiens.Desktop -- [options]</c>.</summary>
    public static class Program
    {
        private const string Usage = @"Simpiens (desktop)

  --seed N             simulation seed (default: the default seed)
  --session ID         session to play (default: the outpost)
  --view iso|top       isometric, the main view (default), or top-down; V switches in the game
  --width W --height H window size (default: 1280 x 720)
  --no-vsync           don't wait for the display's refresh
  --ticks-per-frame N  run N ticks every frame instead of following real time (fast-forward)
  --help               this text

Logging: warnings and errors go to standard error. SIMPIENS_LOG, SIMPIENS_LOG_FILE and SIMPIENS_LOG_FOCUS choose
more (AGENTS.md, Logging).";

        [STAThread]
        public static int Main(string[] args)
        {
            var options = new ClientOptions();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--seed": options.Seed = ulong.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                    case "--session": options.SessionId = Next(args, ref i); break;
                    case "--view": options.View = ParseView(Next(args, ref i)); break;
                    case "--width": options.Width = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                    case "--height": options.Height = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                    case "--no-vsync": options.VSync = false; break;
                    case "--ticks-per-frame": options.TicksPerUpdate = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                    case "--help":
                        Console.WriteLine(Usage);
                        return 0;
                    default:
                        Console.Error.WriteLine($"Unknown option '{args[i]}'.\n\n{Usage}");
                        return 64;
                }
            }

#if DEBUG
            // Development builds offer the Dev Map (the canary pens) through the session switch, as Unity's editor did,
            // and the demonstration cutscenes (F9, and dynamic scenes over play)
            options.DemoScenes = true;
            options.ConfigureServices = services => Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<Simpiens.Core.Session.ISessionDefinition>(services, new Simpiens.Testing.Sessions.CanaryPenSession());
#endif
            ConsoleSimLogSink.Install();
            var logging = SimLogSettings.FromEnvironment();
            if (Environment.GetEnvironmentVariable("SIMPIENS_LOG") == null) logging.ConsoleMinimumLevel = LogLevel.Warning;
            SimLog.Configure(logging);
            try
            {
                using var game = new SimpiensGame(options);
                game.Run();
            }
            finally
            {
                SimLog.Shutdown();
            }
            return 0;
        }

        private static Simpiens.Presentation.ViewMode ParseView(string value)
        {
            switch (value)
            {
                case "iso": case "isometric": return Simpiens.Presentation.ViewMode.Isometric;
                case "top": case "top-down": return Simpiens.Presentation.ViewMode.TopDown;
                default: throw new System.ArgumentException($"--view takes iso or top, not '{value}'.");
            }
        }

        private static string Next(string[] args, ref int i)
        {
            if (i + 1 >= args.Length) throw new ArgumentException($"{args[i]} needs a value.");
            return args[++i];
        }
    }
}
