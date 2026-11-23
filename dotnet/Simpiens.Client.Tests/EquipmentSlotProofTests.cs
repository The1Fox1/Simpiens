using System.Linq;
using Simpiens.Simulation.Spatial;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Testing.Economy;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The equipment system's proof, the client's half (Epic 10 Part 3, K1.6): with a feet slot and boots added by data
    /// alone (<see cref="TestCatalogs.FeetAndBoots"/>), the dossier's model has a row for the feet in the table's display
    /// order, and its gear line shows the boots beside the rest, with no code of its own. The simulation's half is the core
    /// tests' (EquipmentSlotProofTests there).
    /// </summary>
    public class EquipmentSlotProofTests
    {
        [Fact]
        public void TheDossierShowsANewSlot()
        {
            using var extended = TestCatalogs.FeetAndBoots();
            var ui = UiDriver.Start(width: 1280, height: 720);
            var game = ui.Game;
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            crawler.Agent.Inventory.TryEquip(new ItemInstance(TestCatalogs.Boots, 1, 15, durability: 300)); // by hand
            crawler.Agent.Inventory.TryEquip(new ItemInstance(ItemType.HardHat, 1, 60, durability: 1500));
            game.Session.DashboardModel.SelectCrawler(crawler.Id);
            game.Play.Dashboard.Refresh();
            ui.Frame();

            var rows = game.Session.DashboardModel.Dossier.Equipment.Slots;
            Assert.Equal(EquipmentSlots.InDisplayOrder.Select(s => s.Id), rows.Select(r => r.Slot.Id));
            var feet = rows.Single(r => r.Slot.Id == TestCatalogs.Feet);
            Assert.Equal(("Feet", "Boots, 50% left", "Boots 50%"), (feet.Slot.DisplayName, feet.Text, feet.Short));
            Assert.Equal("Hard Hat 100%, Boots 50%", game.Session.DashboardModel.Dossier.Equipment.Summary); // the head's first, the feet's after the body's (its order, 4)
            Assert.Equal("Gear: Hard Hat 100% Boots 50%", game.Play.Dashboard.Panels.GearShown);
        }
    }
}
