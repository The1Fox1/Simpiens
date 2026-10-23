using System;
using System.Diagnostics;

namespace Simpiens.Client
{
    /// <summary>The parts of a frame <see cref="FrameTimings"/> times, in the order the game runs them.</summary>
    public enum FramePhase
    {
        /// <summary>The simulation's ticks, with the views' frame capture on each (<see cref="ClientSession.Update"/>).</summary>
        Tick,
        /// <summary>Gum: layout and its own input.</summary>
        UiUpdate,
        /// <summary>The screens: input, the camera, the scene's fades and poses, the dashboard's refresh.</summary>
        ScreenUpdate,
        /// <summary>The screens: the world's scene presented, culled and sorted, its sprites and text submitted (or a cutscene's stage).</summary>
        ScreenDraw,
        /// <summary>Gum's draw.</summary>
        UiDraw,
    }

    /// <summary>
    /// How long each part of a frame takes (review D1), over the last <see cref="Window"/> frames: the mean and a high
    /// percentile per phase, and of the whole frame. Draw times are the CPU's: what the GPU does after submission isn't
    /// counted. Recording allocates nothing; the statistics sort a preallocated copy.
    /// </summary>
    public sealed class FrameTimings
    {
        /// <summary>Frames kept: four seconds at 60 fps.</summary>
        public const int Window = 240;

        private static readonly int PhaseCount = Enum.GetValues(typeof(FramePhase)).Length;
        private static readonly double MillisecondsPerTimestamp = 1000.0 / Stopwatch.Frequency;

        // One ring per phase and one for the whole frame (the last row), in timestamp units
        private readonly long[,] _samples = new long[PhaseCount + 1, Window];
        private readonly long[] _current = new long[PhaseCount];
        private readonly long[] _scratch = new long[Window];
        private int _next;

        /// <summary>Frames recorded so far, up to <see cref="Window"/>.</summary>
        public int Frames { get; private set; }

        /// <summary>A timestamp to pass to <see cref="Add"/> when the phase ends.</summary>
        public static long Start() => Stopwatch.GetTimestamp();

        /// <summary>
        /// Adds the time since <paramref name="start"/> to <paramref name="phase"/> in the current frame. A phase that runs
        /// twice in a frame (a capture's extra draw) counts both.
        /// </summary>
        public void Add(FramePhase phase, long start) => _current[(int)phase] += Stopwatch.GetTimestamp() - start;

        /// <summary>Closes the current frame: its phases join the window, and the next frame starts at zero.</summary>
        public void EndFrame()
        {
            long total = 0;
            for (int p = 0; p < PhaseCount; p++)
            {
                _samples[p, _next] = _current[p];
                total += _current[p];
                _current[p] = 0;
            }
            _samples[PhaseCount, _next] = total;
            _next = (_next + 1) % Window;
            if (Frames < Window) Frames++;
        }

        /// <summary>Forgets every frame (a measurement starts after its warm-up).</summary>
        public void Clear()
        {
            Array.Clear(_samples, 0, _samples.Length);
            Array.Clear(_current, 0, _current.Length);
            _next = 0;
            Frames = 0;
        }

        public double Mean(FramePhase phase) => MeanOf((int)phase);

        public double MeanFrame => MeanOf(PhaseCount);

        /// <summary>The time below which <paramref name="percent"/> of the window's frames spent in <paramref name="phase"/>, in milliseconds.</summary>
        public double Percentile(FramePhase phase, double percent) => PercentileOf((int)phase, percent);

        /// <summary>The same for the whole frame (the frames' own totals, not a sum of the phases' percentiles).</summary>
        public double FramePercentile(double percent) => PercentileOf(PhaseCount, percent);

        private double MeanOf(int row)
        {
            if (Frames == 0) return 0.0;
            long sum = 0;
            for (int i = 0; i < Frames; i++) sum += _samples[row, i];
            return sum * MillisecondsPerTimestamp / Frames;
        }

        private double PercentileOf(int row, double percent)
        {
            if (Frames == 0) return 0.0;
            for (int i = 0; i < Frames; i++) _scratch[i] = _samples[row, i];
            Array.Sort(_scratch, 0, Frames);
            int rank = (int)Math.Ceiling(percent / 100.0 * Frames) - 1;
            return _scratch[Math.Clamp(rank, 0, Frames - 1)] * MillisecondsPerTimestamp;
        }
    }
}
