using Simpiens.Client.Ui;
using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// One input frame (review U1): the game reads its input once per update, and Gum's cursor is the client's, fed from
    /// that frame with Gum's own mouse rules.
    /// </summary>
    public class ClientCursorTests
    {
        private static void Frame(ClientCursor cursor, double time, float x, float y, bool left = false, bool right = false, int wheel = 0)
        {
            cursor.Feed(new PointerInput { Position = new Vec2(x, y), Left = left, Right = right, WheelNotches = wheel });
            cursor.Activity(time);
        }

        [Fact]
        public void PushClickAndDoubleClickFollowGumsRules()
        {
            var cursor = new ClientCursor();
            Frame(cursor, 0.00, 10, 10);
            Frame(cursor, 0.02, 10, 10, left: true);
            Assert.True(cursor.PrimaryPush && cursor.PrimaryDown);
            Frame(cursor, 0.04, 12, 10);
            Assert.True(cursor.PrimaryClick);
            Assert.False(cursor.PrimaryDoubleClick);
            Assert.Equal(2, cursor.XChange);

            Frame(cursor, 0.10, 12, 10, left: true);
            Assert.True(cursor.PrimaryDoublePush);
            Frame(cursor, 0.12, 13, 11);
            Assert.True(cursor.PrimaryDoubleClick, "a second click within 0.25 s and 4 px");

            // A drag's release is a click, but never half of a double click
            Frame(cursor, 1.00, 50, 50, left: true);
            Frame(cursor, 1.02, 70, 50, left: true);
            Frame(cursor, 1.04, 70, 50);
            Assert.True(cursor.PrimaryClick);
            Frame(cursor, 1.06, 70, 50, left: true);
            Frame(cursor, 1.08, 70, 50);
            Assert.False(cursor.PrimaryDoubleClick);

            Frame(cursor, 2.00, 70, 50, right: true);
            Assert.True(cursor.SecondaryPush);
            Frame(cursor, 2.02, 70, 50);
            Assert.True(cursor.SecondaryClick);
        }

        [Fact]
        public void AWheelsNotchesCountOnce()
        {
            var cursor = new ClientCursor();
            Frame(cursor, 0.0, 0, 0, wheel: 2);
            Assert.Equal(2, cursor.ScrollWheelChange);
            cursor.Activity(0.02); // Gum updated again before the next frame was fed
            Assert.Equal(0, cursor.ScrollWheelChange);
        }

        private sealed class CountingInput : IClientInputSource
        {
            public int Reads;

            public void Read(out PointerInput pointer, out KeyInput keys)
            {
                Reads++;
                pointer = default;
                keys = default;
            }
        }

        [Fact]
        public void TheGameReadsItsInputOnceAnUpdateAndGumUsesTheClientsCursor()
        {
            var ui = UiDriver.Start();
            var game = ui.Game;
            Assert.Same(game.Ui.Cursor, Gum.Forms.FormsUtilities.Cursor); // installed as Gum supports it (GumService.Cursor is then null, by Gum's design)

            var counting = new CountingInput();
            game.Input = counting;
            ui.Frame(5);
            Assert.Equal(5, counting.Reads);

            game.Input = ui.Script;
            ui.MoveTo(new Vec2(321f, 123f));
            Assert.Equal(321, game.Ui.Cursor.X);
            Assert.Equal(new Vec2(321f, 123f), game.CurrentInput.Pointer.Position);
        }
    }
}
