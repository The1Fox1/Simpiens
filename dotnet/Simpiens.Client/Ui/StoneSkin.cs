using System.Runtime.CompilerServices;
using Gum.Forms.Controls;
using Gum.Forms.DefaultVisuals.V3;
using Gum.Managers;
using Microsoft.Xna.Framework;
using Simpiens.Presentation.Art;

namespace Simpiens.Client.Ui
{
    /// <summary>How a button is drawn from the kit: raised stone, or the amber primary (one per window, its main action).</summary>
    public enum ButtonLook
    {
        Stone,
        Primary,
    }

    /// <summary>
    /// Gum's controls drawn from the UI kit (the UI polish plan, U3): a button's background nine-slice points at the kit's
    /// piece for its state (idle, hover, pressed, disabled, or active while its window is open), its words take the
    /// state's colour and drop a pixel when pressed; a slider's track is the groove and its thumb a stone button. Gum's
    /// own state code runs first and the kit's after it, so Gum still decides which state a control is in.
    /// Without a kit (<see cref="UiStyle.Kit"/> unset) controls keep Gum's look.
    /// </summary>
    public static class StoneSkin
    {
        private sealed class Skin
        {
            public ButtonLook Look;
            public bool Active;
            public string Piece;
        }

        private static readonly ConditionalWeakTable<ButtonVisual, Skin> Skins = new ConditionalWeakTable<ButtonVisual, Skin>();

        /// <summary>Draws <paramref name="button"/> from the kit as <paramref name="look"/>.</summary>
        public static void Apply(Button button, ButtonLook look = ButtonLook.Stone)
        {
            if (button.Visual is ButtonVisual visual) Apply(visual, look);
        }

        /// <summary>Draws <paramref name="slider"/>'s track as the groove and its thumb as a stone button.</summary>
        public static void Apply(Slider slider)
        {
            var kit = UiStyle.Kit;
            if (kit == null || slider.Visual is not SliderVisual visual) return;
            PointAt(visual.TrackBackground, kit, UiKit.Groove, UiKit.GrooveCorner);
            visual.TrackBackground.Color = Color.White;
            Apply(visual.ThumbInstance, ButtonLook.Stone);
        }

        /// <summary>
        /// Lights <paramref name="button"/> as active (its window is open) or not. A button that isn't skinned ignores it.
        /// </summary>
        public static void SetActive(Button button, bool active)
        {
            if (button.Visual is not ButtonVisual visual || !Skins.TryGetValue(visual, out var skin) || skin.Active == active) return;
            skin.Active = active;
            button.UpdateState();
        }

        /// <summary>The kit piece <paramref name="button"/> shows now (tests read it); null when it isn't skinned.</summary>
        public static string PieceOf(Button button) => button.Visual is ButtonVisual visual && Skins.TryGetValue(visual, out var skin) ? skin.Piece : null;

        private static void Apply(ButtonVisual visual, ButtonLook look)
        {
            var kit = UiStyle.Kit;
            if (kit == null) return;
            var skin = new Skin { Look = look };
            Skins.AddOrUpdate(visual, skin);
            PointAt(visual.Background, kit, UiKit.Button, UiKit.Corner);
            foreach (var state in visual.ButtonCategory.States)
            {
                var gums = state.Apply;
                string name = state.Name;
                state.Apply = () =>
                {
                    gums?.Invoke();
                    Show(visual, kit, skin, name);
                };
            }
            Show(visual, kit, skin, "Enabled");
        }

        private static void Show(ButtonVisual visual, IconAtlas kit, Skin skin, string state)
        {
            bool primary = skin.Look == ButtonLook.Primary;
            bool disabled = state.StartsWith("Disabled", System.StringComparison.Ordinal);
            bool pushed = state == "Pushed";
            bool hover = state.StartsWith("Highlighted", System.StringComparison.Ordinal);
            string piece = disabled ? UiKit.ButtonDisabled
                : primary ? (pushed ? UiKit.PrimaryPressed : hover ? UiKit.PrimaryHover : UiKit.Primary)
                : pushed ? UiKit.ButtonPressed : skin.Active ? UiKit.ButtonActive : hover ? UiKit.ButtonHover : UiKit.Button;
            skin.Piece = piece;
            var source = kit.Kit(piece);
            var background = visual.Background;
            background.Color = Color.White;
            if (background.TextureLeft != source.X) background.TextureLeft = source.X;
            if (background.TextureTop != source.Y) background.TextureTop = source.Y;
            Color words = disabled ? UiStyle.TextMuted
                : primary ? UiStyle.TextOnAccent
                : skin.Active || hover ? UiStyle.TextBright : UiStyle.Text;
            visual.TextInstance.Color = words;
            visual.TextInstance.Y = pushed ? 1f : 0f; // the words sink with the stone
        }

        private static void PointAt(Gum.GueDeriving.NineSliceRuntime slice, IconAtlas kit, string piece, int corner)
        {
            var source = kit.Kit(piece);
            slice.Texture = kit.Texture;
            slice.TextureAddress = TextureAddress.Custom;
            slice.TextureLeft = source.X;
            slice.TextureTop = source.Y;
            slice.TextureWidth = source.Width;
            slice.TextureHeight = source.Height;
            slice.CustomFrameTextureCoordinateWidth = corner;
        }
    }
}
