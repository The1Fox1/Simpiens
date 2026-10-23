using System;
using System.IO;
using System.Linq;
using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Epic CLIENT M0's render gate: the outpost drawn by the real client, offscreen on Mesa's software renderer,
    /// compared with reference images committed in <c>Baselines/</c>. These are top-down's (the developers' view, asked for
    /// by name since isometric is the game's default; isometric's are in <c>IsometricRenderTests</c>). Each capture is also written to
    /// <c>render-output/</c> next to the test binary, for anyone (an agent included) to look at. The references are drawn
    /// in the pinned image (<c>scripts/test-client-in-container.sh</c>), where a render must match them exactly; elsewhere
    /// a driver's rounding is allowed. A deliberate change of what is drawn re-records them there, with <c>--record</c>
    /// (<c>SIMPIENS_RECORD_RENDERS=1</c> inside), in its own commit, after a look.
    /// </summary>
    public class RenderTests
    {
        /// <summary>Floor 1's camera frame at 16 pixels per unit, with a margin.</summary>
        private const int Width = 800, Height = 600;

        [Fact]
        public void TheOutpostAtTickZeroLooksAsRecorded()
        {
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.TopDown }, frames: 0);
            game.Session.Simulation.Clock.SetTimeMultiplier(0f); // the first frame initializes the game without a tick
            game.RunOneFrame();
            Assert.Equal(0L, game.Session.Simulation.Clock.CurrentTick);

            CompareWithReference("render-floor1-t0", FrameCapture.CaptureMap(game, Width, Height));
        }

        [Fact]
        public void TheOutpostAtTick300LooksAsRecorded()
        {
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.TopDown, TicksPerUpdate = 100 }, frames: 3);
            Assert.Equal(300L, game.Session.Simulation.Clock.CurrentTick);

            CompareWithReference("render-floor1-t300", FrameCapture.CaptureMap(game, Width, Height));
        }

        [Fact]
        public void ASelectedCrawlerHasItsRingAndName()
        {
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.TopDown, TicksPerUpdate = 100 }, frames: 3);
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == Simpiens.Simulation.Spatial.EntityType.Pawn);
            game.Session.Selection.SelectCrawler(crawler.Id, follow: false);

            CompareWithReference("render-floor1-t300-selected", FrameCapture.CaptureMap(game, Width, Height));
        }

        [Fact]
        public void TheOutpostAtTick1500LooksAsRecorded()
        {
            // Later in the day: the scout has charted rooms, so their fog has lifted
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.TopDown, TicksPerUpdate = 100 }, frames: 15);
            Assert.Equal(1500L, game.Session.Simulation.Clock.CurrentTick);

            CompareWithReference("render-floor1-t1500", FrameCapture.CaptureMap(game, Width, Height));
        }

        [Fact]
        public void AZoomedAndPannedViewLooksAsRecorded()
        {
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.TopDown, TicksPerUpdate = 100 }, frames: 3);
            var hub = game.Session.Simulation.Host.Current.Map.Labels.First(l => l.Text == "Outpost Hub").Position;
            game.World.Rig.ZoomAt(hub, 0.4f);
            game.World.Rig.Pan(new Vec2(2f, -3f));

            CompareWithReference("render-floor1-t300-zoomed", FrameCapture.CaptureMap(game, Width, Height));
        }

        internal static void CompareWithReference(string name, byte[] actual, int width = Width, int height = Height)
        {
            string output = Path.Combine(AppContext.BaseDirectory, "render-output");
            Png.Write(Path.Combine(output, name + ".png"), width, height, actual);

            string reference = Path.Combine(Repo.Root, "dotnet", "Simpiens.Client.Tests", "Baselines", name + ".png");
            if (Environment.GetEnvironmentVariable("SIMPIENS_RECORD_RENDERS") == "1")
            {
                Png.Write(reference, width, height, actual);
                return;
            }
            Assert.True(File.Exists(reference), $"No reference image {reference}; record one with SIMPIENS_RECORD_RENDERS=1 (and look at it first).");

            byte[] expected = Png.Decode(File.ReadAllBytes(reference), out int decodedWidth, out int decodedHeight);
            Assert.Equal((width, height), (decodedWidth, decodedHeight));

            // In the pinned image (scripts/client-tests.Containerfile) the references were drawn by the same Mesa and SDL,
            // so a render must match them byte for byte; elsewhere, a driver's rounding is allowed (review T2, epic F9)
            bool exact = Environment.GetEnvironmentVariable("SIMPIENS_RENDERS_EXACT") == "1";
            int channelTolerance = exact ? 0 : ChannelTolerance;
            double maxDifferingShare = exact ? 0.0 : MaxDifferingShare;

            var diff = new byte[actual.Length];
            int differing = 0;
            for (int i = 0; i < width * height; i++)
            {
                int worst = 0;
                for (int c = 0; c < 4; c++) worst = Math.Max(worst, Math.Abs(actual[i * 4 + c] - expected[i * 4 + c]));
                bool differs = worst > channelTolerance;
                if (differs) differing++;
                diff[i * 4] = differs ? (byte)255 : (byte)(expected[i * 4] / 4);
                diff[i * 4 + 1] = differs ? (byte)0 : (byte)(expected[i * 4 + 1] / 4);
                diff[i * 4 + 2] = differs ? (byte)255 : (byte)(expected[i * 4 + 2] / 4);
                diff[i * 4 + 3] = 255;
            }

            if (differing > width * height * maxDifferingShare)
            {
                Png.Write(Path.Combine(output, name + ".diff.png"), width, height, diff);
                Assert.Fail($"{name}: {differing} of {width * height} pixels differ from the reference by more than {channelTolerance}{(exact ? " (exact: the pinned renderer)" : "")}. Actual and diff: {output}");
            }
        }

        /// <summary>Per-channel difference that counts as the same pixel (rounding between drivers), outside the pinned image.</summary>
        private const int ChannelTolerance = 2;

        /// <summary>The share of pixels allowed to differ: 0.1%.</summary>
        private const double MaxDifferingShare = 0.001;
    }
}
