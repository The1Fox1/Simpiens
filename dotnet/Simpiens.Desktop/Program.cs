using System;
using System.Globalization;
using Simpiens.Client;

namespace Simpiens.Desktop
{
    /// <summary>The desktop game: <c>dotnet run --project dotnet/Simpiens.Desktop -- [options]</c>.</summary>
    public static class Program
    {
        private const string Usage = @"Simpiens (desktop)

  --seed N             simulation seed (default: the default seed)
  --session ID         session to play (default: the outpost)
  --width W --height H window size (default: 1280 x 720)
  --no-vsync           don't wait for the display's refresh
  --ticks-per-frame N  run N ticks every frame instead of following real time (fast-forward)
  --help               this text";

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

            using var game = new SimpiensGame(options);
            game.Run();
            return 0;
        }

        private static string Next(string[] args, ref int i)
        {
            if (i + 1 >= args.Length) throw new ArgumentException($"{args[i]} needs a value.");
            return args[++i];
        }
    }
}
