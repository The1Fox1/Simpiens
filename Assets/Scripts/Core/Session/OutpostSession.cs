using System.Collections.Generic;
using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;
using UnityEngine;

namespace Simpiens.Core.Session
{
    /// <summary>
    /// The live game (Epic MAP Phase 3): free crawlers with seeded personalities walk in from the Road Gate of the
    /// outpost floor (Floor1Layout) and find the hub's trade stall, the hearth and the two seeded bounties (Scout
    /// the Deep Seam, Gather its iron) for themselves. Nothing is scripted: the supply loop emerges
    /// (OutpostSupplyLoopScenario).
    /// </summary>
    public sealed class OutpostSession : ISessionDefinition
    {
        public const string SessionId = "outpost";
        public const int DefaultCrawlerCount = 12;

        public const ushort SeedGatherBountyId = 1;
        public const ushort SeedScoutBountyId = 2;

        /// <summary>The frontier sector both seeded bounties target: scout it, then gather its iron.</summary>
        public const byte SeedBountySectorId = Floor1Layout.DeepSeamSectorId;

        /// <summary>Seeded bounties expire this many ticks after the session starts (every session starts at 0).</summary>
        public const uint SeedBountyDurationTicks = 6000;

        /// <summary>Extra crawlers beyond the gate's spawn points start this far east of a spawn point, between two.</summary>
        private const float OverflowOffset = 1f;

        [VContainer.Inject]
        public OutpostSession() : this(DefaultCrawlerCount)
        {
        }

        /// <summary>A crawler count other than the default is for measurement only (PopulationScalingProbe).</summary>
        public OutpostSession(int crawlerCount)
        {
            CrawlerCount = crawlerCount;
        }

        public int CrawlerCount { get; }

        public string Id => SessionId;
        public string DisplayName => "Main Map";
        public MapDefinition Map => Floor1Layout.Definition;

        public void Populate(SessionBuilder session)
        {
            var spawns = new List<Vector2>(Floor1Layout.SpawnPointCount);
            for (int i = 0; i < session.Map.Pois.Count; i++)
            {
                var poi = session.Map.Pois[i];
                if (poi.Kind == PoiKind.SpawnPoint) spawns.Add(poi.Position);
            }

            for (int c = 0; c < CrawlerCount; c++)
            {
                var position = spawns[c % spawns.Count] + Vector2.right * (OverflowOffset * (c / spawns.Count));
                session.SpawnAgent(position, $"Crawler{c}", seededPersonality: true);
            }

            var stallId = session.OpenStall(Floor1Layout.Stall, new StallTerms(
                rationSellPrice: 15, rationStock: 50,
                oreBuyPrice: 20, oreStock: 10,
                herbBuyPrice: 25, herbStock: 10,
                treasuryGold: 250));

            // Both seeded bounties go through the same escrow path as player-posted ones.
            // TargetLocation is left default so the simulation derives it from the target sector.
            session.SeedBounty(new BountyContract(
                bountyId: SeedGatherBountyId,
                type: BountyType.GatherResource,
                targetSectorId: SeedBountySectorId,
                targetItemType: ItemType.IronOre,
                targetQuantity: 1,
                rewardGold: 25,
                issuedTick: 0,
                expirationTick: SeedBountyDurationTicks,
                creatorStallId: stallId,
                status: BountyStatus.Open));

            session.SeedBounty(new BountyContract(
                bountyId: SeedScoutBountyId,
                type: BountyType.ScoutSector,
                targetSectorId: SeedBountySectorId,
                targetItemType: ItemType.None,
                targetQuantity: 1,
                rewardGold: 15,
                issuedTick: 0,
                expirationTick: SeedBountyDurationTicks,
                creatorStallId: stallId,
                status: BountyStatus.Open));
        }
    }
}
