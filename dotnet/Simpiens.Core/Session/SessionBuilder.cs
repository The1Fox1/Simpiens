using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;
namespace Simpiens.Core.Session
{
    /// <summary>
    /// What a <see cref="ISessionDefinition"/> may do while it populates a freshly built world. Created by
    /// <see cref="SwarmSpawner"/> for one respawn. Seeded bounties are staged and registered after the ledger baseline,
    /// so their escrow moves gold that the baseline already counted.
    /// </summary>
    public sealed class SessionBuilder
    {
        private readonly SessionHost _spawner;
        private readonly List<BountyContract> _seedBounties = new List<BountyContract>(4);
        private uint _seededStallGold;
        private int _stallCount;

        internal SessionBuilder(SessionHost spawner, MapDefinition map, LoadedWorld world, ISimulationManager simulation, SimulationSettings settings)
        {
            _spawner = spawner;
            Map = map;
            World = world;
            Simulation = simulation;
            Settings = settings;
        }

        public MapDefinition Map { get; }

        /// <summary>Entity ids of the map's spawned resource nodes and hearths, by definition key.</summary>
        public LoadedWorld World { get; }

        /// <summary>Null in isolated tests.</summary>
        public ISimulationManager Simulation { get; }

        /// <summary>Null in isolated tests.</summary>
        public IGroupRegistry Groups => Simulation?.Groups;

        public SimulationSettings Settings { get; }

        public IReadOnlyList<AgentState> Agents => _spawner.CrawlerStates;

        internal IReadOnlyList<BountyContract> SeedBounties => _seedBounties;
        internal uint SeededStallGold => _seededStallGold;
        internal int StallCount => _stallCount;

        /// <summary>
        /// Spawns one agent at <paramref name="position"/>. Seeded personalities derive from the simulation seed and
        /// the agent's entity id; unseeded agents keep the neutral default.
        /// </summary>
        public AgentState SpawnAgent(Vec2 position, string label, bool seededPersonality)
        {
            return _spawner.SpawnAgent(position, label, seededPersonality);
        }

        /// <summary>
        /// Opens a trade stall at the map POI <paramref name="poiKey"/> with an opening treasury and goods (item system I5:
        /// the session's economy gives them, EconomyTuning.StallGoods). Returns its entity id (default without a simulation).
        /// </summary>
        public EntityId OpenStall(string poiKey, uint treasuryGold, IReadOnlyList<StallGood> goods)
        {
            if (Simulation == null) return default;
            if (!Map.TryGetPoi(poiKey, out var poi))
            {
                throw new KeyNotFoundException($"Map '{Map.Name}' has no stall POI '{poiKey}'.");
            }

            var stallId = _spawner.NextEntityId();
            World.RegisterEntity(poiKey, stallId);
            var stall = new TradeStallData(stallId, poi.Position, treasuryGold, poi.Radius);
            for (int i = 0; goods != null && i < goods.Count; i++) stall = stall.WithGood(goods[i]);
            Simulation.RegisterTradeStall(stall);
            _seededStallGold += stall.TreasuryGold;
            _stallCount++;
            return stallId;
        }

        /// <summary>
        /// Stages a bounty to be registered through the normal escrow path once population is complete. Ticks are
        /// session-relative: every session starts at tick 0.
        /// </summary>
        public void SeedBounty(in BountyContract bounty)
        {
            _seedBounties.Add(bounty);
        }
    }
}
