using System.Linq;
using Simpiens.Presentation;
using Simpiens.Simulation.Spatial;
using Simpiens.Testing.Environment;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Views read the simulation and never write it (review T4; ROADMAP §1A.1): with the clock paused, a minute of the
    /// player looking around leaves the authoritative world exactly as it was. Only typed commands change it, and those
    /// apply on a tick.
    /// </summary>
    public class ViewsWriteNothingTests
    {
        [Fact]
        public void LookingAroundAPausedWorldChangesNothingInIt()
        {
            var driver = UiDriver.Start(width: 1280, height: 720);
            driver.Ticks(300); // a crowd with plans, bounties and a charted room or two
            var game = driver.Game;
            var sim = game.Session.Simulation;
            long tick = sim.Clock.CurrentTick;
            var before = WorldStateHash.Capture(tick, sim.Host, sim.Simulation, sim.Sectors);

            // The panels, a crawler's dossier, both windows
            var crawler = sim.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            game.Session.DashboardModel.SelectCrawler(crawler.Id);
            driver.Frame(5);
            Assert.Equal(crawler.Id, game.Session.Selection.SelectedCrawler);
            game.Session.DashboardModel.OpenMerchant();
            driver.Frame(5);
            game.Session.DashboardModel.OpenBounties();
            driver.Frame(5);
            driver.Script.Keys = new KeyInput { EscapePressed = true };
            driver.Frame();
            driver.Script.Keys = new KeyInput { TabPressed = true }; // to the map
            driver.Frame();
            Assert.False(game.Session.DashboardModel.PanelsVisible);

            // The map: a pan, a click on a crawler and one on the floor, the views and wall styles, a reframe
            driver.Script.Keys = new KeyInput { Left = true, Up = true };
            driver.Frame(20);
            driver.Script.Keys = default;
            driver.Click(game.World.View.WorldToScreen(crawler.Position));
            driver.Click(new Vec2(640f, 500f));
            Assert.True(game.Session.Selection.SelectedSector >= 0 || game.Session.Selection.SelectedCrawler != default, "the clicks selected something");
            driver.Script.Keys = new KeyInput { ViewTogglePressed = true };
            driver.Frame();
            driver.Script.Keys = new KeyInput { WallStylePressed = true };
            driver.Frame();
            driver.Script.Keys = new KeyInput { HomePressed = true };
            driver.Frame();
            driver.Script.Keys = default;
            driver.Frame(20);

            Assert.Equal(tick, sim.Clock.CurrentTick);
            var after = WorldStateHash.Capture(sim.Clock.CurrentTick, sim.Host, sim.Simulation, sim.Sectors);
            Assert.True(before.Total == after.Total, "A view wrote the simulation: " + before.DescribeDifference(after));
        }
    }
}
