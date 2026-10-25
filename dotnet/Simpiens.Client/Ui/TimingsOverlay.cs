using System.Globalization;
using System.Text;
using Gum.Converters;
using Gum.GueDeriving;
using RenderingLibrary.Graphics;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The frame's timings by phase in the bottom right corner (review D1), for development builds: F3 shows and hides it.
    /// It reads <see cref="FrameTimings"/> four times a second while shown; hidden, it costs nothing.
    /// </summary>
    public sealed class TimingsOverlay
    {
        private const float RefreshSeconds = 0.25f;

        private readonly FrameTimings _timings;
        private readonly TextRuntime _text;
        private readonly StringBuilder _builder = new StringBuilder(256);
        private float _sinceRefresh = RefreshSeconds;

        public TimingsOverlay(UiLayer ui, FrameTimings timings)
        {
            _timings = timings;
            Root = UiParts.Pill(out _text, string.Empty, UiStyle.Header, UiStyle.Border, UiStyle.Text, UiText.Small);
            Root.Name = "frame-timings";
            Root.XUnits = GeneralUnitType.PixelsFromLarge;
            Root.XOrigin = HorizontalAlignment.Right;
            Root.X = -16f;
            Root.YUnits = GeneralUnitType.PixelsFromLarge;
            Root.YOrigin = VerticalAlignment.Bottom;
            Root.Y = -20f;
            Root.Visible = false;
            ui.Root.Children.Add(Root);
        }

        public ContainerRuntime Root { get; }

        public bool Shown => Root.Visible;

        /// <summary>The text shown last (tests read it).</summary>
        public string Text => _text.Text;

        public void Toggle()
        {
            Root.Visible = !Root.Visible;
            _sinceRefresh = RefreshSeconds; // shown: refresh on the next update
        }

        public void Update(float realSeconds)
        {
            if (!Root.Visible) return;
            _sinceRefresh += realSeconds;
            if (_sinceRefresh < RefreshSeconds) return;
            _sinceRefresh = 0f;

            _builder.Clear();
            _builder.Append("ms, mean / p99 of ").Append(_timings.Frames).Append(" frames");
            Line("tick", FramePhase.Tick);
            Line("ui update", FramePhase.UiUpdate);
            Line("screens", FramePhase.ScreenUpdate);
            Line("draw", FramePhase.ScreenDraw);
            Line("ui draw", FramePhase.UiDraw);
            _builder.Append('\n').Append("frame: ").Append(_timings.MeanFrame.ToString("F2", CultureInfo.InvariantCulture)).Append(" / ").Append(_timings.FramePercentile(99).ToString("F2", CultureInfo.InvariantCulture));
            UiParts.SetText(_text, _builder.ToString());
        }

        private void Line(string name, FramePhase phase)
            => _builder.Append('\n').Append(name).Append(": ").Append(_timings.Mean(phase).ToString("F2", CultureInfo.InvariantCulture)).Append(" / ").Append(_timings.Percentile(phase, 99).ToString("F2", CultureInfo.InvariantCulture));
    }
}
