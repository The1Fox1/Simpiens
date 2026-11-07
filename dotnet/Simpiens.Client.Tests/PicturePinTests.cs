using System.Linq;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Pins what the map draws as data, finer than the render references' tolerance (Epic CLIENT, plan step 1.1): every
    /// draw item in draw order, as the screen sees it (sprite by name, layer, centre, size, turn, tint), and every placed
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
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.TopDown }, frames: 0);
            game.Session.Simulation.Clock.SetTimeMultiplier(0f);
            game.RunOneFrame();
            AssertPinned(game, PinnedTick0);
        }

        [Fact]
        public void TheOutpostAtTick300DrawsAsPinned()
        {
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.TopDown, TicksPerUpdate = 100 }, frames: 3);
            AssertPinned(game, PinnedTick300);
        }

        [Fact]
        public void ASelectedCrawlerInAZoomedViewDrawsAsPinned()
        {
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.TopDown, TicksPerUpdate = 100 }, frames: 3);
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == Simpiens.Simulation.Spatial.EntityType.Pawn);
            game.Session.Selection.SelectCrawler(crawler.Id, follow: false);
            var hub = game.Session.Simulation.Host.Current.Map.Labels.First(l => l.Text == "Outpost Hub").Position;
            game.World.Rig.ZoomAt(hub, 0.4f);
            game.World.Rig.Pan(new Vec2(2f, -3f));
            AssertPinned(game, PinnedSelectedZoomed);
        }

        [Fact]
        public void TheIsometricOutpostAtTick300DrawsAsPinned()
        {
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.Isometric, TicksPerUpdate = 100 }, frames: 3);
            AssertPinned(game, PinnedIsometricTick300);
        }

        [Fact]
        public void ASelectedCrawlerInAZoomedIsometricViewDrawsAsPinned()
        {
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.Isometric, TicksPerUpdate = 100 }, frames: 3);
            var crawler = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == Simpiens.Simulation.Spatial.EntityType.Pawn);
            game.Session.Selection.SelectCrawler(crawler.Id, follow: false);
            var hub = game.Session.Simulation.Host.Current.Map.Labels.First(l => l.Text == "Outpost Hub").Position;
            game.World.Rig.ZoomAt(hub, 0.4f);
            game.World.Rig.Pan(new Vec2(2f, -3f));
            AssertPinned(game, PinnedIsometricSelectedZoomed);
        }

        private static void AssertPinned(SimpiensGame game, (ulong items, ulong labels) pinned)
        {
            FrameCapture.CaptureMap(game, Width, Height); // draws the scene as the render references do
            var actual = (ItemsHash(game), LabelsHash(game));
            Assert.True(actual == pinned, $"The drawn picture changed: items {actual.Item1}UL, labels {actual.Item2}UL (pinned {pinned.items}UL, {pinned.labels}UL).");
        }

        /// <summary>
        /// Each item that lands on screen (<see cref="ScreenQuad.Touches"/>), in draw order, in screen terms, quantized to
        /// 1/64 pixel so equal pictures hash equal. What is culled isn't part of the picture.
        /// </summary>
        private static ulong ItemsHash(SimpiensGame game)
        {
            var camera = game.World.View;
            var items = game.World.Scene.Items;
            int shown = 0;
            for (int i = 0; i < items.Count; i++) if (ScreenQuad.Of(items[i], camera).Touches(Width, Height)) shown++;
            ulong hash = Fnv.Start;
            hash = Fnv.Add(hash, shown);
            for (int i = 0; i < items.Count; i++)
            {
                ref readonly var item = ref items[i];
                var quad = ScreenQuad.Of(item, camera);
                if (!quad.Touches(Width, Height)) continue;
                var centre = quad.Anchor; // every item is centred on its anchor today
                // The sprite by name, not by sheet and index (graphics V0): indices are the sheets' business
                hash = Fnv.Add(hash, SpriteSheets.Contents(item.Sheet)[item.SpriteIndex].Name);
                hash = Fnv.Add(hash, DrawSortKey.Layer(item.SortKey));
                hash = Fnv.Add(hash, Fnv.Quantize(centre.X));
                hash = Fnv.Add(hash, Fnv.Quantize(centre.Y));
                hash = Fnv.Add(hash, Fnv.Quantize(quad.Width));
                hash = Fnv.Add(hash, Fnv.Quantize(quad.Height));
                hash = Fnv.Add(hash, Fnv.Quantize(item.Rotation * 64f));
                hash = Fnv.Add(hash, (long)item.Tint.R << 24 | (long)item.Tint.G << 16 | (long)item.Tint.B << 8 | item.Tint.A);
            }
            return hash;
        }

        private static ulong LabelsHash(SimpiensGame game)
        {
            var placed = game.World.LabelLayout.Placed;
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

            public static ulong Add(ulong hash, string text)
            {
                hash = Add(hash, text.Length);
                foreach (char c in text) hash = Add(hash, c);
                return hash;
            }

            public static long Quantize(float value) => (long)System.Math.Round(value * 64.0);
        }

        // Recorded 2026-10-20 at main-kni 66c805f, before the projection seam (plan step 1.1). Items re-pinned at plan step
        // 2.2: walls take the projection's depth (isometric needs it), which reorders top-down's wall tiles among themselves;
        // they never overlap, so the picture is the same (render references unchanged, and with wall depth 0 the 1.1 pins
        // still matched). Items re-pinned again at plan step 2.3 (producer decision D1 = C, 2026-10-21): the fog hides what an
        // uncharted sector holds, so the props under the clouds are no longer drawn (the references were re-recorded with
        // them). The labels' pins are 1.1's. M3 step 0.4: the pins count only what lands on screen, so culling (which drops
        // the rest before sorting) can't move them; the full views have nothing off screen and kept their pins, and the
        // zoomed view's items were re-pinned for the items it no longer counts. Epic 10 G0 (2026-10-23): the crawlers move
        // differently (navigation fixes for finding F2), so every pin after tick 0 was re-pinned with the render references;
        // the selected crawler's name moves with it, hence the zoomed views' labels. Re-pinned again for Epic 10 G1 (walkers
        // keep right around a crawler in the way; gathering prefers what the stall still buys). Re-pinned with the labels for
        // Epic 10 G2: the Sump and the Glow Caves replace the rock, with their names, their dangers under them ("FOUL AIR")
        // and tinted floors. Graphics V0 (2026-10-28): items hash their sprite's name instead of its sheet and index, so
        // the sheets can renumber; every item pin was re-pinned with no picture change (the renders exact in the pinned image,
        // the labels' pins unchanged). Graphics V3.1 (2026-10-29): standing pieces snap their anchors to whole pixels, so every
        // pin with a crawler between pixels moved (tick 0's didn't: nothing is between pixels yet); labels unchanged.
        // Graphics V5.3: the stall stands in the hub with its goods, and carriers show their bundles; every item pin re-pinned.
        // Epic 10 G5.2 (2026-10-31): crawlers move differently (F5's navigation fix), and Floor 1 gains the Drowned Gallery
        // below the Sump, with its name and "FLOODED", which moves the frame's centre down 3 units; every pin re-pinned with
        // the labels, with the render references.
        // Epic UI-Beta B2 (2026-11-03): every crawler is drawn as its own body, a cell of the world sheet's body region, so
        // every item pin was re-pinned with the render references after the producer's look; labels unchanged.
        // Epic 10 Part 2 W0.4 (2026-11-04): the workshop row and the guildhall replace the rock beside the Drowned Gallery,
        // with their floors, walls and names; the whole-floor pins moved, items and labels, and the zoomed views' labels
        // (every placed label is hashed), while their items held: nothing a crawler does changed.
        // Epic 10 Part 2 T5 (2026-11-05), the 24-hour day: 12 bunks in two rows of six, and every crawler spawns and moves
        // on the day's pace, so every item pin was re-pinned with the render references; the whole floor's labels held, the
        // zoomed views' moved with the selected crawler.
        // Epic 10 Part 2 W1.6a.1 (2026-11-05): the stations' plots lie in the workshop row, at the floor's lower edge; the
        // whole-floor views' items re-pinned with the render references, labels unchanged, the zoomed views unchanged.
        // Epic 10 Part 2 W5.2 (2026-11-06): the guildhall's eight barracks bunks, and with eight more entities on the map every
        // later id moves, so every crawler's look and day moved too; every item pin re-pinned with the render references, the
        // whole floor's labels held, the zoomed views' moved with the selected crawler.
        // Epic 10 Part 2's stretch (2026-11-06, D69): the hazards' animations in the whole floor's views; items re-pinned,
        // labels and the zoomed views unchanged (no hazard sector in them).
        private static readonly (ulong, ulong) PinnedTick0 = (12937878628055227934UL, 14344167986805025999UL);
        private static readonly (ulong, ulong) PinnedTick300 = (6032210246337508215UL, 14344167986805025999UL);
        // Epic UI-Beta B1: the selected crawler's label shows its given name; labels re-pinned, items unchanged.
        private static readonly (ulong, ulong) PinnedSelectedZoomed = (16238647467431113274UL, 4268601865235128987UL);
        // Isometric's (M3 step 2.3), pinned with its render references after a look at them.
        private static readonly (ulong, ulong) PinnedIsometricTick300 = (18273753880055733174UL, 7638092815957420050UL);
        private static readonly (ulong, ulong) PinnedIsometricSelectedZoomed = (14081764154863997278UL, 13801314577213324051UL);
    }
}
