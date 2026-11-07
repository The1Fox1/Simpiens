using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Inventory;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Who braves a hazard alone (Epic 10 Part 2, W3.3, D51): a bold crawler judges a hazard by what gear made against its
    /// kind leaves of it. Bare or in hazard gear the Drowned Gallery's 1.5 is past the lone limit; through a respirator it
    /// is 0.375. A bold crawler in hazard gear trades it for the stall's respirator (D53), the charted Gallery's plan
    /// being known (D54), and works the Gallery alone. Mechanism tests: the gear, the stock and the purses are set by hand;
    /// W3.5's scenario has none. Finding the seam once it wears the respirator is exploration's luck (F15), so the test
    /// shows it the seam then.
    /// </summary>
    public class LoneVentureTests
    {
        private static AgentInventory Wearing(ItemType item)
        {
            var inventory = new AgentInventory();
            if (item != ItemType.None) inventory.TryEquip(new ItemInstance(1, item, 1, 60, durability: 1500));
            return inventory;
        }

        [Fact]
        public void GearMadeForAHazardChangesWhoBravesItAlone()
        {
            var gallery = Floor1Layout.DrownedGalleryHazard;
            var caves = Floor1Layout.GlowCavesHazard;
            Assert.False(Vitality.WouldEnter(true, gallery, Wearing(ItemType.None)), "Bare, the Gallery is past the lone limit.");
            Assert.False(Vitality.WouldEnter(true, gallery, Wearing(ItemType.HazardGear)), "Hazard gear softens the harm, not the judgment.");
            Assert.True(Vitality.WouldEnter(true, gallery, Wearing(ItemType.Respirator)), "Through a respirator the Gallery is within it.");
            Assert.Equal(0.375f, Vitality.LoneDanger(gallery, Wearing(ItemType.Respirator)), 4);
            Assert.False(Vitality.WouldEnter(true, gallery, Wearing(ItemType.HardHat)), "A hard hat does nothing for flooded air.");
            Assert.False(Vitality.WouldEnter(false, gallery, Wearing(ItemType.Respirator)), "The cautious don't go at all.");
            Assert.True(Vitality.WouldEnter(true, caves, Wearing(ItemType.None)), "The Glow Caves are within the limit bare, as before.");
        }

        /// <summary>
        /// D53, D54: a bold crawler in sound hazard gear, knowing nothing of the Gallery but what the charted map shows, trades
        /// its gear in for the stall's respirator (the gear goes back into stock, no gold back) and, shown the seam, works the
        /// Gallery alone.
        /// </summary>
        [Fact]
        public void ABoldCrawlerTradesItsGearForARespiratorAndWorksTheGalleryAlone()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var map = sim.Host.Current.Map;
            var bold = sim.Host.CrawlerStates.First(s => !s.IsDrifter && s.Personality.RiskTolerancePercent >= Vitality.VentureRiskTolerancePercent);
            Assert.True(sim.Sectors.IsExplored(Floor1Layout.DrownedGallerySectorId), "The Gallery is charted from the start: its plan is known.");

            // By hand: it wears sound hazard gear, the stall has a respirator, and only this crawler can pay for protection
            Assert.True(bold.Inventory.TryEquip(new ItemInstance(1, ItemType.HazardGear, 1, 30, durability: 1500)));
            var sm = (SimulationManager)sim.Simulation;
            sm.UpdateStall(sm.ActiveStalls[0].WithStock(ItemType.Respirator, 1));
            int gearStock = sm.ActiveStalls[0].StockOf(ItemType.HazardGear);
            foreach (var crawler in sim.Host.CrawlerStates) crawler.Inventory.Gold = crawler == bold ? 80 : 0; // the price and a ration's: after it, it needs to earn

            long inGallery = -1;
            int gearStockAtUpgrade = -1;
            var seam = sim.Host.World.GetEntityId(Floor1Layout.GallerySeam);
            var seamAt = map.ResourceNodes.First(n => n.Key == Floor1Layout.GallerySeam).Position;
            for (long t = 0; t < DayClock.TicksPerDay && inGallery < 0; t++)
            {
                sim.Step(1);
                bool respirator = bold.Inventory.GetEquipped(EquipmentSlot.Protection).Type == ItemType.Respirator;
                if (respirator && gearStockAtUpgrade < 0)
                {
                    gearStockAtUpgrade = sim.Simulation.ActiveStalls[0].StockOf(ItemType.HazardGear);
                    // Shown the seam (by hand: which sector it searches first is luck, F15)
                    ((Simpiens.Cognition.Memory.AgentMemory)bold.Memory).SpatialMemoryMap[seam] = new Simpiens.Cognition.Memory.SpatialMemoryRecord(
                        seam, EntityType.Resource, GridPoint.RoundToInt(seamAt), (uint)sim.Clock.CurrentTick, ItemType.Glowstone);
                }
                if (respirator && map.FindSector(bold.Position).Id == Floor1Layout.DrownedGallerySectorId) inGallery = sim.Clock.CurrentTick;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"wears {bold.Inventory.GetEquipped(EquipmentSlot.Protection).Type}; in the Gallery at t{inGallery}; gear bought {bold.Activity.GearPurchases}; hazard ticks {bold.Activity.HazardTicks}");
            Assert.Equal(ItemType.Respirator, bold.Inventory.GetEquipped(EquipmentSlot.Protection).Type);
            Assert.Equal(1, bold.Activity.GearPurchases);
            Assert.Equal(0, sim.Simulation.ActiveStalls[0].StockOf(ItemType.Respirator));
            Assert.Equal(gearStock + 1, gearStockAtUpgrade); // its gear traded in (others, earning since, may buy it later)
            Assert.True(inGallery >= 0, "Through its respirator it went into the Gallery alone.");
            Assert.False(sim.Simulation.Guild.HasExpedition, "Alone: no party was sent.");
            Assert.Equal(0, sim.Simulation.IntentFaults);
        }
    }
}
