using System;
using System.Collections.Generic;
using System.Text;
using Simpiens.Entities;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation.Economy;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The art catalogue (graphics V1): what each view mode draws, as data. Its pieces must be exactly what the art
    /// sets drew before the catalogue: sheet, sprite and the bits of every size and pivot, over every tile, node,
    /// crawler state and frame (with its micro-animation's squash), the hearth over time, the bunk, ring and shadow.
    /// </summary>
    public class ArtCatalogTests
    {
        private readonly ITestOutputHelper _output;

        public ArtCatalogTests(ITestOutputHelper output) => _output = output;

        private static readonly float[] Times = { 0f, 0.1f, 0.25f, 0.4f, 1.7f, 3.3f, 12.05f };

        [Theory]
        [InlineData("top-down")]
        [InlineData("isometric")]
        public void ThePiecesAreWhatTheArtSetsDrewBeforeTheCatalogue(string mode)
        {
            var art = mode == "top-down" ? (WorldArt)TopDownArt.Instance : IsometricArt.Instance;
            var text = new StringBuilder();
            ulong hash = Start;
            void Piece(string what, ArtPiece p)
            {
                text.AppendLine($"{what}: sheet {p.Sheet} sprite {p.Sprite} {p.Width:R}x{p.Height:R} pivot {p.PivotX:R},{p.PivotY:R}");
                hash = Add(hash, (int)p.Sheet);
                hash = Add(hash, p.Sprite);
                hash = Add(hash, BitConverter.SingleToInt32Bits(p.Width));
                hash = Add(hash, BitConverter.SingleToInt32Bits(p.Height));
                hash = Add(hash, BitConverter.SingleToInt32Bits(p.PivotX));
                hash = Add(hash, BitConverter.SingleToInt32Bits(p.PivotY));
            }

            foreach (TileKind kind in Enum.GetValues(typeof(TileKind))) Piece($"tile {kind}", art.Tile(kind));
            Piece("low wall", art.LowWall);
            for (int item = 0; item <= 7; item++) Piece($"node {(ItemType)item}", art.Resource((ItemType)item));
            foreach (float t in Times) Piece($"hearth at {t}", art.Hearth(t));
            Piece("bunk", art.Bunk);
            Piece("shadow", art.CrawlerShadow);
            Piece("ring", art.SelectionRing);
            foreach (AgentVisualState state in Enum.GetValues(typeof(AgentVisualState)))
            {
                foreach (float t in Times)
                {
                    CrawlerAppearance.MicroAnimation(state, t, out float tilt, out float scaleX, out float scaleY);
                    int frame = CrawlerAppearance.AnimationFrame(state, t);
                    Piece($"crawler {state} at {t}", art.Crawler(new CrawlerPose(state, frame, tilt, scaleX, scaleY)));
                }
            }
            hash = Add(hash, BitConverter.SingleToInt32Bits(art.NameOffset.X));
            hash = Add(hash, BitConverter.SingleToInt32Bits(art.NameOffset.Y));
            hash = Add(hash, BitConverter.SingleToInt32Bits(art.NameHeight));
            text.AppendLine($"name offset {art.NameOffset.X:R},{art.NameOffset.Y:R} height {art.NameHeight:R}");

            _output.WriteLine(text.ToString());
            string output = System.IO.Path.Combine(AppContext.BaseDirectory, "render-output");
            System.IO.Directory.CreateDirectory(output);
            System.IO.File.WriteAllText(System.IO.Path.Combine(output, $"art-pieces-{mode}.txt"), text.ToString());
            Assert.True(Pinned[mode] == hash, $"{mode}'s pieces changed: {hash}UL (pinned {Pinned[mode]}UL). Each piece is in the test's output.");
        }

        [Theory]
        [InlineData("top-down")]
        [InlineData("isometric")]
        public void EveryKeyTheSceneNamesResolves(string mode)
        {
            var catalog = mode == "top-down" ? TopDownArtTable.Catalog : IsometricArtTable.Catalog;
            foreach (TileKind kind in Enum.GetValues(typeof(TileKind))) Assert.True(catalog.TryId(ArtKeys.Tile(kind), out _), ArtKeys.Tile(kind));
            foreach (AgentVisualState state in Enum.GetValues(typeof(AgentVisualState)))
            {
                var crawler = catalog.Id(ArtKeys.Crawler(state));
                Assert.Equal(2, catalog[crawler].FrameCount);
            }
            foreach (var key in new[] { ArtKeys.TileWallLow, ArtKeys.PropHearth, ArtKeys.PropBunk, ArtKeys.SelectionRing, ArtKeys.NodeDefault })
                Assert.True(catalog.TryId(key, out _), key);
            Assert.Equal(mode == "isometric", catalog.TryId(ArtKeys.CrawlerShadow, out _));
            // A node by its item's catalog sprite key; an unknown key is the default node
            Assert.Equal(catalog.Id(ArtKeys.Node("Resource_OreVein")), catalog.Node("Resource_OreVein"));
            Assert.Equal(catalog.Id(ArtKeys.NodeDefault), catalog.Node("Item_Nothing"));
            Assert.Equal(catalog.Id(ArtKeys.NodeDefault), catalog.Node(null));
        }

        [Fact]
        public void TheHearthsFramesTurnAtSixAHertzAndItPulses()
        {
            var catalog = TopDownArtTable.Catalog;
            var hearth = catalog.Id(ArtKeys.PropHearth);
            Assert.Equal(catalog.Sprite(hearth, 0), catalog.Piece(hearth, 0.1f).Sprite);
            Assert.Equal(catalog.Sprite(hearth, 1), catalog.Piece(hearth, 0.2f).Sprite);
            Assert.Equal(catalog.Sprite(hearth, 0), catalog.Piece(hearth, 0.35f).Sprite);
            Assert.NotEqual(catalog.Piece(hearth, 0f).Width, catalog.Piece(hearth, 0.3f).Width);
        }

        [Fact]
        public void ABuilderRefusesBrokenRows()
        {
            Refused(b => b.Add("a", DrawSheet.Sprites, new[] { "Map_Floor" }, 1f, 1f).Add("a", DrawSheet.Sprites, new[] { "Map_Floor" }, 1f, 1f), "a key twice");
            Refused(b => b.Add("a", DrawSheet.Sprites, new[] { "Iso_Floor" }, 1f, 1f), "a sprite its sheet doesn't have");
            Refused(b => b.Add("a", DrawSheet.Sprites, new string[0], 1f, 1f), "no frames");
            Refused(b => b.Add("a", DrawSheet.Sprites, new[] { "Map_Floor" }, -1f, 1f), "a negative size");
            Refused(b => b.Add("a", DrawSheet.Sprites, new[] { "Map_Floor" }, 1f, 1f, unitScale: 0f), "no scale");
            Refused(b => b.Add("", DrawSheet.Sprites, new[] { "Map_Floor" }, 1f, 1f), "no key");
            Assert.Throws<KeyNotFoundException>(() => TopDownArtTable.Catalog.Id("prop/nothing"));
        }

        private static void Refused(Action<ArtCatalog.Builder> rows, string what)
        {
            var builder = new ArtCatalog.Builder("test");
            rows(builder);
            Assert.True(Record.Exception(() => builder.Build()) is ArgumentException, $"A catalogue with {what} is refused.");
        }

        /// <summary>Recorded from the art sets at 5e8c6cf, before the catalogue (graphics V1.3).</summary>
        private static readonly Dictionary<string, ulong> Pinned = new Dictionary<string, ulong>
        {
            ["top-down"] = 14863390491918432379UL,
            ["isometric"] = 4992552654820548980UL,
        };

        private const ulong Start = 14695981039346656037UL;

        private static ulong Add(ulong hash, int value)
        {
            for (int b = 0; b < 4; b++) hash = (hash ^ (byte)(value >> (8 * b))) * 1099511628211UL;
            return hash;
        }
    }
}
