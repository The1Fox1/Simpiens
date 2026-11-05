using System;
using System.Globalization;
using System.IO;
using Simpiens.Diagnostics;
using ZLogger;
using static System.FormattableString;

namespace Simpiens.Client
{
    /// <summary>
    /// What the player chose that outlives a game: the window's size and full screen, picked in the header's menu. Kept as
    /// <c>key=value</c> lines in a small text file; a missing, unreadable or odd file is no settings, never an error.
    /// </summary>
    public sealed class ClientSettings
    {
        public const string FileName = "settings.txt";

        /// <summary>The largest side a remembered window may have; a larger one is an odd file.</summary>
        private const int MaxSide = 16384;

        /// <summary>The window's size; 0 by 0 when the player hasn't picked one.</summary>
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }

        public bool HasWindowSize => WindowWidth > 0 && WindowHeight > 0;

        /// <summary>The game fills the screen; <see cref="WindowWidth"/> by <see cref="WindowHeight"/> is the window it leaves for.</summary>
        public bool FullScreen { get; set; }

        /// <summary><c>Simpiens/settings.txt</c> under the user's local application data, beside the saves' folder.</summary>
        public static string DefaultPath()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Simpiens", FileName);

        public static ClientSettings Load(string path)
        {
            var settings = new ClientSettings();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return settings;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                SimLog.Bootstrap.ZLogWarning($"Can't read the settings in {path}: {e.Message}");
                return settings;
            }
            foreach (string line in lines)
            {
                int equals = line.IndexOf('=');
                if (equals < 0) continue;
                string key = line.Substring(0, equals).Trim();
                string value = line.Substring(equals + 1).Trim();
                if (key == "window" && TryParseSize(value, out int width, out int height))
                {
                    settings.WindowWidth = width;
                    settings.WindowHeight = height;
                }
                else if (key == "fullscreen" && bool.TryParse(value, out bool fullScreen))
                {
                    settings.FullScreen = fullScreen;
                }
            }
            return settings;
        }

        /// <summary>Writes the settings to <paramref name="path"/>; a failure is a warning, and the game goes on.</summary>
        public void Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string window = HasWindowSize ? Invariant($"window={WindowWidth}x{WindowHeight}\n") : string.Empty;
                File.WriteAllText(path, window + (FullScreen ? "fullscreen=true\n" : "fullscreen=false\n"));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                SimLog.Bootstrap.ZLogWarning($"Can't keep the settings in {path}: {e.Message}");
            }
        }

        private static bool TryParseSize(string value, out int width, out int height)
        {
            width = height = 0;
            int x = value.IndexOf('x');
            if (x < 0) return false;
            if (!int.TryParse(value.AsSpan(0, x), NumberStyles.None, CultureInfo.InvariantCulture, out width)
                || !int.TryParse(value.AsSpan(x + 1), NumberStyles.None, CultureInfo.InvariantCulture, out height)) return false;
            return width > 0 && height > 0 && width <= MaxSide && height <= MaxSide;
        }
    }
}
