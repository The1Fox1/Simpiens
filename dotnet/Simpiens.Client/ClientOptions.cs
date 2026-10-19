using Simpiens.Core.Session;
using Simpiens.Simulation;

namespace Simpiens.Client
{
    /// <summary>How a <see cref="SimpiensGame"/> starts: its session, seed, window and pacing.</summary>
    public sealed class ClientOptions
    {
        public string SessionId { get; set; } = OutpostSession.SessionId;
        public ulong Seed { get; set; } = SimulationSettings.DefaultSeed;

        public int Width { get; set; } = 1280;
        public int Height { get; set; } = 720;
        public bool VSync { get; set; } = true;

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

        /// <summary>Extra registrations for the simulation (development builds add the Dev Map; tests add doubles).</summary>
        public System.Action<Microsoft.Extensions.DependencyInjection.IServiceCollection> ConfigureServices { get; set; }

        public static string DefaultSaveDirectory()
            => System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Simpiens", "Saves");
    }
}
