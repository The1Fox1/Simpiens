using System;
using System.IO;
using Gum.Forms.Controls;
using Simpiens.Client.Ui;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The window's size (the producer, 2026-11-03): 1920 by 1080 by default, and the header's menu picks 1280 by 720,
    /// 1920 by 1080 or 2240 by 1260, or full screen. The window changes on the next update, the UI lays out for it, the
    /// pip of the window's size (or Full screen) is lit, and the host's settings file keeps the choice.
    /// </summary>
    public class WindowSizeTests
    {
        private static Vec2 Centre(FrameworkElement control)
            => new Vec2(control.AbsoluteLeft + control.ActualWidth * 0.5f, control.AbsoluteTop + control.ActualHeight * 0.5f);

        private static string TempSettings() => Path.Combine(Path.GetTempPath(), "simpiens-client-tests", Guid.NewGuid().ToString("N"), ClientSettings.FileName);

        /// <summary>The lit pip, or -1 when the window is another size.</summary>
        private static int LitPip(HeaderBar header)
        {
            int lit = -1;
            for (int i = 0; i < header.WindowSizeButtons.Count; i++)
            {
                if (StoneSkin.PieceOf(header.WindowSizeButtons[i]) != UiKit.ButtonActive) continue;
                Assert.Equal(-1, lit); // one pip at a time
                lit = i;
            }
            return lit;
        }

        [Fact]
        public void AGameOpensAtNineteenTwentyByTenEighty()
        {
            var options = new ClientOptions();
            Assert.Equal((1920, 1080), (options.Width, options.Height));
            Assert.Null(options.SettingsFile); // nothing is kept unless the host asks
            Assert.Equal(new[] { "1280 × 720", "1920 × 1080", "2240 × 1260" }, Array.ConvertAll(WindowSizes.All, WindowSizes.Text));
        }

        [Fact]
        public void TheMenuPicksTheWindowsSizeAndTheHeaderFollows()
        {
            string settings = TempSettings();
            var ui = UiDriver.Start(new ClientOptions { SettingsFile = settings }, 1280, 720);
            var game = ui.Game;
            var header = game.Play.Dashboard.Header;
            ui.Click(Centre(header.MenuButton));
            Assert.True(header.MenuOpen);
            Assert.Equal(0, LitPip(header));

            ui.Click(Centre(header.WindowSizeButtons[2]));
            ui.Frame(); // the window changes at the start of the next update
            Assert.Equal((2240, 1260), (game.GraphicsDevice.Viewport.Width, game.GraphicsDevice.Viewport.Height));
            Assert.Equal(2, LitPip(header));
            Assert.True(header.MenuOpen, "The menu stays open on the new size.");
            Assert.Equal(2240f - 16f, header.MenuButton.AbsoluteLeft + header.MenuButton.ActualWidth, 0.5f); // the header spans the new width
            var kept = ClientSettings.Load(settings);
            Assert.Equal((2240, 1260), (kept.WindowWidth, kept.WindowHeight));

            ui.Click(Centre(header.WindowSizeButtons[1]));
            ui.Frame();
            Assert.Equal((1920, 1080), (game.GraphicsDevice.Viewport.Width, game.GraphicsDevice.Viewport.Height));
            Assert.Equal(1, LitPip(header));
            Assert.Equal(1920f - 16f, header.MenuButton.AbsoluteLeft + header.MenuButton.ActualWidth, 0.5f);
            Assert.Equal("window=1920x1080\nfullscreen=false\n", File.ReadAllText(settings));

            header.BindWindowSize(1500, 900, fullScreen: false); // dragged to a size of its own: no pip lit
            Assert.Equal(-1, LitPip(header));
        }

        [Fact]
        public void FullScreenFillsTheScreenAndGivesBackTheWindow()
        {
            string settings = TempSettings();
            var ui = UiDriver.Start(new ClientOptions { SettingsFile = settings }, 1280, 720);
            var game = ui.Game;
            var header = game.Play.Dashboard.Header;
            ui.Click(Centre(header.MenuButton));
            Assert.NotEqual(UiKit.ButtonActive, StoneSkin.PieceOf(header.FullScreenButton));

            ui.Click(Centre(header.FullScreenButton));
            ui.Frame();
            Assert.True(game.IsFullScreen);
            var screen = game.GraphicsDevice.Adapter.CurrentDisplayMode;
            Assert.Equal((screen.Width, screen.Height), (game.GraphicsDevice.Viewport.Width, game.GraphicsDevice.Viewport.Height));
            Assert.Equal(UiKit.ButtonActive, StoneSkin.PieceOf(header.FullScreenButton));
            Assert.Equal(-1, LitPip(header)); // the screen's size isn't a window's
            var kept = ClientSettings.Load(settings);
            Assert.True(kept.FullScreen);
            Assert.Equal((1280, 720), (kept.WindowWidth, kept.WindowHeight)); // the window it leaves for

            ui.Click(Centre(header.FullScreenButton)); // back to the window it left
            ui.Frame();
            Assert.False(game.IsFullScreen);
            Assert.Equal((1280, 720), (game.GraphicsDevice.Viewport.Width, game.GraphicsDevice.Viewport.Height));
            Assert.Equal(0, LitPip(header));
            Assert.False(ClientSettings.Load(settings).FullScreen);

            game.RequestFullScreen(true); // and a size from full screen leaves it for that window
            ui.Frame();
            Assert.True(game.IsFullScreen);
            ui.Click(Centre(header.WindowSizeButtons[1]));
            ui.Frame();
            Assert.False(game.IsFullScreen);
            Assert.Equal((1920, 1080), (game.GraphicsDevice.Viewport.Width, game.GraphicsDevice.Viewport.Height));
            Assert.Equal(1, LitPip(header));
        }

        [Fact]
        public void SettingsSurviveAMissingOrOddFile()
        {
            string path = TempSettings();
            Assert.False(ClientSettings.Load(path).HasWindowSize);
            Assert.False(ClientSettings.Load(null).HasWindowSize);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            foreach (string odd in new[] { "window=wide", "window=0x720", "window=1920x", "window=-1920x1080", "window=99999x1080", "1920x1080" })
            {
                File.WriteAllText(path, odd);
                Assert.False(ClientSettings.Load(path).HasWindowSize, $"'{odd}' gave a size.");
            }

            File.WriteAllText(path, "# a note\nsomething=else\n window = 2560x1440 \n");
            var read = ClientSettings.Load(path);
            Assert.Equal((2560, 1440), (read.WindowWidth, read.WindowHeight));

            new ClientSettings { WindowWidth = 1280, WindowHeight = 720 }.Save(path);
            read = ClientSettings.Load(path);
            Assert.Equal((1280, 720), (read.WindowWidth, read.WindowHeight));
            Assert.False(read.FullScreen);

            new ClientSettings { WindowWidth = 1920, WindowHeight = 1080, FullScreen = true }.Save(path);
            read = ClientSettings.Load(path);
            Assert.True(read.FullScreen);
            Assert.Equal((1920, 1080), (read.WindowWidth, read.WindowHeight));
            File.WriteAllText(path, "fullscreen=maybe\n");
            Assert.False(ClientSettings.Load(path).FullScreen);
        }
    }
}
