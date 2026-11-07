using System;
using System.IO;
using System.Linq;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Presentation.Art.People;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>
    /// The live outpost drawn with bodies (Epic UI-Beta B2): the map at tick 300 in both view modes, framed and zoomed on
    /// a crawler, written to render-output/bodies-*.png (the producer's look passed on these, 2026-11-02). Every crawler
    /// is drawn from the body region, none as today's figure, and every body the frame shows is baked.
    /// </summary>
    public class BodyMapReviewTests
    {
        private const int Width = 800, Height = 600;

        [Theory]
        [InlineData("top-down")]
        [InlineData("isometric")]
        public void WriteTheMapWithBodies(string mode)
        {
            var game = Offscreen.Start(new ClientOptions { View = mode == "top-down" ? ViewMode.TopDown : ViewMode.Isometric, TicksPerUpdate = 100 }, frames: 3);
            var scene = game.World.Scene;
            Assert.Same(game.Graphics.Bodies, scene.Bodies);
            Assert.Equal(0, game.Graphics.Bodies.Overflow);
            Assert.Equal(0, game.Graphics.Bodies.PendingCount);
            Assert.True(game.Graphics.Bodies.InUse >= game.Session.Simulation.Host.CrawlerStates.Count, "A crawler has no body.");

            string output = Path.Combine(AppContext.BaseDirectory, "render-output");
            Directory.CreateDirectory(output);
            Png.Write(Path.Combine(output, $"bodies-{mode}-t300.png"), Width, Height, FrameCapture.CaptureMap(game, Width, Height));
            AssertEveryCrawlerIsABody(scene);

            // Zoomed on a crawler: where they are at t300 moves with behaviour (the hub was empty once the map grew, W5.2)
            var crawler = game.Session.Simulation.Host.CrawlerStates.First(s => !s.IsDrifter).Position;
            game.World.Rig.ZoomAt(crawler, 0.3f);
            Png.Write(Path.Combine(output, $"bodies-{mode}-t300-zoomed.png"), Width, Height, FrameCapture.CaptureMap(game, Width, Height));
            AssertEveryCrawlerIsABody(scene);
        }

        // No crawler is drawn as today's figure: each is a cell of the body region
        private static void AssertEveryCrawlerIsABody(WorldScene scene)
        {
            int bodies = 0;
            for (int i = 0; i < scene.Items.Count; i++)
            {
                ref readonly var item = ref scene.Items[i];
                if (item.Sheet != DrawSheet.World) continue;
                var sprite = SpriteSheets.World[item.SpriteIndex];
                Assert.False(sprite.Name.StartsWith("Agent_", StringComparison.Ordinal), $"A crawler is drawn as today's {sprite.Name}.");
                if (BodySlots.IsSlot(sprite)) bodies++;
            }
            Assert.True(bodies > 0, "No body is drawn.");
        }
    }
}
