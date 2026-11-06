using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Memory;
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
    /// is 0.375. Knowing the Gallery's glowstone, a bold crawler buys the respirator rather than hazard gear and works the
    /// Gallery alone. Mechanism tests: the knowledge, the stock and the purses are set by hand; W3.5's scenario has none.
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

        [Fact]
        public void ABoldCrawlerWhoKnowsTheGallerysGlowstoneBuysARespiratorAndWorksItAlone()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var map = sim.Host.Current.Map;
            var bold = sim.Host.CrawlerStates.First(s => !s.IsDrifter && s.Personality.RiskTolerancePercent >= Vitality.VentureRiskTolerancePercent);
            var node = sim.Registry.ActiveEntities.First(e => e.Type == EntityType.Resource && e.ResourceKind == ItemType.Glowstone
                && map.FindSector(e.Position).Id == Floor1Layout.DrownedGallerySectorId);

            // By hand: it knows the Gallery's glowstone, the Gallery is charted, the stall has a respirator beside its hazard
            // gear, and only this crawler can pay for protection
            sim.Sectors.SetExplored(Floor1Layout.DrownedGallerySectorId, true);
            ((AgentMemory)bold.Memory).SpatialMemoryMap[node.Id] = new SpatialMemoryRecord(node.Id, EntityType.Resource, GridPoint.RoundToInt(node.Position), 1) { ResourceKind = ItemType.Glowstone };
            var sm = (SimulationManager)sim.Simulation;
            sm.UpdateStall(sm.ActiveStalls[0].WithStock(ItemType.Respirator, 1));
            foreach (var crawler in sim.Host.CrawlerStates) crawler.Inventory.Gold = crawler == bold ? 80 : 0; // the price and a ration's: after it, it needs to earn

            long inGallery = -1;
            for (long t = 0; t < 12 * DayClock.TicksPerHour && inGallery < 0; t += 10)
            {
                sim.Step(10);
                if (bold.Inventory.GetEquipped(EquipmentSlot.Protection).Type == ItemType.Respirator
                    && map.FindSector(bold.Position).Id == Floor1Layout.DrownedGallerySectorId) inGallery = sim.Clock.CurrentTick;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"wears {bold.Inventory.GetEquipped(EquipmentSlot.Protection).Type}; in the Gallery at t{inGallery}; gear bought {bold.Activity.GearPurchases}; hazard ticks {bold.Activity.HazardTicks}");
            Assert.Equal(ItemType.Respirator, bold.Inventory.GetEquipped(EquipmentSlot.Protection).Type);
            Assert.Equal(1, bold.Activity.GearPurchases);
            Assert.Equal(0, sim.Simulation.ActiveStalls[0].StockOf(ItemType.Respirator));
            Assert.True(inGallery >= 0, "Through its respirator it went into the Gallery alone.");
            Assert.False(sim.Simulation.Guild.HasExpedition, "Alone: no party was sent.");
            Assert.Equal(0, sim.Simulation.IntentFaults);
        }
    }
}
