using System;
using Simpiens.Client.Ui;
using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>The key help (Epic UI-Beta B5): F1 shows every binding, from the bindings themselves, and hides them again.</summary>
    public class KeyHelpTests
    {
        [Fact]
        public void TheKeyHelpListsEveryBindingFromTheBindings()
        {
            var bindings = KeyBindings.Default();
            string text = KeyHelpOverlay.Describe(bindings);
            int bound = 0;
            foreach (InputAction action in Enum.GetValues<InputAction>())
                foreach (var b in bindings.All)
                    if (b.Action == action) { bound++; break; }
            Assert.Equal(bound, text.Split('\n').Length); // a line per bound action
            Assert.Contains("Pan left: A, Left", text);
            Assert.Contains("Speed 10x: 4", text);
            Assert.Contains("These keys: F1", text);
            bindings.Bind(Microsoft.Xna.Framework.Input.Keys.Q, InputAction.Pause);
            Assert.Contains("Pause: P, Q", KeyHelpOverlay.Describe(bindings)); // a rebinding shows
        }

        [Fact]
        public void F1ShowsAndHidesIt()
        {
            var ui = UiDriver.Start(width: 1280, height: 720);
            var help = ui.Game.Play.Help;
            Assert.False(help.Shown);
            help.Toggle(KeyBindings.Default());
            ui.Frame();
            Assert.True(help.Shown);
            Assert.Contains("Dashboard or map: Tab", help.Text);
            help.Toggle(KeyBindings.Default());
            Assert.False(help.Shown);
        }
    }
}
