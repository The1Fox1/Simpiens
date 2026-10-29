using System;
using System.Linq;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>Item art (graphics V3.6): a look per item and an item per look, every shape used, and pictures made as the kit says.</summary>
    public class ItemArtTests
    {
        [Fact]
        public void EveryItemHasALookAndEveryLookAnItem()
        {
            var keys = ItemCatalog.All.Select(i => i.ArtKey).ToHashSet();
            foreach (var key in keys) Assert.True(ItemArt.TryGet(key, out _), $"'{key}' has no look in ItemArt.");
            foreach (var look in ItemArt.All) Assert.True(keys.Contains(look.Key), $"ItemArt describes '{look.Key}', which no item is.");
            Assert.Equal(keys.Count, ItemSprites.Keys.Count);
        }

        [Fact]
        public void EveryShapeIsUsedAndWellFormed()
        {
            var used = ItemArt.All.Select(l => l.Value.Shape).ToHashSet();
            foreach (var (name, rows) in ItemShapes.All)
            {
                Assert.True(used.Contains(name), $"No item has the shape '{name}'.");
                Assert.Equal(ItemShapes.GlyphSize, rows.Length);
                Assert.All(rows, r => Assert.Equal(ItemShapes.GlyphSize, r.Length));
            }
            Assert.All(ItemArt.All, l => Assert.True(ItemShapes.All.ContainsKey(l.Value.Shape), $"'{l.Key}' has an unknown shape."));
        }

        [Fact]
        public void MapSpritesStandOnTheirFeetAndIconsWearTheirRarity()
        {
            foreach (var item in ItemCatalog.All)
            {
                var map = SpriteSheets.World[SpriteSheets.World.IndexOf(ItemSprites.MapName(item.ArtKey))];
                bool Opaque(int row) => Enumerable.Range(0, map.Width).Any(x => map.Pixels[row * map.Width + x].A != 0);
                Assert.True(Opaque(map.Height - 1), $"{item.Name}'s map sprite doesn't reach the bottom of its canvas.");
                Assert.False(Opaque(0), $"{item.Name}'s map sprite touches the top of its canvas.");

                var icon = SpriteSheets.Icons[SpriteSheets.Icons.IndexOf(ItemSprites.IconName(item.ArtKey))];
                Assert.Equal(RarityColours.Of(item.Rarity), icon.Pixels[SpriteKit.IconSize / 2]);
            }
        }

        [Fact]
        public void AnItemRowMustNameAPicture()
        {
            var builder = new ArtCatalog.Builder("test").Add(ArtKeys.Missing, DrawSheet.World, new[] { MissingArt.Name }, 1f, 1f)
                .Add("item/ration/hat", DrawSheet.World, new[] { "Map_Floor" }, 1f, 1f);
            Assert.Throws<ArgumentException>(() => builder.Build());
        }
    }
}
