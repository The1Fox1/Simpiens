using Simpiens.Core.Session;
using Simpiens.Presentation;
using Simpiens.Simulation;

namespace Simpiens.Client
{
    /// <summary>How a <see cref="SimpiensGame"/> starts: its session, seed, window and pacing.</summary>
    public sealed class ClientOptions
    {
        public string SessionId { get; set; } = OutpostSession.SessionId;
        public ulong Seed { get; set; } = SimulationSettings.DefaultSeed;

        /// <summary>The window's size: 1920 by 1080 unless the host asks for another (<see cref="WindowSizes"/>).</summary>
        public int Width { get; set; } = WindowSizes.Default.X;
        public int Height { get; set; } = WindowSizes.Default.Y;
        public bool VSync { get; set; } = true;

        /// <summary>Fill the screen (borderless, at its own size) from the first update; the menu switches it.</summary>
        public bool FullScreen { get; set; }

        /// <summary>How the world is looked at first: isometric, the main view (producer P4); V switches to top-down and back.</summary>
        public ViewMode View { get; set; } = ViewMode.Isometric;

        /// <summary>
        /// 0 (the game): the clock follows real time, as Unity's clock driver did. N > 0 (tests, fast-forward): every
        /// update runs exactly N ticks, whatever time passed.
        /// </summary>
        public int TicksPerUpdate { get; set; }

        /// <summary>
        /// False (the game): animations and fades run on real time, the same at any simulation speed, as in Unity. True
        /// (tests, captures): the view's clock is the tick's time, so a frame drawn at a given tick is always the same.
        /// </summary>
        public bool ViewClockFromTicks { get; set; }

        /// <summary>How often the dashboard re-reads the simulation, in real seconds: Unity's 10 Hz. 0 (tests): every frame.</summary>
        public float DashboardRefreshSeconds { get; set; } = 0.1f;

        /// <summary>The quick-save slot's folder; null: <c>Simpiens/Saves</c> under the user's local application data.</summary>
        public string SaveDirectory { get; set; }

        /// <summary>
        /// The player's settings file (<see cref="ClientSettings"/>): a window size picked in the menu is kept there. Null
        /// (tests): nothing is kept. The desktop exe passes <see cref="ClientSettings.DefaultPath"/>.
        /// </summary>
        public string SettingsFile { get; set; }

        /// <summary>
        /// Development builds: the demonstration cutscenes (<see cref="DemoScenes"/>), F9 for the hard one and triggers for
        /// the dynamic ones. Off in release builds, which have no story content yet (the narrative work).
        /// </summary>
        public bool DemoScenes { get; set; }

        /// <summary>Development builds: F3 shows the frame's timings by phase in a corner (review D1).</summary>
        public bool TimingsOverlay { get; set; }

        /// <summary>Development builds: names in the dashboard carry the entity's debug name, "[SwarmAgent_0]" (Epic UI-Beta B5).</summary>
        public bool ShowDebugIds { get; set; }

        /// <summary>
        /// The interface moves (the UI polish plan, U7): windows arrive, figures roll. The desktop game turns it on; tests and
        /// the pinned renders leave it off, so nothing they capture is mid-tween.
        /// </summary>
        public bool UiMotion { get; set; }

        /// <summary>Extra registrations for the simulation (development builds add the Dev Map; tests add doubles).</summary>
        public System.Action<Microsoft.Extensions.DependencyInjection.IServiceCollection> ConfigureServices { get; set; }

        public static string DefaultSaveDirectory()
            => System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Simpiens", "Saves");
    }
}
