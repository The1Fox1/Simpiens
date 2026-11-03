using Gum.Forms.Controls;
using Simpiens.Client.Ui;
using Simpiens.Presentation.Art;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>Gum's controls drawn from the UI kit (the UI polish plan, U3): each button state shows its piece.</summary>
    public class StoneSkinTests
    {
        private static Button AddButton(UiDriver ui, Button button)
        {
            button.X = 100f;
            button.Y = 100f;
            ui.Game.Ui.Root.AddChild(button);
            ui.Frame();
            return button;
        }

        [Fact]
        public void AButtonIsStoneAndLightsWhenTheMouseIsOverIt()
        {
            var ui = UiDriver.Start();
            var button = AddButton(ui, UiParts.Button("STONE", 120f));
            Assert.Equal(UiKit.Button, StoneSkin.PieceOf(button));

            ui.MoveTo(new Simpiens.Vec2(160f, 110f));
            ui.Frame();
            Assert.Equal(UiKit.ButtonHover, StoneSkin.PieceOf(button));

            ui.MoveTo(new Simpiens.Vec2(600f, 500f));
            ui.Frame();
            Assert.Equal(UiKit.Button, StoneSkin.PieceOf(button));
        }

        [Fact]
        public void AnActiveButtonKeepsItsLampUntilItsWindowCloses()
        {
            var ui = UiDriver.Start();
            var button = AddButton(ui, UiParts.Button("GUILD", 120f));
            StoneSkin.SetActive(button, true);
            Assert.Equal(UiKit.ButtonActive, StoneSkin.PieceOf(button));
            StoneSkin.SetActive(button, false);
            Assert.Equal(UiKit.Button, StoneSkin.PieceOf(button));
        }

        [Fact]
        public void ADisabledButtonIsDullAndThePrimaryIsAmber()
        {
            var ui = UiDriver.Start();
            var off = AddButton(ui, UiParts.Button("OFF", 120f));
            off.IsEnabled = false;
            ui.Frame();
            Assert.Equal(UiKit.ButtonDisabled, StoneSkin.PieceOf(off));

            var primary = AddButton(ui, UiParts.PrimaryButton("SET PRICES", 120f));
            Assert.Equal(UiKit.Primary, StoneSkin.PieceOf(primary));
        }

        [Fact]
        public void TheBackgroundIsTheKitsPieceUntinted()
        {
            var ui = UiDriver.Start();
            var button = AddButton(ui, UiParts.Button("STONE", 120f));
            var background = ((Gum.Forms.DefaultVisuals.V3.ButtonVisual)button.Visual).Background;
            var source = ui.Game.Graphics.Icons.Kit(UiKit.Button);
            Assert.Same(ui.Game.Graphics.Icons.Texture, background.Texture);
            Assert.Equal((source.X, source.Y, source.Width, source.Height), (background.TextureLeft, background.TextureTop, background.TextureWidth, background.TextureHeight));
            Assert.Equal(Microsoft.Xna.Framework.Color.White, background.Color);
        }
    }
}
