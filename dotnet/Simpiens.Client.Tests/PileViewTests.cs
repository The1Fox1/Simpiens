using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Loot;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Piles on the map (Epic 10 Part 3, K4.5a): the frames carry each pile's top item, its rarest stack; the scene draws it
    /// as that item's picture on the stall's display, where it lies, in both view modes, and hides it under an uncharted
    /// sector's fog like any prop.
    /// </summary>
    public class PileViewTests
    {
        private static readonly Vec2 InTheHub = new Vec2(1f, 1f);
        private static readonly Vec2 InTheWarren = new Vec2(21f, 16f); // the Overgrowth opens uncharted

        [Theory]
        [InlineData("top-down")]
        [InlineData("isometric")]
        public void APileIsDrawnAsItsRarestItemAndTheFogHidesIt(string mode)
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var world = (IPileWorld)sim.Simulation;
            uint tick = (uint)sim.Clock.CurrentTick;
            var seen = sim.Simulation.Piles.Drop(world, InTheHub, new[] { new ItemInstance(1, ItemType.Hide, 2, 0), Outfitting.NewPiece(ItemType.FangKnife, Grades.Standard), new ItemInstance(1, ItemType.Chitin, 1, 0) }, 0, default, tick);
            var hidden = sim.Simulation.Piles.Drop(world, InTheWarren, new[] { new ItemInstance(1, ItemType.Hide, 1, 0) }, 0, default, tick);

            var frames = TestFrames.Of(sim);
            Assert.True(frames.Current.TryGetIndex(seen.Id, out int index));
            Assert.Equal(ItemType.FangKnife, frames.Current[index].PileItem); // the epic knife over the common hide and the uncommon chitin
            Assert.True(frames.Current.TryGetIndex(hidden.Id, out index));
            Assert.Equal(ItemType.Hide, frames.Current[index].PileItem);

            var viewMode = mode == "top-down" ? ViewMode.TopDown : ViewMode.Isometric;
            var scene = new WorldScene { Mode = viewMode };
            scene.Build(sim.Host.Current.Map, frames, 0f);
            var knife = scene.Catalog.Piece(scene.Catalog.Picture(ItemCatalog.Get(ItemType.FangKnife).ArtKey, scene.Catalog.DisplayPicture), 0f);
            var hide = scene.Catalog.Piece(scene.Catalog.Picture(ItemCatalog.Get(ItemType.Hide).ArtKey, scene.Catalog.DisplayPicture), 0f);
            var items = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).ToList();
            Assert.Contains(items, d => d.Sheet == DrawSheet.World && d.SpriteIndex == knife.Sprite && d.X == InTheHub.X && d.Y == InTheHub.Y && d.Tint.A == 255);
            Assert.DoesNotContain(items, d => d.Sheet == DrawSheet.World && d.SpriteIndex == hide.Sprite && d.X == InTheWarren.X && d.Y == InTheWarren.Y);

            sim.Sectors.SetExplored(Simpiens.Core.Session.Layouts.Floor1Layout.OvergrowthSectorId, true);
            frames = TestFrames.Of(sim);
            scene = new WorldScene { Mode = viewMode };
            scene.Build(sim.Host.Current.Map, frames, 0f);
            items = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).ToList();
            Assert.Contains(items, d => d.Sheet == DrawSheet.World && d.SpriteIndex == hide.Sprite && d.X == InTheWarren.X && d.Y == InTheWarren.Y && d.Tint.A == 255);
        }
    }
}
