using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Entities;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The red coat a hurt crawler wears (Epic 10 Part 2, S3, D71): it blinks twice a second and never hides the crawler;
    /// the scene draws it in the crawler's own sprite, right over it, only while the frame says it is hurt and only where
    /// the fog lets the crawler show; and the renderer fills exactly the sprite's pixels with its colour.
    /// </summary>
    public class HurtCoatTests
    {
        private static readonly Vec2 Sump = new Vec2(-16f, -12f); // charted from the start

        [Fact]
        public void TheCoatBlinksTwiceASecondAndNeverHidesTheCrawler()
        {
            int blinks = 0;
            bool on = HurtCoat.Strength(-0.001f) > 0f;
            float most = 0f, least = 1f;
            for (int ms = 0; ms < 1000; ms++)
            {
                float strength = HurtCoat.Strength(ms / 1000f);
                if (strength > 0f && !on) blinks++;
                on = strength > 0f;
                most = MathF.Max(most, strength);
                least = MathF.Min(least, strength);
            }
            Assert.Equal(2, blinks); // under the 3 flashes a second of photosensitivity guidance
            Assert.Equal(HurtCoat.Peak, most);
            Assert.True(most < 1f, "At its strongest the crawler still shows through.");
            Assert.Equal(0f, least); // and between blinks it is off
        }

        [Fact]
        public void AHurtCrawlerWearsOneCoatInItsOwnSpriteRightOverIt()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var map = sim.Host.Current.Map;
            var hurt = sim.Host.CrawlerStates[0];
            hurt.Position = Sump;
            sim.Step(1);
            Assert.True(hurt.HurtThisTick > 0f, "In the Sump, it is hurt.");

            foreach (var mode in new[] { ViewMode.TopDown, ViewMode.Isometric })
            {
                var scene = new WorldScene { Mode = mode };
                scene.Build(map, TestFrames.Of(sim), 0f); // the blink's first step
                var items = scene.Items;
                int coats = 0, hurtCoats = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    var coat = items[i];
                    if (!coat.Silhouette) continue;
                    coats++;
                    var under = items[i - 1]; // drawn right after its crawler
                    Assert.Equal(DrawRole.Crawler, DrawSortKey.Role(coat.SortKey));
                    Assert.False(under.Silhouette);
                    Assert.True(under.Sheet == coat.Sheet && under.SpriteIndex == coat.SpriteIndex && under.X == coat.X && under.Y == coat.Y && under.Z == coat.Z
                        && under.Width == coat.Width && under.Height == coat.Height && under.PivotX == coat.PivotX && under.PivotY == coat.PivotY
                        && under.Rotation == coat.Rotation && under.SnapToPixel == coat.SnapToPixel && under.Pick == coat.Pick, $"{mode.Name}: the coat is its crawler's sprite, where it stands.");
                    var wearer = items.PickedEntity(coat.Pick);
                    Assert.True(sim.Host.CrawlerStates.First(c => c.AgentId == wearer).HurtThisTick > 0f, $"{mode.Name}: only a hurt crawler wears a coat.");
                    Assert.Equal(HurtCoat.Colour.R, coat.Tint.R);
                    Assert.Equal(HurtCoat.Colour.G, coat.Tint.G);
                    Assert.Equal(HurtCoat.Colour.B, coat.Tint.B);
                    Assert.Equal((byte)(255 * HurtCoat.Peak), coat.Tint.A);
                    if (wearer == hurt.AgentId) hurtCoats++;
                }
                TestContext.Current.TestOutputHelper?.WriteLine($"{mode.Name}: {coats} coats, {sim.Host.CrawlerStates.Count(c => c.HurtThisTick > 0f)} crawlers hurt");
                Assert.Equal(1, hurtCoats);

                scene.Build(map, TestFrames.Of(sim), 0.4f); // between blinks
                for (int i = 0; i < scene.Items.Count; i++) Assert.False(scene.Items[i].Silhouette, $"{mode.Name}: between blinks no coat shows.");
            }

            sim.Sectors.SetExplored(Floor1Layout.SumpSectorId, false); // the fog hides what an uncharted sector holds, its coats too
            var fogged = new WorldScene { Mode = ViewMode.TopDown };
            fogged.Build(map, TestFrames.Of(sim), 0f);
            for (int i = 0; i < fogged.Items.Count; i++)
                Assert.False(fogged.Items[i].Pick >= 0 && fogged.Items.PickedEntity(fogged.Items[i].Pick) == hurt.AgentId, "Under the fog neither the crawler nor its coat shows.");
        }

        public static TheoryData<string, float> Zooms => new TheoryData<string, float>
        {
            { "top-down", 2f }, { "top-down", 2.37f }, { "top-down", 3f },
            { "isometric", 2f }, { "isometric", 2.37f }, { "isometric", 3f },
        };

        /// <summary>
        /// Drawn through the renderer, at whole and in-between zooms: a coat at full strength turns exactly the sprite's
        /// solid pixels its colour and touches nothing else, so it can't slip off its crawler; at half strength it is half
        /// colour, half crawler (blended premultiplied, as the effect writes it).
        /// </summary>
        [Theory]
        [MemberData(nameof(Zooms))]
        public void TheCoatCoversExactlyItsCrawlersPixels(string modeName, float zoom)
        {
            const int Size = 96;
            var mode = modeName == "isometric" ? ViewMode.Isometric : ViewMode.TopDown;
            var game = Offscreen.Start();
            var view = new View(mode.Projection, new ViewCamera(new Vec2(0.3f, -0.2f), zoom * mode.Projection.ArtPixelsPerUnit, Size, Size));
            var piece = mode.Catalog.Piece(new EntityVisuals(mode.Catalog).Crawler(AgentVisualState.Walking), 0f);
            var dark = new Color(40, 44, 52, 255);
            var light = new Color(220, 216, 200, 255);
            var colour = HurtCoat.Colour;

            Color[] Draw(Color background, byte coat)
            {
                var list = new DrawList();
                Stand(list, piece, new Rgba32(255, 255, 255, 255), silhouette: false);
                if (coat > 0) Stand(list, piece, new Rgba32(colour.R, colour.G, colour.B, coat), silhouette: true);
                var device = game.GraphicsDevice;
                using var target = new RenderTarget2D(device, Size, Size);
                device.SetRenderTarget(target);
                device.Clear(background);
                game.Graphics.Renderer.Draw(list, view);
                device.SetRenderTarget(null);
                var pixels = new Color[Size * Size];
                target.GetData(pixels);
                return pixels;
            }

            var bareOnDark = Draw(dark, 0);
            var bareOnLight = Draw(light, 0);
            var coated = Draw(dark, 255);
            var half = Draw(dark, 128);
            int solid = 0, partial = 0, misses = 0, halfMisses = 0;
            var misplaced = new List<string>();
            for (int i = 0; i < bareOnDark.Length; i++)
            {
                bool clear = bareOnDark[i] == dark && bareOnLight[i] == light;
                bool opaque = bareOnDark[i] == bareOnLight[i]; // the background doesn't show through
                if (clear)
                {
                    if (coated[i] != dark || half[i] != dark) misplaced.Add($"({i % Size},{i / Size})");
                    continue;
                }
                if (!opaque) { partial++; continue; }
                solid++;
                if (!Near(coated[i], colour.R, colour.G, colour.B, 1)) misses++;
                var sprite = bareOnDark[i];
                if (!Near(half[i], (colour.R * 128 + sprite.R * 127) / 255, (colour.G * 128 + sprite.G * 127) / 255, (colour.B * 128 + sprite.B * 127) / 255, 3)) halfMisses++;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"{modeName} at {zoom}: {solid} solid pixels, {partial} partly see-through; misplaced {misplaced.Count}, unfilled {misses}, half-strength off {halfMisses}");
            Assert.True(solid > 50, "The crawler was drawn.");
            Assert.True(misplaced.Count == 0, "The coat touched pixels its crawler doesn't: " + string.Join(" ", misplaced.Take(12)));
            Assert.Equal(0, misses);
            Assert.Equal(0, halfMisses);
        }

        /// <summary>
        /// The game's own picture, close up: at t300 a crawler mining glowstone in the Glow Caves is hurt by the falling rock,
        /// and wears its coat at the blink's strongest step (the view's clock follows the ticks in tests: 30 s).
        /// </summary>
        [Fact]
        public void AHurtCrawlerLooksAsRecorded()
        {
            const int Width = 800, Height = 600;
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.Isometric, TicksPerUpdate = 100 }, frames: 3, width: Width, height: Height);
            var frame = game.Session.Frames.Current;
            int hurt = -1;
            for (int i = 0; i < frame.Count && hurt < 0; i++) if ((frame[i].Flags & CrawlerFlags.Hurt) != 0) hurt = i;
            Assert.True(hurt >= 0, "At t300 a crawler is hurt.");
            Assert.Equal(HurtCoat.Peak, HurtCoat.Strength(frame.Tick / 10f));
            var at = frame[hurt].Position;
            game.World.Rig.ZoomAt(at, 0.3f);
            game.World.Rig.Pan(at - game.World.Rig.Position);
            RenderTests.CompareWithReference("render-iso-hurt-t300-zoomed", FrameCapture.CaptureMap(game, Width, Height), Width, Height);
        }

        private static void Stand(DrawList list, in ArtPiece piece, Rgba32 tint, bool silhouette)
        {
            ref var item = ref list.Add(0, DrawRole.Crawler, 0f, piece.Sprite, 0f, 0f, piece.Width, piece.Height, tint, 0f, piece.Sheet);
            item.PivotX = piece.PivotX;
            item.PivotY = piece.PivotY;
            item.SnapToPixel = true;
            item.Silhouette = silhouette;
        }

        private static bool Near(Color c, int r, int g, int b, int tolerance)
            => Math.Abs(c.R - r) <= tolerance && Math.Abs(c.G - g) <= tolerance && Math.Abs(c.B - b) <= tolerance;
    }
}
