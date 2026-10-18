using System;
using System.IO;
using System.Linq;
using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Epic CLIENT M0's render gate: the outpost drawn by the real client, offscreen on Mesa's software renderer,
    /// compared with reference images committed in <c>Baselines/</c>. Each capture is also written to
    /// <c>render-output/</c> next to the test binary, for anyone (an agent included) to look at. A deliberate change of
    /// what is drawn re-records the references with <c>SIMPIENS_RECORD_RENDERS=1</c>, in its own commit, after a look.
    /// </summary>
    public class RenderTests
    {
        /// <summary>Floor 1's camera frame at 16 pixels per unit, with a margin.</summary>
        private const int Width = 800, Height = 600;

        [Fact]
        public void TheOutpostAtTickZeroLooksAsRecorded()
        {
            var game = Offscreen.Start(frames: 0);
            game.Simulation.Clock.SetTimeMultiplier(0f); // the first frame initializes the game without a tick
            game.RunOneFrame();
            Assert.Equal(0L, game.Simulation.Clock.CurrentTick);

            CompareWithReference("render-floor1-t0", FrameCapture.Capture(game, Width, Height));
        }

        [Fact]
        public void TheOutpostAtTick300LooksAsRecorded()
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 100 }, frames: 3);
            Assert.Equal(300L, game.Simulation.Clock.CurrentTick);

            CompareWithReference("render-floor1-t300", FrameCapture.Capture(game, Width, Height));
        }

        [Fact]
        public void ASelectedCrawlerHasItsRingAndName()
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 100 }, frames: 3);
            var crawler = game.Simulation.Registry.ActiveEntities.First(e => e.Type == Simpiens.Simulation.Spatial.EntityType.Pawn);
            game.Selection.SelectCrawler(crawler.Id, follow: false);

            CompareWithReference("render-floor1-t300-selected", FrameCapture.Capture(game, Width, Height));
        }

        private static void CompareWithReference(string name, byte[] actual)
        {
            string output = Path.Combine(AppContext.BaseDirectory, "render-output");
            Png.Write(Path.Combine(output, name + ".png"), Width, Height, actual);

            string reference = Path.Combine(Repo.Root, "dotnet", "Simpiens.Client.Tests", "Baselines", name + ".png");
            if (Environment.GetEnvironmentVariable("SIMPIENS_RECORD_RENDERS") == "1")
            {
                Png.Write(reference, Width, Height, actual);
                return;
            }
            Assert.True(File.Exists(reference), $"No reference image {reference}; record one with SIMPIENS_RECORD_RENDERS=1 (and look at it first).");

            byte[] expected = Png.Decode(File.ReadAllBytes(reference), out int width, out int height);
            Assert.Equal((Width, Height), (width, height));

            var diff = new byte[actual.Length];
            int differing = 0;
            for (int i = 0; i < Width * Height; i++)
            {
                int worst = 0;
                for (int c = 0; c < 4; c++) worst = Math.Max(worst, Math.Abs(actual[i * 4 + c] - expected[i * 4 + c]));
                bool differs = worst > ChannelTolerance;
                if (differs) differing++;
                diff[i * 4] = differs ? (byte)255 : (byte)(expected[i * 4] / 4);
                diff[i * 4 + 1] = differs ? (byte)0 : (byte)(expected[i * 4 + 1] / 4);
                diff[i * 4 + 2] = differs ? (byte)255 : (byte)(expected[i * 4 + 2] / 4);
                diff[i * 4 + 3] = 255;
            }

            if (differing > Width * Height * MaxDifferingShare)
            {
                Png.Write(Path.Combine(output, name + ".diff.png"), Width, Height, diff);
                Assert.Fail($"{name}: {differing} of {Width * Height} pixels differ from the reference by more than {ChannelTolerance}. Actual and diff: {output}");
            }
        }

        /// <summary>Per-channel difference that counts as the same pixel (rounding between drivers).</summary>
        private const int ChannelTolerance = 2;

        /// <summary>The share of pixels allowed to differ: 0.1%.</summary>
        private const double MaxDifferingShare = 0.001;
    }
}
