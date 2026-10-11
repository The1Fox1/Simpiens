using Simpiens.Core.Session.Layouts;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;
using UnityEngine;

namespace Simpiens.Core.Session
{
    /// <summary>
    /// The live game (Epic MAP Phase 2): free crawlers with seeded personalities, the outpost trade stall and the two
    /// seeded bounties (Gather ore, Scout a shrouded sector) on the Outpost Commons yard. Nothing is scripted: the
    /// supply loop emerges (OutpostSupplyLoopScenario). Phase 3 moves it onto Floor 1.
    /// </summary>
    public sealed class OutpostSession : ISessionDefinition
    {
        public const string SessionId = "outpost";
        public const int CrawlerCount = 6;

        public const ushort SeedGatherBountyId = 1;
        public const ushort SeedScoutBountyId = 2;

        /// <summary>Seeded bounties expire this many ticks after the session starts (every session starts at 0).</summary>
        public const uint SeedBountyDurationTicks = 6000;

        public string Id => SessionId;
        public string DisplayName => "Main Map";
        public MapDefinition Map => OutpostCommonsLayout.Definition;

        public void Populate(SessionBuilder session)
        {
            for (int c = 0; c < CrawlerCount; c++)
            {
                // Spread across the north edge of the yard
                session.SpawnAgent(new Vector2(-3f + (c % 4) * 2f, 13f + (c / 4) * 1.5f), $"Crawler{c}", seededPersonality: true);
            }

            var stallId = session.OpenStall(OutpostCommonsLayout.OutpostStall, new StallTerms(
                rationSellPrice: 15, rationStock: 50,
                oreBuyPrice: 20, oreStock: 10,
                herbBuyPrice: 25, herbStock: 10,
                treasuryGold: 250));

            // Both seeded bounties go through the same escrow path as player-posted ones.
            // TargetLocation is left default so the simulation derives it from the target sector.
            session.SeedBounty(new BountyContract(
                bountyId: SeedGatherBountyId,
                type: BountyType.GatherResource,
                targetSectorId: OutpostCommonsLayout.OreRidgeSectorId,
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
                targetSectorId: OutpostCommonsLayout.HerbHollowSectorId,
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
