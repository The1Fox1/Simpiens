using Simpiens.Intervention;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Workshops;

namespace Simpiens.Hosting
{
    /// <summary>
    /// A scripted player for the tavern (Epic LIFE-1 plan 2, T.6, D18): it acts only through the typed commands a real player
    /// sends, as <see cref="WorkshopPlayer"/> does. After every tick it builds the tavern once the stall holds its price and
    /// the reserve besides; hires as its keeper the resident who likes the patron best among those who would take the work at
    /// its wage (the counter has no skill to hire for); and gives the keeper its shifts. The menu keeps the session's prices.
    /// It counts what the tavern serves (<c>SimulationManager.Served</c>). Shared by the Definition-of-Done scenarios, the
    /// soak and the console.
    /// </summary>
    public sealed class TavernPlayer
    {
        public const string SourceId = "TavernPlayer";

        private readonly LocalSimulation _sim;
        private readonly IPlayerInterventionService _commands;
        private readonly byte _shifts;
        private readonly ushort _wage;
        private readonly uint _reserve;

        /// <param name="shifts">The blocks the keeper keeps the counter (<see cref="ShiftBlocks"/>): its opening hours.</param>
        /// <param name="wage">The keeper's wage; the session's default wage when 0.</param>
        /// <param name="reserve">Gold it keeps in the treasury: it builds only when the stall holds the price and this besides.</param>
        public TavernPlayer(LocalSimulation sim, byte shifts = ShiftBlocks.DayShift, ushort wage = 0, uint reserve = 0)
        {
            _sim = sim;
            _commands = (IPlayerInterventionService)sim.Services.GetService(typeof(IPlayerInterventionService));
            _shifts = shifts;
            _wage = wage != 0 ? wage : sim.Simulation.Economy.Workshops.DefaultWage;
            _reserve = reserve;
            sim.Host.TickCompleted += Act;
            sim.Simulation.Served += (crawler, item, price) =>
            {
                Served++;
                Takings += price;
                if (item == Simpiens.Simulation.Economy.ItemType.Ale) Ales++;
                else if (item == Simpiens.Simulation.Economy.ItemType.Stew) Suppers++;
            };
        }

        /// <summary>When it first saw the tavern standing, and first hired its keeper; -1 until it has.</summary>
        public long BuiltTick { get; private set; } = -1;
        public long HireTick { get; private set; } = -1;

        /// <summary>What the tavern served, and its takings: every sale's price, to the treasury.</summary>
        public int Served { get; private set; }
        public int Ales { get; private set; }
        public int Suppers { get; private set; }
        public uint Takings { get; private set; }

        /// <summary>Stops acting (a scenario that takes over).</summary>
        public void Stop() => _sim.Host.TickCompleted -= Act;

        // One command a tick at most: each applies at the start of the next
        private void Act(long tick)
        {
            if (_commands == null) return;
            var workshops = _sim.Simulation.Workshops;
            var host = (IWorkshopHost)_sim.Simulation;
            int index = workshops.IndexOf(StationKind.Tavern);
            if (index < 0) return;

            if (!workshops[index].Built)
            {
                if (workshops.CanBuild(host, index) != WorkshopRules.Check.Ok) return;
                if (_sim.Simulation.ActiveStalls[0].TreasuryGold < _sim.Simulation.Economy.Workshops.CostOf(StationKind.Tavern) + _reserve) return;
                _commands.QueueInfluence(InfluenceEvent.CreateBuildStation(SourceId, (byte)index));
                return;
            }
            if (BuiltTick < 0) BuiltTick = tick;

            var keeper = workshops.ArtisanAt(index);
            if (keeper == default)
            {
                var crawlers = _sim.Host.CrawlerStates;
                int best = -1, bestLiking = int.MinValue;
                for (int i = 0; i < crawlers.Count; i++)
                {
                    if (workshops.CanHire(host, crawlers[i].AgentId, index, _wage, (uint)tick) != WorkshopRules.Check.Ok) continue;
                    var affinity = crawlers[i].Memory != null ? crawlers[i].Memory.GetAffinity(Simpiens.Simulation.WorldRegistry.PlayerEntityId, (uint)tick) : default;
                    int liking = affinity.Trust + affinity.Warmth;
                    if (liking > bestLiking) { best = i; bestLiking = liking; }
                }
                if (best < 0) return;
                _commands.QueueInfluence(InfluenceEvent.CreateHireArtisan(SourceId, crawlers[best].AgentId, (byte)index, _wage));
                if (HireTick < 0) HireTick = tick;
                return;
            }

            if (workshops.TryGetContract(keeper, out var contract) && contract.Shifts != _shifts)
                _commands.QueueInfluence(InfluenceEvent.CreateSetShifts(SourceId, keeper, _shifts));
        }
    }
}
