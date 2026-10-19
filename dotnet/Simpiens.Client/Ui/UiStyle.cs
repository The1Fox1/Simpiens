using Gum.Forms.DefaultVisuals.V3;
using Microsoft.Xna.Framework;
using Simpiens.Presentation.Dashboard;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The dashboard's look (Epic CLIENT M2, D1): Gum's built-in controls in the palette of Unity's
    /// <c>FacilityDashboard.uss</c> (Tailwind's slate, sky, emerald, amber and red), with Gum's built-in font scaled to
    /// Unity's 11 to 14 px. A designer's reskin replaces this class; the panels don't change.
    /// </summary>
    public static class UiStyle
    {
        /// <summary>Gum's built-in font is Arial at 18 px; text is drawn at this scale (about 13.5 px).</summary>
        public const float TextScale = 0.75f;

        public static readonly Color Backdrop = Hex(0x090d16);      // behind the dashboard's panels
        public static readonly Color Header = Hex(0x0d1322);        // the header bar
        public static readonly Color Panel = Hex(0x0f172a);         // panels and windows
        public static readonly Color Card = Hex(0x162032);          // rows in a list
        public static readonly Color CardBorder = Hex(0x233149);
        public static readonly Color Border = Hex(0x1e293b);
        public static readonly Color Control = Hex(0x1e293b);       // buttons
        public static readonly Color ControlBorder = Hex(0x475569);
        public static readonly Color Text = Hex(0xe2e8f0);
        public static readonly Color TextBright = Hex(0xf1f5f9);
        public static readonly Color TextMuted = Hex(0x64748b);
        public static readonly Color Accent = Hex(0x38bdf8);        // sky
        public static readonly Color Success = Hex(0x10b981);       // emerald
        public static readonly Color Warning = Hex(0xf59e0b);       // amber
        public static readonly Color Danger = Hex(0xef4444);        // red

        /// <summary>Unity's feedback colours: green (0.1, 0.8, 0.4), red (0.95, 0.3, 0.3), amber (0.95, 0.8, 0.2).</summary>
        public static Color ToneColor(FeedbackTone tone) => tone switch
        {
            FeedbackTone.Ok => new Color(0.1f, 0.8f, 0.4f),
            FeedbackTone.Error => new Color(0.95f, 0.3f, 0.3f),
            FeedbackTone.Notice => new Color(0.95f, 0.8f, 0.2f),
            _ => Text,
        };

        /// <summary>Sets Gum's built-in visuals to this palette and text size. Call once Gum is initialized, before building controls.</summary>
        public static void Apply()
        {
            var style = Styling.ActiveStyle;
            var colors = style.Colors;
            colors.Primary = Control;
            colors.Accent = Accent;
            colors.Success = Success;
            colors.Warning = Warning;
            colors.Danger = Danger;
            colors.InputBackground = Card;
            colors.SurfaceVariant = Panel;
            colors.TextPrimary = Text;
            colors.TextMuted = TextMuted;
            colors.IconDefault = Text;

            var text = style.Text;
            foreach (var state in new[] { text.Normal, text.Strong, text.Emphasis }) state.SetValue("FontScale", TextScale, "float");
        }

        private static Color Hex(uint rgb) => new Color((int)(rgb >> 16) & 0xff, (int)(rgb >> 8) & 0xff, (int)rgb & 0xff);
    }
}
