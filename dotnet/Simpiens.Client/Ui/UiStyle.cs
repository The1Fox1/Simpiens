using Gum.Forms.DefaultVisuals.V3;
using Microsoft.Xna.Framework;
using Simpiens.Presentation;
using Simpiens.Entities;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation.Economy;

namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The dashboard's look: Lantern &amp; Stone (the UI polish plan, U1), the outpost's warm stone lit by the portraits'
    /// lantern, with one accent, lantern amber, for what is live or valuable. Its text is Inter's baked faces
    /// (<see cref="UiText"/>). Every colour the dashboard draws is a token here (review U3), so a reskin replaces this
    /// class and the panels don't change. It replaced the palette of Unity's <c>FacilityDashboard.uss</c> (Tailwind's
    /// slate and sky; Epic CLIENT M2, D1).
    /// </summary>
    public static class UiStyle
    {
        // Surfaces: warm grey stone, never brown (brown on brown reads as mud)
        public static readonly Color Backdrop = Hex(0x14110f);      // behind the dashboard's panels
        public static readonly Color Header = Hex(0x2a2420);        // the header bar: the lintel's stone
        public static readonly Color Panel = Hex(0x1c1815);         // panels and windows
        public static readonly Color Card = Hex(0x241f1b);          // rows in a list
        public static readonly Color CardBorder = Hex(0x3a312a);
        public static readonly Color Border = Hex(0x3b322b);
        public static readonly Color Control = Hex(0x3a312a);       // buttons
        public static readonly Color ControlBorder = Hex(0x5a4d42);
        public static readonly Color Inset = Hex(0x14110f);          // a box sunk into a card or a status bar
        public static readonly Color Veil = new Color(20, 14, 8, 115); // over the map under an open window: about 45%, so the map shows
        public static readonly Color SliderTrack = Hex(0x14110f);

        // Text: parchment on stone (13.7:1 on the panel; the muted text 5.3:1)
        public static readonly Color Text = Hex(0xefe2c6);
        public static readonly Color TextBright = Hex(0xf6ecd6);
        public static readonly Color TextTitle = Hex(0xfbf3e2);      // a window's or the dossier's title
        public static readonly Color TextLight = Hex(0xd9cbb0);      // event lines, form labels, values
        public static readonly Color TextSecondary = Hex(0xb9a88a);  // descriptions, subtitles, the clock
        public static readonly Color TextMuted = Hex(0x9a8a72);
        public static readonly Color BadgeText = Hex(0xfbf3e2);      // on a state badge's plate
        public static readonly Color TextOnAccent = Hex(0x2a1a08);   // on the amber primary button (8.2:1)

        // Meaning: one accent; the rest only for exceptions
        public static readonly Color Accent = Hex(0xf0a73a);        // lantern amber
        public static readonly Color Success = Hex(0x8fbf6a);
        public static readonly Color Warning = Hex(0xe8b64c);
        public static readonly Color Danger = Hex(0xd4573b);
        public static readonly Color Operational = Hex(0x8fbf6a);   // the status pill's words

        // The outpost's numbers and the bounty board
        public static readonly Color Gold = Hex(0xf0a73a);
        public static readonly Color GoldText = Hex(0xf6c66b);
        public static readonly Color Stock = Hex(0x8a7a63);         // rations and salvage: neutral, the icons carry the colour
        public static readonly Color StockText = Hex(0xefe2c6);
        public static readonly Color Bounty = Hex(0xb9803f);        // bronze
        public static readonly Color BountyText = Hex(0xe3b77a);

        // The sector cards' fog
        public static readonly Color Shrouded = Hex(0x14110f);
        public static readonly Color ShroudedBorder = Hex(0x3a312a);

        // A crawler's drive bars
        public static readonly Color DriveHealth = Hex(0x8fbf6a);  // green (Epic 10 G2)
        public static readonly Color DriveHunger = Hex(0xe0a040);
        public static readonly Color DriveEnergy = Hex(0x5a7fa6);  // the guild's blued steel
        public static readonly Color DriveSocial = Hex(0x5fa8a0);

        // The test-session banner (development builds)
        public static readonly Color TestBanner = Hex(0x7c2d12);
        public static readonly Color TestBannerBorder = Hex(0xf97316);
        public static readonly Color TestBannerText = Hex(0xffedd5);

        /// <summary>
        /// A crawler's state badge: a dark plate under light words, so a list of badges stays quiet and only panic, in
        /// the danger red, stands out (the words say the state; colour only helps).
        /// </summary>
        public static Color StateColor(AgentVisualState state) => state switch
        {
            AgentVisualState.Walking => Hex(0x2f3b4d),
            AgentVisualState.Harvesting => Hex(0x4f3d1c),
            AgentVisualState.Resting => Hex(0x3a3350),
            AgentVisualState.Sleeping => Hex(0x3a3350),
            AgentVisualState.Gossiping => Hex(0x24414a),
            AgentVisualState.Sharing => Hex(0x2f4a2a),
            AgentVisualState.Panicking => Hex(0xb8432c),
            AgentVisualState.Yielding => Hex(0x4a4220),
            _ => Hex(0x3a312a),
        };

        /// <summary>A bounty's status on its card: open, claimed, or done and gone.</summary>
        public static Color BountyStatusColor(BountyStatus status) => status == BountyStatus.Open ? Success : status == BountyStatus.Claimed ? Accent : TextMuted;

        /// <summary>Feedback: green, red and amber, warmed to the palette.</summary>
        public static Color ToneColor(FeedbackTone tone) => tone switch
        {
            FeedbackTone.Ok => Success,
            FeedbackTone.Error => Hex(0xe06a4a),
            FeedbackTone.Notice => Hex(0xf0c060),
            _ => Text,
        };

        /// <summary>
        /// The UI kit's pieces (<see cref="Simpiens.Presentation.Art.UiKit"/>) in the game's icon atlas, which
        /// <see cref="StoneSkin"/> draws controls from; null keeps Gum's look. Gum keeps its style in statics, and so does
        /// this: the game that initialized Gum last sets it (<see cref="UiLayer"/>).
        /// </summary>
        public static IconAtlas Kit { get; private set; }

        /// <summary>
        /// Sets Gum's built-in visuals to this palette and text size, and the kit controls are drawn from. Call once Gum is
        /// initialized, before building controls.
        /// </summary>
        public static void Apply(IconAtlas kit)
        {
            Kit = kit;
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

            // Gum's controls draw their text in these three styles: baked faces of Inter, 1:1 (no italic is baked)
            var text = style.Text;
            SetFace(text.Normal, UiText.Body);
            SetFace(text.Strong, UiText.BodyBold);
            SetFace(text.Emphasis, UiText.Body);

            // A text created without a style starts on a baked face, not Gum's built-in Arial
            Gum.GueDeriving.TextRuntime.DefaultFont = FontSet.Family;
            Gum.GueDeriving.TextRuntime.DefaultFontSize = UiText.Body.Size;
        }

        private static void SetFace(Gum.DataTypes.Variables.StateSave state, UiText style)
        {
            state.SetValue("Font", FontSet.Family, "string");
            state.SetValue("FontSize", style.Size, "int");
            state.SetValue("IsBold", style.Bold, "bool");
        }

        /// <summary>A CSS hex colour, 0xRRGGBB.</summary>
        public static Color Hex(uint rgb) => new Color((int)(rgb >> 16) & 0xff, (int)(rgb >> 8) & 0xff, (int)rgb & 0xff);
    }
}
