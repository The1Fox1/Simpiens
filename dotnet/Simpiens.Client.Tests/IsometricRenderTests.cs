using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Presentation;
using Simpiens.Simulation.Spatial;
using Simpiens.Testing.Sessions;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Isometric's render references (M3 plan step 2.3), at the game's default 1280x720: the outpost through the day,
    /// zoomed, with a selection, a fog lifting, the header over it, the Dev Map, and the wall styles for the producer's
    /// pick (P2). Recorded with <c>SIMPIENS_RECORD_RENDERS=1</c> after a look; top-down's are in <see cref="RenderTests"/>.
    /// </summary>
    public class IsometricRenderTests
    {
        private const int Width = 1280, Height = 720;

        private static SimpiensGame Outpost(int frames, int ticksPerUpdate = 100)
            => Offscreen.Start(new ClientOptions { View = ViewMode.Isometric, TicksPerUpdate = ticksPerUpdate }, frames: frames, width: Width, height: Height);

        private static void Compare(string name, SimpiensGame game, bool withUi = false)
            => RenderTests.CompareWithReference(name, withUi ? FrameCapture.Capture(game, Width, Height) : FrameCapture.CaptureMap(game, Width, Height), Width, Height);

        [Fact]
        public void TheIsometricOutpostAtTickZeroLooksAsRecorded()
        {
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.Isometric }, frames: 0, width: Width, height: Height);
            game.Session.Simulation.Clock.SetTimeMultiplier(0f);
            game.RunOneFrame();
            Compare("render-iso-t0", game);
        }

        [Fact]
        public void TheIsometricOutpostAtTick300LooksAsRecorded() => Compare("render-iso-t300", Outpost(3));

        [Fact]
        public void TheIsometricOutpostAtTick1500LooksAsRecorded() => Compare("render-iso-t1500", Outpost(15));

        [Fact]
        public void AZoomedIsometricViewWithASelectionLooksAsRecorded()
        {
            var game = Outpost(3);
            var hub = game.Session.Simulation.Host.Current.Map.Labels.First(l => l.Text == "Outpost Hub").Position;
            var crawler = game.Session.Simulation.Registry.ActiveEntities.Where(e => e.Type == EntityType.Pawn).OrderBy(e => (e.Position - hub).SqrMagnitude).First();
            game.Session.Selection.SelectCrawler(crawler.Id, follow: false);
            game.World.Rig.ZoomAt(hub, 0.3f);
            game.World.Rig.Pan(crawler.Position - game.World.Rig.Position);
            game.RunOneFrame();
            Compare("render-iso-t400-zoomed-selected", game);
        }

        [Fact]
        public void ACloudLiftingInIsometricLooksAsRecorded()
        {
            // Five ticks a frame until a scout's report starts a fade, then half a second (5 ticks) into its 1.5 s
            var game = Outpost(1, ticksPerUpdate: 5);
            for (int i = 0; i < 1000 && !game.World.Scene.Fog.Overlays.Any(o => o.FadeStart != null); i++) game.RunOneFrame();
            Assert.Contains(game.World.Scene.Fog.Overlays, o => o.FadeStart != null);
            game.RunOneFrame();
            Compare("render-iso-fog-lifting", game);
        }

        [Fact]
        public void TheHeaderOverTheIsometricMapLooksAsRecorded()
        {
            var game = Outpost(3);
            game.Play.Dashboard.TogglePanels();
            game.Session.Simulation.Clock.SetTimeMultiplier(1f);
            game.Play.Dashboard.Refresh();
            Compare("render-iso-header-t300", game, withUi: true);
        }

        [Fact]
        public void TheIsometricDevMapLooksAsRecorded()
        {
            var options = new ClientOptions { View = ViewMode.Isometric, TicksPerUpdate = 100, ConfigureServices = services => Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<ISessionDefinition>(services, new CanaryPenSession()) };
            var game = Offscreen.Start(options, frames: 1, Width, Height);
            game.Session.DashboardModel.Sessions.Switch();
            game.Play.Dashboard.TogglePanels();
            for (int i = 0; i < 3; i++) game.RunOneFrame();
            Assert.Equal(CanaryPenSession.SessionId, game.Session.Simulation.Host.Current.Id);
            game.Session.Simulation.Clock.SetTimeMultiplier(1f);
            game.Play.Dashboard.Refresh();
            Compare("render-iso-devmap-t300", game, withUi: true);
        }

        [Theory]
        [InlineData(WallStyle.Full, "render-iso-t300-walls-full")]
        [InlineData(WallStyle.Low, "render-iso-t300-walls-low")]
        public void TheOtherWallStylesLookAsRecorded(WallStyle style, string name)
        {
            var game = Outpost(3);
            game.World.Scene.WallStyle = style;
            Compare(name, game);
        }
    }
}
