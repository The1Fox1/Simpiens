using System;
using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The sampling rules (graphics V3.1, epic F12 and F19), checked on the CPU against the real outpost scene through
    /// the rig's real zoom steps: where each screen pixel's centre lands in its sprite's texels, as point sampling picks
    /// them. A centre on a texel's edge is a tie, which float rounding (down to the atlas's width) settles.
    /// </summary>
    public class SamplingTests
    {
        private readonly ITestOutputHelper _output;

        public SamplingTests(ITestOutputHelper output) => _output = output;

        public static TheoryData<string> Modes => new TheoryData<string> { "top-down", "isometric" };

        /// <summary>Every view the wheel reaches from the fit, 40 notches each way, and whole zooms 1 to 4.</summary>
        internal static List<(string Name, View View, int Whole)> Views(ViewMode mode, Simpiens.Simulation.World.MapDefinition map)
        {
            var views = new List<(string, View, int)>();
            const int width = 1280, height = 720;
            foreach (float step in new[] { CameraInput.ZoomStep, 1f / CameraInput.ZoomStep })
            {
                var rig = new CameraRig { Projection = mode.Projection };
                rig.Configure(map.CameraFrame, width / (float)height, height);
                for (int notch = 0; notch <= 40; notch++)
                {
                    if (notch > 0) rig.ZoomAt(rig.Position, step);
                    views.Add(($"notch {(step < 1f ? notch : -notch)}", rig.ToView(width, height), 0));
                }
            }
            for (int k = 1; k <= 4; k++)
            {
                var view = new View(mode.Projection, new ViewCamera(map.CameraFrame.Center, k * mode.Projection.ArtPixelsPerUnit, width, height)).SnappedToPixels();
                views.Add(($"whole {k}x", view, k));
            }
            return views;
        }

        /// <summary>
        /// For each axis of each unrotated, point-sampled item in <paramref name="scene"/>, the smallest distance (in texels)
        /// from a pixel centre to a texel edge, with the quad shifted by <paramref name="bias"/>; and how many centres lie
        /// within <paramref name="near"/> texels of an edge.
        /// </summary>
        internal static (double Closest, int Near) Margins(WorldScene scene, in View view, float bias, double near)
        {
            double closest = double.MaxValue;
            int count = 0;
            var items = scene.Items;
            for (int i = 0; i < items.Count; i++)
            {
                ref readonly var item = ref items[i];
                if (item.OnFloor || item.Rotation != 0f || item.Sheet == DrawSheet.Fog) continue;
                var quad = ScreenQuad.Of(item, view);
                if (!quad.Touches(view.ViewportWidth, view.ViewportHeight)) continue;
                var sprite = SpriteSheets.Contents(item.Sheet)[item.SpriteIndex];
                Axis(quad.Left + bias, quad.Width, sprite.Width, view.ViewportWidth, near, ref closest, ref count);
                Axis(quad.Top + bias, quad.Height, sprite.Height, view.ViewportHeight, near, ref closest, ref count);
            }
            return (closest, count);
        }

        private static void Axis(float start, float size, int texels, int limit, double near, ref double closest, ref int count)
        {
            if (size <= 0f) return;
            int first = Math.Max(0, (int)Math.Floor(start)), last = Math.Min(limit - 1, (int)Math.Ceiling(start + size));
            for (int x = first; x <= last; x++)
            {
                double centre = x + 0.5;
                if (centre < start || centre >= start + size) continue;
                double t = (centre - start) * texels / size;
                if (t < 0.5 || t > texels - 0.5) continue; // the quad's own edges are transparent gutter either way
                double d = Math.Abs(t - Math.Round(t));
                if (d < closest) closest = d;
                if (d < near) count++;
            }
        }

        /// <summary>
        /// With the bias, at whole zooms, at the rig's framing and at its closest zoom (where 1,230 and 2,198 centres sat
        /// exactly on edges without it), every pixel centre is clear of every texel edge by far more than a GPU's float
        /// error (about 2e-5 texel): the picture there can't depend on the atlas. In-between zooms keep a few generic
        /// near-ties (<see cref="MeasureTies"/>, epic F12).
        /// </summary>
        [Theory]
        [MemberData(nameof(Modes))]
        public void WholeZoomsTheFramingAndTheClosestZoomKeepCentresClearOfTexelEdges(string mode)
        {
            var viewMode = mode == "top-down" ? ViewMode.TopDown : ViewMode.Isometric;
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var frames = TestFrames.Of(sim);
            var map = sim.Host.Current.Map;
            var scene = new WorldScene { Mode = viewMode };
            scene.Advance(map, frames, 0f);
            var views = Views(viewMode, map);
            var checkedViews = views.Where(v => v.Whole > 0 || v.Name == "notch 0").ToList();
            checkedViews.Add(views.Where(v => v.Name.StartsWith("notch ", StringComparison.Ordinal)).OrderByDescending(v => v.View.Zoom).First());
            foreach (var (name, view, _) in checkedViews)
            {
                scene.Present(map, frames, 0f, null, view);
                var margins = Margins(scene, view, WorldRenderer.SampleBias, 1e-4);
                Assert.True(margins.Closest >= 2e-3, $"{mode} {name} ({view.Zoom}x): a pixel centre {margins.Closest:E1} texel from an edge.");
            }
        }

        /// <summary>A standing piece snapped to whole pixels keeps one shape as it moves by fractions of a pixel, at an in-between zoom (epic F19).</summary>
        [Theory]
        [MemberData(nameof(Modes))]
        public void AStandingPieceKeepsItsShapeAsItMoves(string mode)
        {
            var viewMode = mode == "top-down" ? ViewMode.TopDown : ViewMode.Isometric;
            Assert.Equal(1, Shapes(viewMode, 1.37f, snap: true));
            Assert.True(Shapes(viewMode, 1.37f, snap: false) > 1, "without the snap the crawler's shape changes as it moves (the probe's 10 and 14)");
        }

        /// <summary>How many silhouettes a walking crawler takes over 16 sub-pixel steps, drawn as the renderer draws it.</summary>
        internal static int Shapes(ViewMode mode, float zoom, bool snap)
        {
            var catalog = mode.Catalog;
            var art = catalog.Id(ArtKeys.Crawler(Simpiens.Entities.AgentVisualState.Walking));
            var piece = catalog.Piece(art, 0, 1f, 1f);
            var sprite = SpriteSheets.Contents(piece.Sheet)[piece.Sprite];
            var view = new View(mode.Projection, new ViewCamera(Vec2.Zero, zoom * mode.Projection.ArtPixelsPerUnit, 640, 480)).SnappedToPixels();
            var shapes = new HashSet<string>();
            for (int step = 0; step < 16; step++)
            {
                float offset = step / 16f / view.Camera.PixelsPerUnit; // a sixteenth of a pixel along the floor's x
                var item = new DrawItem { Sheet = piece.Sheet, SpriteIndex = piece.Sprite, X = 0.3f + offset, Y = 0.2f, Width = piece.Width, Height = piece.Height,
                    PivotX = piece.PivotX, PivotY = piece.PivotY, SnapToPixel = snap };
                var quad = ScreenQuad.Of(item, view);
                shapes.Add(Silhouette(sprite, quad.Left + WorldRenderer.SampleBias, quad.Top + WorldRenderer.SampleBias, quad.Width, quad.Height));
            }
            return shapes.Count;
        }

        /// <summary>The opaque screen pixels a point-sampled quad covers, relative to its own corner (a pure shift is the same shape).</summary>
        internal static string Silhouette(PixelSprite sprite, float left, float top, float width, float height)
        {
            var lit = new List<(int X, int Y)>();
            for (int y = (int)MathF.Floor(top); y <= (int)MathF.Ceiling(top + height); y++)
                for (int x = (int)MathF.Floor(left); x <= (int)MathF.Ceiling(left + width); x++)
                {
                    float u = (x + 0.5f - left) / width, v = (y + 0.5f - top) / height;
                    if (u < 0f || u >= 1f || v < 0f || v >= 1f) continue;
                    if (sprite.Pixels[(int)(v * sprite.Height) * sprite.Width + (int)(u * sprite.Width)].A > 0) lit.Add((x, y));
                }
            int minX = lit.Min(p => p.X), minY = lit.Min(p => p.Y);
            return string.Join(";", lit.Select(p => $"{p.X - minX},{p.Y - minY}"));
        }

        [Theory(Explicit = true)]
        [MemberData(nameof(Modes))]
        public void MeasureTies(string mode)
        {
            var viewMode = mode == "top-down" ? ViewMode.TopDown : ViewMode.Isometric;
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var frames = TestFrames.Of(sim);
            var map = sim.Host.Current.Map;
            var scene = new WorldScene { Mode = viewMode };
            scene.Advance(map, frames, 0f);
            foreach (var (name, view, _) in Views(viewMode, map))
            {
                scene.Present(map, frames, 0f, null, view);
                var unbiased = Margins(scene, view, 0f, 1e-4);
                var biased = Margins(scene, view, WorldRenderer.SampleBias, 1e-4);
                _output.WriteLine($"{mode} {name} ({view.Zoom:0.###}x): without the bias {unbiased.Near} centres within 1e-4 texel (closest {unbiased.Closest:E1}); with it {biased.Near} (closest {biased.Closest:E1})");
            }
        }
    }
}
