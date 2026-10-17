using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;
using UnityEngine;

namespace Simpiens.Core.Session
{
    /// <summary>Trading terms for a stall opened by a session; position and radius come from the stall's POI.</summary>
    public readonly struct StallTerms
    {
        public readonly ushort RationSellPrice;
        public readonly ushort RationStock;
        public readonly ushort OreBuyPrice;
        public readonly ushort OreStock;
        public readonly ushort HerbBuyPrice;
        public readonly ushort HerbStock;
        public readonly uint TreasuryGold;

        public StallTerms(ushort rationSellPrice, ushort rationStock, ushort oreBuyPrice, ushort oreStock, ushort herbBuyPrice, ushort herbStock, uint treasuryGold)
        {
            RationSellPrice = rationSellPrice;
            RationStock = rationStock;
            OreBuyPrice = oreBuyPrice;
            OreStock = oreStock;
            HerbBuyPrice = herbBuyPrice;
            HerbStock = herbStock;
            TreasuryGold = treasuryGold;
        }
    }

    /// <summary>
    /// What a <see cref="ISessionDefinition"/> may do while it populates a freshly built world. Created by
    /// <see cref="SwarmSpawner"/> for one respawn. Seeded bounties are staged and registered after the ledger baseline,
    /// so their escrow moves gold that the baseline already counted.
    /// </summary>
    public sealed class SessionBuilder
    {
        private readonly SwarmSpawner _spawner;
        private readonly List<BountyContract> _seedBounties = new List<BountyContract>(4);
        private uint _seededStallGold;
        private int _stallCount;

        internal SessionBuilder(SwarmSpawner spawner, MapDefinition map, LoadedWorld world, ISimulationManager simulation, SimulationSettings settings)
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

        public IReadOnlyList<AutonomousAgent> Agents => _spawner.Agents;

        internal IReadOnlyList<BountyContract> SeedBounties => _seedBounties;
        internal uint SeededStallGold => _seededStallGold;
        internal int StallCount => _stallCount;

        /// <summary>
        /// Spawns one agent at <paramref name="position"/>. Seeded personalities derive from the simulation seed and
        /// the agent's entity id; unseeded agents keep the neutral default.
        /// </summary>
        public AutonomousAgent SpawnAgent(Vector2 position, string label, bool seededPersonality)
        {
            return _spawner.SpawnAgent(position, label, seededPersonality);
        }

        /// <summary>Opens a trade stall at the map POI <paramref name="poiKey"/>. Returns its entity id (default without a simulation).</summary>
        public EntityId OpenStall(string poiKey, in StallTerms terms)
        {
            if (Simulation == null) return default;
            if (!Map.TryGetPoi(poiKey, out var poi))
            {
                throw new KeyNotFoundException($"Map '{Map.Name}' has no stall POI '{poiKey}'.");
            }

            var stallId = _spawner.NextEntityId();
            World.RegisterEntity(poiKey, stallId);
            var stall = new TradeStallData(
                stallId,
                position: poi.Position,
                rationSellPrice: terms.RationSellPrice,
                rationStock: terms.RationStock,
                oreBuyPrice: terms.OreBuyPrice,
                oreStock: terms.OreStock,
                herbBuyPrice: terms.HerbBuyPrice,
                herbStock: terms.HerbStock,
                treasuryGold: terms.TreasuryGold,
                interactionRadius: poi.Radius);
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
