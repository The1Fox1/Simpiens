using System.IO;
using System.Linq;
using Gum.Forms.Controls;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Spatial;
using Simpiens.Testing.Sessions;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Quick save and load, the session switch and the camera's follow (Epic CLIENT M2.7): Unity's
    /// QuickSaveButtonsScenario, SessionSwitchScenario and ShroudViewScenario's camera part, through the client's UI.
    /// </summary>
    public class SaveAndSessionTests
    {
        private const int Width = 1280, Height = 720;

        private static Vec2 Centre(FrameworkElement control)
            => new Vec2(control.AbsoluteLeft + control.ActualWidth * 0.5f, control.AbsoluteTop + control.ActualHeight * 0.5f);

        [Fact]
        public void TheHeadersSaveAndLoadKeepAndBringBackTheGame()
        {
            var ui = UiDriver.Start(width: Width, height: Height);
            var game = ui.Game;
            var header = game.Play.Dashboard.Header;
            Assert.True(header.SaveButton.IsVisible && header.LoadButton.IsVisible, "Save and Load show with a slot.");
            Assert.StartsWith(Path.GetTempPath(), game.Session.SaveGame.QuickSavePath);

            ui.Click(Centre(header.LoadButton));
            ui.Frame(); // the host applies it before its next ticks (review D3)
            Assert.Equal("There is no quick save yet.", header.SaveStatusText);

            ui.Ticks(300);
            long saved = game.Session.Simulation.Clock.CurrentTick;
            ui.Click(Centre(header.SaveButton));
            Assert.Equal("Saving...", game.Session.DashboardModel.Saves.Status);
            ui.Frame(); // the host applies it before its next ticks (review D3)
            for (int i = 0; i < 200 && game.Session.DashboardModel.Saves.IsSaving; i++) ui.Frame();
            Assert.Equal($"Saved at tick {saved}.", header.SaveStatusText);
            Assert.True(File.Exists(game.Session.SaveGame.QuickSavePath));
            Assert.True(SaveFile.TryDecode(File.ReadAllBytes(game.Session.SaveGame.QuickSavePath), out var onDisk, out _));
            Assert.Equal(saved, onDisk.Header.Tick);
            Assert.Equal(OutpostSession.DefaultCrawlerCount, onDisk.Crawlers.Length);

            ui.Ticks(100);
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            game.Session.DashboardModel.SelectCrawler(crawler.Id);
            game.Session.DashboardModel.OpenBounties();
            ui.Click(Centre(header.LoadButton));
            Assert.Equal(saved + 100, game.Session.Simulation.Clock.CurrentTick); // not inside the click: Gum is still dispatching it
            ui.Frame(); // the host applies it before its next ticks (review D3)
            Assert.Equal(saved, game.Session.Simulation.Clock.CurrentTick);
            Assert.Equal($"Loaded the save from tick {saved}.", header.SaveStatusText);
            Assert.Contains(saved.ToString("D5"), game.Play.Dashboard.Header.TickText);
            Assert.Equal(OutpostSession.DefaultCrawlerCount, game.Session.Simulation.Registry.ActiveEntities.Count(e => e.Type == EntityType.Pawn));
            Assert.Equal(default, game.Session.Selection.SelectedCrawler); // ids are reissued: the selection is dropped
            Assert.False(game.Session.DashboardModel.Bounties.IsOpen, "A load closes the windows.");

            ui.Ticks(100);
            Assert.Equal(saved + 100, game.Session.Simulation.Clock.CurrentTick);
        }

        [Fact]
        public void TheSessionSwitchGoesToTheDevMapAndBackToAFreshOutpost()
        {
            var options = new ClientOptions { ConfigureServices = services => Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<ISessionDefinition>(services, new CanaryPenSession()) };
            var ui = UiDriver.Start(options, Width, Height);
            var game = ui.Game;
            var host = game.Session.Simulation.Host;
            var bar = game.Play.Dashboard.Sessions;
            Assert.Equal(2, host.Sessions.Count);
            Assert.Same(host.Find(OutpostSession.SessionId), host.Default);

            var fresh = WorldCapture.Of(game);
            Assert.Equal(0L, fresh.Tick);
            Assert.Equal("Floor1", fresh.MapName);
            Assert.Equal(OutpostSession.DefaultCrawlerCount, fresh.Crawlers);

            ui.Ticks(200);
            Assert.True(bar.Root.Visible, "The switch shows when a test session is registered.");
            Assert.False(bar.BannerShown, "No test-session banner on the main map.");
            Assert.Equal("SWITCH TO DEV MAP", bar.Button.Text);

            ui.Click(Centre(bar.Button));
            Assert.Equal(OutpostSession.SessionId, host.Current.Id); // queued, not run inside the click
            ui.Frame(); // the host applies it before its next ticks (review D3)
            Assert.Equal(CanaryPenSession.SessionId, host.Current.Id);
            Assert.True(bar.BannerShown);
            Assert.Equal("DEV MAP: TEST SESSION (NOT THE GAME)", bar.BannerTextShown);
            Assert.Equal("SWITCH TO MAIN MAP", bar.Button.Text);
            Assert.Equal(0L, game.Session.Simulation.Clock.CurrentTick);
            Assert.Equal("DevMap", host.Current.Map.Name);
            Assert.Equal(CanaryPenSession.ScriptedPenAgentCount, game.Session.Simulation.Registry.ActiveEntities.Count(e => e.Type == EntityType.Pawn));
            var sim = game.Session.Simulation.Simulation;
            Assert.True(sim.ActiveStalls.Count == 0 && sim.ActiveBounties.Count == 0, "No stall or bounty survives the switch.");
            Assert.Equal(0, ((Simpiens.Cognition.Contracts.IIntentExecutionContext)sim).ActiveIntents.Count);
            var events = new System.Collections.Generic.List<Simpiens.Narrative.ChronicleEvent>();
            sim.Chronicle.GetAllRecentEvents(events, Simpiens.Narrative.NarrativeChronicleService.DefaultGlobalCapacity);
            Assert.All(events, e => Assert.Equal(0u, e.SimulationTick));
            Assert.Equal(game.Session.Simulation.Sectors.All.Count, game.Session.DashboardModel.Sectors.Sectors.Count);

            ui.Ticks(200);
            ui.Click(Centre(bar.Button));
            ui.Frame(); // the host applies it before its next ticks (review D3)
            Assert.Equal(OutpostSession.SessionId, host.Current.Id);
            Assert.False(bar.BannerShown, "The banner hides again on the main map.");
            Assert.Equal(0, ((Simpiens.Cognition.Contracts.IIntentExecutionContext)sim).ActiveIntents.Count);
            WorldCapture.Of(game).AssertSameAs(fresh);

            ui.Ticks(200);
            uint stallGold = (uint)sim.ActiveStalls.Sum(s => s.TreasuryGold);
            uint crawlerGold = (uint)game.Session.Simulation.Registry.ActiveEntities.Where(e => e.Agent != null).Sum(e => e.Agent.Inventory?.Gold ?? 0);
            Assert.True(sim.EconomyLedger.ValidateConservation(stallGold, crawlerGold, out var why), "Ledger conserved after the switch back: " + why);
        }

        /// <summary>What Unity's scenario compared between a fresh outpost and one switched away and back.</summary>
        private sealed class WorldCapture
        {
            public long Tick;
            public string MapName;
            public int Crawlers;
            public string Agents;
            public uint Fauceted, Escrow;
            public string Stalls, Bounties, Explored;

            public static WorldCapture Of(SimpiensGame game)
            {
                var sim = game.Session.Simulation.Simulation;
                var sectors = game.Session.Simulation.Sectors;
                var pawns = game.Session.Simulation.Registry.ActiveEntities.Where(e => e.Type == EntityType.Pawn).ToList();
                return new WorldCapture
                {
                    Tick = game.Session.Simulation.Clock.CurrentTick,
                    MapName = game.Session.Simulation.Host.Current.Map.Name,
                    Crawlers = pawns.Count,
                    Agents = string.Join(";", pawns.Select(p => $"{p.Id}@{p.Position.X:F3},{p.Position.Y:F3} {p.Agent.Personality}")),
                    Fauceted = sim.EconomyLedger.TotalFaucetedGold,
                    Escrow = sim.EconomyLedger.TotalEscrowGold,
                    Stalls = string.Join(";", sim.ActiveStalls.Select(s => $"{s.StallEntityId} {s.TreasuryGold} {s.RationStock} {s.RationSellPrice}")),
                    Bounties = string.Join(";", sim.ActiveBounties.Select(b => $"{b.BountyId} {b.Status} {b.ExpirationTick} {b.RewardGold}")),
                    Explored = string.Join("", sectors.All.Select(s => $"{s.Id}{(sectors.IsExplored(s.Id) ? '+' : '-')}")),
                };
            }

            public void AssertSameAs(WorldCapture fresh)
            {
                Assert.Equal(fresh.Tick, Tick);
                Assert.Equal(fresh.MapName, MapName);
                Assert.Equal(fresh.Agents, Agents);
                Assert.Equal((fresh.Fauceted, fresh.Escrow), (Fauceted, Escrow));
                Assert.Equal(fresh.Stalls, Stalls);
                Assert.Equal(fresh.Bounties, Bounties);
                Assert.Equal(fresh.Explored, Explored);
            }
        }

        [Fact]
        public void TheCameraKeepsAFollowedCrawlerCentredAndAFloorClickDropsIt()
        {
            const float tolerance = 0.25f; // Unity's
            const int frames = 200;
            var ui = UiDriver.Start(new ClientOptions { TicksPerUpdate = 1 }, Width, Height);
            var game = ui.Game;
            game.Session.Simulation.Clock.SetTimeMultiplier(1f); // TicksPerUpdate runs a tick a frame
            game.Play.Dashboard.TogglePanels();
            ui.Frame(5);
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            ui.Click(game.World.View.WorldToScreen(crawler.Position));
            Assert.Equal(crawler.Id, game.Session.Selection.SelectedCrawler);
            Assert.True(game.Session.Selection.Following);
            Assert.Equal(game.Session.Simulation.Sectors.GetSectorAt(crawler.Position).Id, game.Session.Selection.SelectedSector);

            game.World.Rig.ZoomAt(game.World.Rig.Position, 0.01f); // as close as the rig goes, as Unity's scenario did
            ui.Frame(10); // a second of view time to reach it
            int checkedFrames = 0, misses = 0;
            float worst = 0f;
            for (int i = 0; i < frames; i++)
            {
                ui.Frame();
                var rig = game.World.Rig;
                float halfW = rig.Size * rig.Aspect, halfH = rig.Size;
                var frame = rig.Frame;
                var at = crawler.Position;
                bool centrable = at.X >= frame.XMin - CameraRig.Margin + halfW && at.X <= frame.XMax + CameraRig.Margin - halfW
                    && at.Y >= frame.YMin - CameraRig.Margin + halfH && at.Y <= frame.YMax + CameraRig.Margin - halfH;
                if (!centrable) continue;
                checkedFrames++;
                float d = Vec2.Distance(rig.Position, at);
                worst = System.Math.Max(worst, d);
                if (d > tolerance) misses++;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"follow: {checkedFrames} frames checked, {misses} beyond {tolerance} (worst {worst:F3})");
            Assert.True(checkedFrames >= frames / 2, $"Only {checkedFrames} follow frames could be checked.");
            Assert.True(misses == 0, $"The camera lost the crawler in {misses} of {checkedFrames} frames (worst {worst:F3}).");

            // A click on empty floor drops the crawler and selects the room
            var garden = game.Session.Simulation.Sectors.All.First(s => s.Id == Floor1Layout.GardenSectorId);
            game.World.Rig.Reframe();
            ui.Frame();
            var floor = EmptyFloor(game, garden.Bounds);
            ui.Click(game.World.View.WorldToScreen(floor));
            Assert.Equal(default, game.Session.Selection.SelectedCrawler);
            Assert.False(game.Session.Selection.Following);
            Assert.Equal(Floor1Layout.GardenSectorId, game.Session.Selection.SelectedSector);
        }

        [Fact]
        public void TheDevMapAndItsBannerLookAsRecorded()
        {
            var options = new ClientOptions { View = ViewMode.TopDown, TicksPerUpdate = 100, ConfigureServices = services => Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<ISessionDefinition>(services, new CanaryPenSession()) };
            var game = Offscreen.Start(options, frames: 1, Width, Height);
            game.Session.DashboardModel.Sessions.Switch();
            game.Play.Dashboard.TogglePanels();
            for (int i = 0; i < 3; i++) game.RunOneFrame();
            Assert.Equal(CanaryPenSession.SessionId, game.Session.Simulation.Host.Current.Id);
            game.Session.Simulation.Clock.SetTimeMultiplier(1f);
            game.Play.Dashboard.Refresh();
            RenderTests.CompareWithReference("render-devmap-banner-t300", FrameCapture.Capture(game, Width, Height), Width, Height);
        }

        /// <summary>A point in the room at least two units from any crawler.</summary>
        private static Vec2 EmptyFloor(SimpiensGame game, RectF room)
        {
            var pawns = game.Session.Simulation.Registry.ActiveEntities.Where(e => e.Type == EntityType.Pawn).Select(e => e.Position).ToList();
            for (float y = room.YMin + 1.5f; y < room.YMax - 1f; y += 0.5f)
            {
                for (float x = room.XMin + 1.5f; x < room.XMax - 1f; x += 0.5f)
                {
                    var p = new Vec2(x, y);
                    if (pawns.All(c => Vec2.Distance(c, p) > 2f)) return p;
                }
            }
            return room.Center;
        }
    }
}
