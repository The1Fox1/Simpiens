using System;
using System.Linq;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Client.Ui;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The menus' icons (graphics V4.1): an icon drawn 1:1 at a whole pixel is its art, magnified, pixel for pixel, under
    /// Gum's point filter and its linear filter alike (as the baked fonts are, F8); every item's icon resolves.
    /// </summary>
    public class IconTests
    {
        private const int Width = 400, Height = 300, Left = 120, Top = 80;

        [Fact]
        public void AnIconDrawnAtAWholePixelIsItsArtUnderEitherFilter()
        {
            var game = Offscreen.Start(new ClientOptions(), frames: 1, Width, Height);
            var item = ItemCatalog.All.First(i => i.Rarity == ItemRarity.Rare);
            string key = ArtKeys.Item(item.ArtKey, ItemPicture.Icon);
            var icon = UiParts.Icon(game.Graphics.Icons, key);
            icon.X = Left;
            icon.Y = Top;
            game.Ui.Root.Children.Add(icon);
            var previous = RenderingLibrary.Graphics.Renderer.TextureFilter;
            try
            {
                RenderingLibrary.Graphics.Renderer.TextureFilter = TextureFilter.Point;
                var point = FrameCapture.Capture(game, Width, Height);
                RenderingLibrary.Graphics.Renderer.TextureFilter = TextureFilter.Linear;
                var linear = FrameCapture.Capture(game, Width, Height);

                var art = SpriteSheets.Icons[SpriteSheets.Icons.IndexOf(ItemSprites.IconName(item.ArtKey))];
                int checkedPixels = 0;
                for (int y = 0; y < IconAtlas.Size; y++)
                    for (int x = 0; x < IconAtlas.Size; x++)
                    {
                        var expected = art.Pixels[y / IconAtlas.Scale * art.Width + x / IconAtlas.Scale];
                        int at = ((Top + y) * Width + Left + x) * 4;
                        Assert.True(point[at] == linear[at] && point[at + 1] == linear[at + 1] && point[at + 2] == linear[at + 2], $"The filters differ at {x}, {y}.");
                        if (expected.A != 255) continue; // the rim's clear corners show what is under them
                        Assert.True(point[at] == expected.R && point[at + 1] == expected.G && point[at + 2] == expected.B, $"At {x}, {y}: {point[at]},{point[at + 1]},{point[at + 2]}, the art {expected}.");
                        checkedPixels++;
                    }
                Assert.True(checkedPixels > IconAtlas.Size * IconAtlas.Size / 2);
            }
            finally
            {
                RenderingLibrary.Graphics.Renderer.TextureFilter = previous;
                game.Ui.Root.Children.Remove(icon);
            }
        }

        [Fact]
        public void EveryItemsIconResolvesAndAMissingOneShowsTheMissingArt()
        {
            var game = Offscreen.Start(new ClientOptions(), frames: 0);
            var atlas = game.Graphics.Icons;
            var missing = atlas.Source("item/nothing/icon");
            foreach (var item in ItemCatalog.All)
            {
                var source = atlas.Source(ArtKeys.Item(item.ArtKey, ItemPicture.Icon));
                Assert.Equal((IconAtlas.Size, IconAtlas.Size), (source.Width, source.Height));
                Assert.NotEqual(missing, source);
            }
            Assert.Equal(missing, atlas.Source(null));
        }

        /// <summary>
        /// The dashboard's icons (graphics V4.3): in the merchant's rows, the dossier's gear line, beside the bounty
        /// board's picker and in the header's stock, each shows its item and lands on whole pixels (so it draws 1:1).
        /// </summary>
        [Fact]
        public void TheDashboardsIconsShowTheirItemsAtWholePixels()
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 10 }, frames: 3, width: 1280, height: 720);
            game.Options.DashboardRefreshSeconds = 0f;
            var crawler = game.Session.Simulation.Host.CrawlerStates[0];
            crawler.Inventory.TryEquip(new Simpiens.Simulation.Economy.ItemInstance(1, Simpiens.Simulation.Economy.ItemType.HazardGear, 1, 30, durability: 100));
            var model = game.Session.DashboardModel;
            model.SelectCrawler(crawler.AgentId);
            model.OpenMerchant();
            for (int i = 0; i < 3; i++) game.RunOneFrame();
            var dashboard = game.Play.Dashboard;

            var shown = new System.Collections.Generic.List<(string Where, Gum.GueDeriving.SpriteRuntime Icon, string Key)>();
            for (int i = 0; i < model.Merchant.Rows.Count; i++) shown.Add(($"merchant row {i}", dashboard.Merchant.Icons[i], model.Merchant.Rows[i].Icon));
            shown.Add(("dossier gear", dashboard.Panels.GearIcon, "item/hazard-gear/icon"));
            for (int i = 0; i < model.Header.Stock.Count; i++) shown.Add(($"header stock {i}", dashboard.Header.StockEntries[i].Icon, model.Header.Stock[i].Icon));
            Assert.Equal(model.Header.Stock.Count, dashboard.Header.StockEntries.Count);

            model.ToggleMerchant();
            model.OpenBounties();
            for (int i = 0; i < 3; i++) game.RunOneFrame();
            shown.Add(("bounty picker", dashboard.Bounties.ItemIcon, model.Bounties.ItemIcon));

            foreach (var (where, icon, key) in shown)
            {
                Assert.True(icon.Visible, $"{where}: hidden.");
                float left = icon.AbsoluteLeft, top = icon.AbsoluteTop;
                Assert.True(left == MathF.Round(left) && top == MathF.Round(top), $"{where}: at {left}, {top}, not on whole pixels.");
                int scale = icon.Width <= IconAtlas.SmallSize ? 1 : IconAtlas.Scale;
                Assert.Equal(game.Graphics.Icons.Source(key, scale), icon.SourceRectangle);
            }
        }
    }
}
