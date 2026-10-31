using System.Collections.Generic;
using Simpiens.Intervention;
using Simpiens.Simulation.Guild;

namespace Simpiens.Hosting
{
    /// <summary>
    /// A scripted player for the guild (Epic 10 G4): it acts only through the typed commands a real player sends, as the
    /// economy soak's profiles do. After every tick it looks at what the dossier shows (the guild's own rule, read-only)
    /// and recruits the first resident, in spawn order, who would sign on at its wage, until the guild reaches its target
    /// size. Shared by the Definition-of-Done scenarios, the soak's guild profile (G6) and the console.
    /// </summary>
    public sealed class GuildPlayer
    {
        public const string SourceId = "GuildPlayer";

        private readonly LocalSimulation _sim;
        private readonly IPlayerInterventionService _commands;
        private readonly int _targetSize;
        private readonly ushort _wage;
        private readonly List<EntityId> _recruited = new List<EntityId>(4);

        /// <param name="wage">The wage it offers; the session's default wage when 0.</param>
        public GuildPlayer(LocalSimulation sim, int targetSize, ushort wage = 0)
        {
            _sim = sim;
            _commands = (IPlayerInterventionService)sim.Services.GetService(typeof(IPlayerInterventionService));
            _targetSize = targetSize;
            _wage = wage != 0 ? wage : sim.Simulation.Economy.Guild.DefaultWage;
            sim.Host.TickCompleted += Act;
        }

        /// <summary>Every crawler it has asked to sign on, in order.</summary>
        public IReadOnlyList<EntityId> Recruited => _recruited;

        /// <summary>The tick it first asked someone to sign on, or -1.</summary>
        public long FirstRecruitTick { get; private set; } = -1;

        public ushort Wage => _wage;

        /// <summary>Stops acting (a scenario that takes over).</summary>
        public void Stop() => _sim.Host.TickCompleted -= Act;

        /// <summary>Sends a price command, as the player's merchant terminal does.</summary>
        public void SetPrices(in UpdateStallPricesCommand prices) => _commands.QueueInfluence(InfluenceEvent.CreateUpdateStallPrices(SourceId, prices));

        private void Act(long tick)
        {
            var guild = _sim.Simulation.Guild;
            if (guild.Members.Count >= _targetSize || _commands == null) return;
            var host = (IGuildHost)_sim.Simulation;
            var crawlers = _sim.Host.CrawlerStates;
            for (int i = 0; i < crawlers.Count; i++)
            {
                var crawler = crawlers[i];
                if (guild.CanRecruit(host, crawler.AgentId, _wage, (uint)tick) != GuildRules.Check.Ok) continue;
                _commands.QueueInfluence(InfluenceEvent.CreateRecruit(SourceId, crawler.AgentId, _wage));
                _recruited.Add(crawler.AgentId);
                if (FirstRecruitTick < 0) FirstRecruitTick = tick;
                return; // one a tick: the command applies at the start of the next
            }
        }
    }
}
