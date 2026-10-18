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
    }
}
