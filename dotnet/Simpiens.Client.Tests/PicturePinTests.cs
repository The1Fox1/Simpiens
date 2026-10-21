using System.Linq;
using Simpiens.Presentation;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Pins what the map draws as data, finer than the render references' tolerance (Epic CLIENT, plan step 1.1): every
    /// draw item in draw order, as the screen sees it (sheet, sprite, layer, centre, size, turn, tint), and every placed
    /// label. Refactors of the projection and the draw list must keep these hashes; when a render reference moves while
    /// they hold, the change is float rounding, not a different picture. A deliberate change of the picture re-records
    /// them in the same commit as the references.
    /// </summary>
    public class PicturePinTests
    {
        private const int Width = 800, Height = 600;

        [Fact]
        public void TheOutpostAtTickZeroDrawsAsPinned()
        {
            var game = Offscreen.Start(frames: 0);
            game.Simulation.Clock.SetTimeMultiplier(0f);
            game.RunOneFrame();
            AssertPinned(game, PinnedTick0);
        }

        [Fact]
        public void TheOutpostAtTick300DrawsAsPinned()
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 100 }, frames: 3);
            AssertPinned(game, PinnedTick300);
        }

        [Fact]
        public void ASelectedCrawlerInAZoomedViewDrawsAsPinned()
        {
            var game = Offscreen.Start(new ClientOptions { TicksPerUpdate = 100 }, frames: 3);
            var crawler = game.Simulation.Registry.ActiveEntities.First(e => e.Type == Simpiens.Simulation.Spatial.EntityType.Pawn);
            game.Selection.SelectCrawler(crawler.Id, follow: false);
            var hub = game.Simulation.Host.Current.Map.Labels.First(l => l.Text == "Outpost Hub").Position;
            game.Rig.ZoomAt(hub, 0.4f);
            game.Rig.Pan(new Vec2(2f, -3f));
            AssertPinned(game, PinnedSelectedZoomed);
        }

        private static void AssertPinned(SimpiensGame game, (ulong items, ulong labels) pinned)
        {
            FrameCapture.CaptureMap(game, Width, Height); // draws the scene as the render references do
            var actual = (ItemsHash(game), LabelsHash(game));
            Assert.True(actual == pinned, $"The drawn picture changed: items {actual.Item1}UL, labels {actual.Item2}UL (pinned {pinned.items}UL, {pinned.labels}UL).");
        }

        /// <summary>Each item in draw order, in screen terms, quantized to 1/64 pixel so equal pictures hash equal.</summary>
        private static ulong ItemsHash(SimpiensGame game)
        {
            var camera = game.View;
            var items = game.Scene.Items;
            ulong hash = Fnv.Start;
            hash = Fnv.Add(hash, items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                ref readonly var item = ref items[i];
                var centre = camera.WorldToScreen(new Vec2(item.X, item.Y));
                hash = Fnv.Add(hash, (long)item.Sheet);
                hash = Fnv.Add(hash, item.SpriteIndex);
                hash = Fnv.Add(hash, DrawSortKey.Layer(item.SortKey));
                hash = Fnv.Add(hash, Fnv.Quantize(centre.X));
                hash = Fnv.Add(hash, Fnv.Quantize(centre.Y));
                hash = Fnv.Add(hash, Fnv.Quantize(item.Width * camera.PixelsPerUnit));
                hash = Fnv.Add(hash, Fnv.Quantize(item.Height * camera.PixelsPerUnit));
                hash = Fnv.Add(hash, Fnv.Quantize(item.Rotation * 64f));
                hash = Fnv.Add(hash, (long)item.Tint.R << 24 | (long)item.Tint.G << 16 | (long)item.Tint.B << 8 | item.Tint.A);
            }
            return hash;
        }

        private static ulong LabelsHash(SimpiensGame game)
        {
            var placed = game.LabelLayout.Placed;
            ulong hash = Fnv.Start;
            hash = Fnv.Add(hash, placed.Count);
            foreach (var label in placed)
            {
                hash = Fnv.Add(hash, label.Index);
                hash = Fnv.Add(hash, label.Shown ? 1 : 0);
                hash = Fnv.Add(hash, label.Face.Size);
                hash = Fnv.Add(hash, label.Face.Bold ? 1 : 0);
                hash = Fnv.Add(hash, label.X);
                hash = Fnv.Add(hash, label.Y);
                hash = Fnv.Add(hash, label.Width);
                hash = Fnv.Add(hash, label.Height);
            }
            return hash;
        }

        private static class Fnv
        {
            public const ulong Start = 14695981039346656037UL;

            public static ulong Add(ulong hash, long value)
            {
                for (int b = 0; b < 8; b++) hash = (hash ^ (byte)(value >> (8 * b))) * 1099511628211UL;
                return hash;
            }

            public static long Quantize(float value) => (long)System.Math.Round(value * 64.0);
        }

        // Recorded 2026-10-20 at main-kni 66c805f, before the projection seam (plan step 1.1)
        private static readonly (ulong, ulong) PinnedTick0 = (4409963616687968739UL, 6705829926719516470UL);
        private static readonly (ulong, ulong) PinnedTick300 = (12971714843452477590UL, 6705829926719516470UL);
        private static readonly (ulong, ulong) PinnedSelectedZoomed = (15458948144476328945UL, 17765688760816253013UL);
    }
}
