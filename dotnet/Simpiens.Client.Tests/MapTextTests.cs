using System;
using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Simpiens.Simulation.World;
using Simpiens.Testing.Sessions;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The map's text in screen space (Epic CLIENT text cleanup T3): the baked fonts' metrics, whole-pixel layout with
    /// kerning, room names sized by the zoom and never overlapping, "UNCHARTED" under its room, the selected name above
    /// its crawler.
    /// </summary>
    public class MapTextTests
    {
        private static readonly Lazy<FontLibrary> Fonts = new Lazy<FontLibrary>(() => FontLibrary.LoadFrom(AppContext.BaseDirectory));

        private static List<WorldLabel> RoomNames(MapDefinition map)
            => map.Labels.Select(l => new WorldLabel(l.Text, l.Position, LabelKind.Room, Rgba32.From(l.Color))).ToList();

        private static View CameraOn(MapDefinition map, float pixelsPerUnit) => new View(TopDownProjection.Instance, new ViewCamera(map.CameraFrame.Center, pixelsPerUnit, 1280, 720));

        [Fact]
        public void TheBakedFontsParseWithEveryCharacterAndTheirKerning()
        {
            foreach (var face in FontSet.All)
            {
                var font = Fonts.Value[face];
                Assert.True(font.GlyphCount >= 180, $"{face}: {font.GlyphCount} glyphs.");
                for (char c = ' '; c <= '~'; c++) Assert.True(font.HasGlyph(c), $"{face} lacks '{c}'.");
                Assert.True(font.KerningCount > 100, $"{face}: {font.KerningCount} kerning pairs.");
                Assert.True(font.LineHeight > face.Size && font.Base > 0 && font.Base <= font.LineHeight, $"{face}: line {font.LineHeight}, base {font.Base}.");
                Assert.NotEmpty(font.Pages);
            }
            Assert.Equal(Fonts.Value[FontSet.Bold(13)].Glyph('?').Id, Fonts.Value[FontSet.Bold(13)].Glyph('一').Id); // missing characters fall back to '?'
        }

        [Fact]
        public void TextIsLaidOutOnWholePixelsWithItsKerning()
        {
            var font = Fonts.Value[FontSet.Bold(16)];
            var (a, b) = new[] { ('T', 'o'), ('A', 'V'), ('V', 'a'), ('L', 'T'), ('P', 'a') }.First(p => font.Kerning(p.Item1, p.Item2) != 0);
            var glyphs = new List<ScreenGlyph>();
            MapLabelLayout.AddGlyphs(font, $"{a}{b}", 100, 50, new Rgba32(255, 255, 255, 255), glyphs);
            Assert.Equal(2, glyphs.Count);
            var ga = font.Glyph(a);
            var gb = font.Glyph(b);
            Assert.Equal(100 + ga.XOffset, glyphs[0].X);
            Assert.Equal(100 + ga.XAdvance + font.Kerning(a, b) + gb.XOffset, glyphs[1].X);
            Assert.Equal(50 + gb.YOffset, glyphs[1].Y);
            Assert.Equal(ga.XAdvance + font.Kerning(a, b) + gb.XAdvance, font.Measure($"{a}{b}"));
        }

        [Theory]
        [InlineData(16f, 16)]
        [InlineData(19f, 16)]
        [InlineData(24f, 24)]
        [InlineData(31f, 28)]
        [InlineData(80f, 32)]
        [InlineData(12.5f, 12)]
        [InlineData(9f, 0)]
        public void ARoomNameIsAWorldUnitTallInBakedStepsAndHiddenBelowTheSmallest(float pixelsPerUnit, int expected)
        {
            var labels = new List<WorldLabel> { new WorldLabel("Outpost Hub", Vec2.Zero, LabelKind.Room, new Rgba32(255, 255, 255, 255)) };
            var layout = new MapLabelLayout();
            layout.Layout(labels, new View(TopDownProjection.Instance, new ViewCamera(Vec2.Zero, pixelsPerUnit, 1280, 720)), Fonts.Value, new List<ScreenGlyph>());
            var placed = Assert.Single(layout.Placed);
            Assert.Equal(expected > 0, placed.Shown);
            if (expected > 0) Assert.Equal(FontSet.Bold(expected), placed.Face);
        }

        public static IEnumerable<object[]> MapsAndZooms()
        {
            foreach (var zoom in new[] { 16f, 24f, 32f, 48f, 64f, 96f, 120f })
            {
                yield return new object[] { "Floor1", zoom };
                yield return new object[] { "DevMap", zoom };
            }
        }

        [Theory]
        [MemberData(nameof(MapsAndZooms))]
        public void NoTwoNamesOnARowOverlapAndARowSharesOneSize(string mapName, float pixelsPerUnit)
        {
            var map = mapName == "Floor1" ? Floor1Layout.Definition : DevMapLayout.Definition;
            var labels = RoomNames(map);
            var layout = new MapLabelLayout();
            layout.Layout(labels, CameraOn(map, pixelsPerUnit), Fonts.Value, new List<ScreenGlyph>());
            var shown = layout.Placed.Where(p => p.Shown).ToList();
            foreach (var row in shown.GroupBy(p => MathF.Round(labels[p.Index].At.Y / MapLabelLayout.RowTolerance)))
            {
                Assert.Single(row.Select(p => p.Face).Distinct());
                var ordered = row.OrderBy(p => p.X).ToList();
                for (int i = 1; i < ordered.Count; i++)
                {
                    Assert.True(ordered[i].X - (ordered[i - 1].X + ordered[i - 1].Width) >= MapLabelLayout.Gap - 1,
                        $"{mapName} at {pixelsPerUnit} px/unit: '{labels[ordered[i - 1].Index].Text}' reaches '{labels[ordered[i].Index].Text}'.");
                }
            }
            if (mapName == "Floor1") Assert.Equal(labels.Count, shown.Count); // the outpost's rooms are wide: every name shows
        }

        [Fact]
        public void UnchartedSitsUnderItsRoomsNameOneSizeSmallerAndTheSelectedNameAboveItsCrawler()
        {
            using var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var identity = (Simpiens.Narrative.IAgentIdentityService)sim.Services.GetService(typeof(Simpiens.Narrative.IAgentIdentityService));
            var frames = TestFrames.Of(sim, 0);
            var selection = new WorldSelection(frames, sim.Sectors, identity);
            var crawler = sim.Registry.ActiveEntities.First(e => e.Agent != null);
            selection.SelectCrawler(crawler.Id, follow: false);
            var scene = new WorldScene();
            scene.Build(sim.Host.Current.Map, frames, 0f, selection);

            var map = sim.Host.Current.Map;
            Assert.Equal(map.Labels.Count, scene.Labels.Count(l => l.Kind == LabelKind.Room));
            Assert.Equal(scene.Fog.Overlays.Count, scene.Labels.Count(l => l.Kind == LabelKind.Uncharted));

            var camera = CameraOn(map, 16f);
            var layout = new MapLabelLayout();
            var glyphs = new List<ScreenGlyph>();
            layout.Layout(scene.Labels, camera, Fonts.Value, glyphs);
            for (int i = 0; i < scene.Labels.Count; i++)
            {
                if (scene.Labels[i].Kind != LabelKind.Uncharted) continue;
                var uncharted = layout.Placed.Single(p => p.Index == i);
                var room = layout.Placed.Single(p => p.Index != i && scene.Labels[p.Index].Kind == LabelKind.Room && scene.Labels[p.Index].At == scene.Labels[i].At);
                Assert.Equal(room.Y + room.Height, uncharted.Y);
                Assert.True(uncharted.Face.Size < room.Face.Size);
            }

            int name = scene.Labels.FindIndex(l => l.Kind == LabelKind.Name);
            Assert.Equal(identity.GetFriendlyName(crawler.Id), scene.Labels[name].Text);
            var placedName = layout.Placed.Single(p => p.Index == name);
            Assert.Equal(FontSet.Bold(MapLabelLayout.NameSize), placedName.Face);
            Assert.True(placedName.Y + placedName.Height <= MathF.Ceiling(camera.WorldToScreen(crawler.Position).Y), "The name sits above its crawler.");
            Assert.All(glyphs, g => Assert.True(g.Width > 0 && g.Height > 0));
        }

        [Fact]
        public void UnchartedFindsItsRoomBySectorNotByAnExactlyEqualPoint()
        {
            // Review S2: the pairing used to compare two floats for equality; a point a hair off lost its room
            var white = Rgba32.From(new Rgba(1f, 1f, 1f, 1f));
            var labels = new List<WorldLabel>
            {
                new WorldLabel("Outpost Hub", new Vec2(0f, 0f), LabelKind.Room, white, sectorId: 3),
                new WorldLabel("Garden", new Vec2(8f, 0f), LabelKind.Room, white, sectorId: 4),
                new WorldLabel(FogOfWar.UnchartedText, new Vec2(0.0001f, 0f), LabelKind.Uncharted, white, sectorId: 3),
            };
            var layout = new MapLabelLayout();
            layout.Layout(labels, CameraOn(Simpiens.Core.Session.Layouts.Floor1Layout.Definition, 16f), Fonts.Value, new List<ScreenGlyph>());
            var room = layout.Placed.Single(p => p.Index == 0);
            var uncharted = layout.Placed.Single(p => p.Index == 2);
            Assert.Equal(room.Y + room.Height, uncharted.Y);
            Assert.True(uncharted.Face.Size < room.Face.Size);
        }
    }
}
