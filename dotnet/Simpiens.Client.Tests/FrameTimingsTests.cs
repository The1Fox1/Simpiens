using System.Diagnostics;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The frame's timings by phase (review D1): what the probe and the F3 overlay report.</summary>
    public class FrameTimingsTests
    {
        private static long Milliseconds(double ms) => (long)(ms * Stopwatch.Frequency / 1000.0);

        /// <summary>A start this many milliseconds ago, so a test controls the duration Add records (plus the moments the call takes: the bounds allow for a slow first call).</summary>
        private static long Ago(double ms) => Stopwatch.GetTimestamp() - Milliseconds(ms);

        [Fact]
        public void EachPhaseAndTheWholeFrameHaveAMeanAndAPercentile()
        {
            var timings = new FrameTimings();
            for (int frame = 1; frame <= 100; frame++)
            {
                timings.Add(FramePhase.Tick, Ago(frame == 100 ? 50 : 1)); // one slow tick in a hundred frames
                timings.Add(FramePhase.ScreenDraw, Ago(2));
                timings.EndFrame();
            }

            Assert.Equal(100, timings.Frames);
            Assert.InRange(timings.Mean(FramePhase.Tick), 1.48, 1.7); // (99 x 1 + 50) / 100
            Assert.InRange(timings.Percentile(FramePhase.Tick, 99), 0.99, 1.5); // the 99th of 100 is still a fast one
            Assert.InRange(timings.Percentile(FramePhase.Tick, 100), 49.9, 50.9);
            Assert.InRange(timings.Mean(FramePhase.ScreenDraw), 1.99, 2.3);
            Assert.Equal(0.0, timings.Mean(FramePhase.UiDraw));
            Assert.InRange(timings.MeanFrame, 3.48, 3.9);
            Assert.InRange(timings.FramePercentile(100), 51.9, 53.0); // the slow frame's own total
        }

        [Fact]
        public void APhaseRunTwiceInAFrameCountsBothAndTheWindowKeepsTheLastFrames()
        {
            var timings = new FrameTimings();
            timings.Add(FramePhase.ScreenDraw, Ago(3));
            timings.Add(FramePhase.ScreenDraw, Ago(3)); // a capture's extra draw
            timings.EndFrame();
            Assert.InRange(timings.Mean(FramePhase.ScreenDraw), 5.98, 6.8);

            for (int i = 0; i < FrameTimings.Window; i++)
            {
                timings.Add(FramePhase.ScreenDraw, Ago(1));
                timings.EndFrame();
            }
            Assert.Equal(FrameTimings.Window, timings.Frames);
            Assert.InRange(timings.Percentile(FramePhase.ScreenDraw, 100), 0.99, 1.9); // the 6 ms frame has left the window

            timings.Clear();
            Assert.Equal(0, timings.Frames);
            Assert.Equal(0.0, timings.MeanFrame);
        }

        [Fact]
        public void RecordingAFrameAllocatesNothing()
        {
            var timings = new FrameTimings();
            timings.Add(FramePhase.Tick, FrameTimings.Start());
            timings.EndFrame();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                timings.Add(FramePhase.Tick, FrameTimings.Start());
                timings.Add(FramePhase.UiDraw, FrameTimings.Start());
                timings.EndFrame();
            }
            Assert.Equal(before, System.GC.GetAllocatedBytesForCurrentThread());
        }

        [Fact]
        public void TheGameTimesItsFramesAndF3ShowsThemInDevelopmentBuilds()
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 1, TimingsOverlay = true }, frames: 5);
            Assert.Equal(5, game.Timings.Frames);
            Assert.True(game.Timings.Mean(FramePhase.ScreenDraw) > 0.0, "the screens' draw is timed");
            Assert.True(game.Timings.Mean(FramePhase.Tick) > 0.0, "a tick every update is timed");
            Assert.False(game.TimingsOverlay.Shown);

            game.TimingsOverlay.Toggle();
            game.RunOneFrame();
            Assert.True(game.TimingsOverlay.Shown);
            Assert.Contains("tick: ", game.TimingsOverlay.Text);
            Assert.Contains("frame: ", game.TimingsOverlay.Text);

            var release = Offscreen.Start(new ClientOptions { TicksPerUpdate = 1 }, frames: 1);
            Assert.Null(release.TimingsOverlay);
        }
    }
}
