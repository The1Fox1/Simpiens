using System.Collections.Generic;
using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;
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

        /// <summary>
        /// Crawlers arrive fed from the road. At the default 50 every crawler reached starvation panic together about
        /// 23 s in, two rooms from the Garden and too poor to buy a ration (10 gold, rations 15g): an opening wave of
        /// starvation on Floor 1 (Epic MAP Phase 3). Fed, they earn their first gold before they need to eat.
        /// </summary>
        public const float ArrivalHunger = 20f;

        /// <summary>Extra crawlers beyond the gate's spawn points start this far east of a spawn point, between two.</summary>
        private const float OverflowOffset = 1f;

        [VContainer.Inject]
        public OutpostSession() : this(DefaultCrawlerCount)
        {
        }

        /// <summary>
        /// A crawler count other than the default is for measurement only (PopulationScalingProbe); so is a tuning other
        /// than <see cref="EconomyTuning.Default"/> (the economy soak).
        /// </summary>
        public OutpostSession(int crawlerCount, EconomyTuning economy = null)
        {
            CrawlerCount = crawlerCount;
            Economy = economy ?? EconomyTuning.Default;
        }

        public int CrawlerCount { get; }

        public EconomyTuning Economy { get; }

        public string Id => SessionId;
        public string DisplayName => "Main Map";
        public MapDefinition Map => Floor1Layout.Definition;

        public void Populate(SessionBuilder session)
        {
            var spawns = new List<Vec2>(Floor1Layout.SpawnPointCount);
            for (int i = 0; i < session.Map.Pois.Count; i++)
            {
                var poi = session.Map.Pois[i];
                if (poi.Kind == PoiKind.SpawnPoint) spawns.Add(poi.Position);
            }

            for (int c = 0; c < CrawlerCount; c++)
            {
                var position = spawns[c % spawns.Count] + Vec2.Right * (OverflowOffset * (c / spawns.Count));
                var crawler = session.SpawnAgent(position, $"Crawler{c}", seededPersonality: true);
                crawler.Hunger = ArrivalHunger;
            }

            var economy = Economy;
            var stallId = session.OpenStall(Floor1Layout.Stall, new StallTerms(
                rationSellPrice: economy.StallRationPrice, rationStock: economy.StallRationStock,
                oreBuyPrice: economy.StallOreBuyPrice, oreStock: economy.StallOreStock,
                herbBuyPrice: economy.StallHerbBuyPrice, herbStock: economy.StallHerbStock,
                treasuryGold: economy.StallTreasury));

            // Both seeded bounties go through the same escrow path as player-posted ones.
            // TargetLocation is left default so the simulation derives it from the target sector.
            session.SeedBounty(new BountyContract(
                bountyId: SeedGatherBountyId,
                type: BountyType.GatherResource,
                targetSectorId: SeedBountySectorId,
                targetItemType: ItemType.IronOre,
                targetQuantity: 1,
                rewardGold: economy.SeedGatherBountyReward,
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
                rewardGold: economy.SeedScoutBountyReward,
                issuedTick: 0,
                expirationTick: SeedBountyDurationTicks,
                creatorStallId: stallId,
                status: BountyStatus.Open));
        }
    }
}
