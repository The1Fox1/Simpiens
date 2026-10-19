using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Gum.Forms.Controls;
using Gum.Wireframe;
using MonoGameGum.GueDeriving;
using Simpiens.Core.Session;
using Simpiens.Presentation;
using Simpiens.Simulation.Spatial;
using Simpiens.Testing.Sessions;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>The UI's text on the baked fonts (Epic CLIENT text cleanup T2): drawn 1:1, every face found.</summary>
    public class UiTextTests
    {
        private const int Width = 1280, Height = 720;

        private static Vec2 Centre(FrameworkElement control)
            => new Vec2(control.AbsoluteLeft + control.ActualWidth * 0.5f, control.AbsoluteTop + control.ActualHeight * 0.5f);

        [Fact]
        public void EveryTextInTheUiIsABakedFaceDrawnOneToOne()
        {
            var errors = new List<string>();
            void Collect(string message) => errors.Add(message);
            CustomSetPropertyOnRenderable.PropertyAssignmentError += Collect;
            var seen = new HashSet<TextRuntime>();
            try
            {
                var options = new ClientOptions { ConfigureServices = services => Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<ISessionDefinition>(services, new CanaryPenSession()) };
                var ui = UiDriver.Start(options, Width, Height);
                var game = ui.Game;

                // The panels with a dossier
                var crawler = game.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
                game.DashboardModel.SelectCrawler(crawler.Id);
                ui.Ticks(300);
                Texts(game, seen);

                // The map with the merchant, then the bounty board with its drop-downs opened
                game.Dashboard.TogglePanels();
                ui.Click(Centre(game.Dashboard.Header.MerchantButton));
                Texts(game, seen);
                ui.Click(Centre(game.Dashboard.Header.BountiesButton));
                foreach (var box in new[] { game.Dashboard.Bounties.TypeBox, game.Dashboard.Bounties.SectorBox })
                {
                    ui.Click(Centre(box));
                    Texts(game, seen);
                    ui.Click(Centre(box));
                }
                game.DashboardModel.CloseWindow();

                // A save's message, and the Dev Map with its banner
                ui.Click(Centre(game.Dashboard.Header.SaveButton));
                for (int i = 0; i < 100 && game.DashboardModel.Saves.IsSaving; i++) ui.Frame();
                ui.Click(Centre(game.Dashboard.Sessions.Button));
                Texts(game, seen);
            }
            finally
            {
                CustomSetPropertyOnRenderable.PropertyAssignmentError -= Collect;
            }

            Assert.Empty(errors);
            Assert.True(seen.Count > 100, $"Only {seen.Count} texts were looked at.");
            var faces = new HashSet<FontFace>(FontSet.All);
            foreach (var text in seen)
            {
                string what = $"'{text.Text}' ({text.Font} {text.FontSize}{(text.IsBold ? " bold" : "")}, scale {text.FontScale})";
                Assert.True(text.FontScale == 1f, $"{what} is scaled.");
                var face = new FontFace(text.Font, (int)text.FontSize, text.IsBold);
                Assert.True(faces.Contains(face) && text.FontSize == face.Size, $"{what} is not a baked face.");
                Assert.Equal(LineHeight(face), text.BitmapFont?.LineHeightInPixels);
            }
        }

        private static void Texts(SimpiensGame game, HashSet<TextRuntime> seen)
        {
            Walk(game.Ui.Root, seen);
            Walk(Gum.Forms.Controls.FrameworkElement.PopupRoot, seen);
            Walk(Gum.Forms.Controls.FrameworkElement.ModalRoot, seen);
        }

        private static void Walk(GraphicalUiElement element, HashSet<TextRuntime> seen)
        {
            if (element == null) return;
            if (element is TextRuntime text && text.AbsoluteVisible && !string.IsNullOrEmpty(text.Text)) seen.Add(text);
            foreach (var child in element.Children)
            {
                if (child is GraphicalUiElement gue) Walk(gue, seen);
            }
        }

        private static readonly Dictionary<FontFace, int> LineHeights = new Dictionary<FontFace, int>();

        private static int LineHeight(FontFace face)
        {
            if (LineHeights.TryGetValue(face, out int height)) return height;
            string fnt = File.ReadAllText(Path.Combine(System.AppContext.BaseDirectory, FontSet.ContentFolder, face.CacheFileName));
            return LineHeights[face] = int.Parse(Regex.Match(fnt, "lineHeight=(\\d+)").Groups[1].Value);
        }
    }
}
