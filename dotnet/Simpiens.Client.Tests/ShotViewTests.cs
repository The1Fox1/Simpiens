using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Shots on the map (Epic 10 Part 3, K8.4a), in both view modes: the frames carry the tick's shots from the creatures' pass,
    /// and the scene draws each flying from its shooter to its target for <see cref="WorldScene.ShotSeconds"/> of the view's
    /// time, then lets it go. Presentation only: nothing here touches the simulation's state.
    /// </summary>
    public class ShotViewTests
    {
        [Theory]
        [InlineData("top-down")]
        [InlineData("isometric")]
        public void AShotFliesFromItsShooterToItsTarget(string mode)
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(20);
            using var frames = new WorldViewFrames();
            frames.Follow(sim.Host, sim.Registry, sim.Sectors, sim.Clock, sim.Chronicle, sim.Simulation);
            var archer = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            Assert.True(archer.Inventory.TryEquip(Outfitting.NewPiece(ItemType.Crossbow, Grades.Standard)));
            var node = sim.Registry.GetEntity(archer.AgentId);
            var creature = sim.Simulation.Creatures.Creatures.First(c => c.IsAlive);

            // By hand: the two in the charted hub, 3 units apart; the archer's shot asked for, the tick run
            creature.Position = new Vec2(2f, 2f);
            archer.Position = new Vec2(-1f, 2f);
            archer.NextSwingAt = 0;
            sim.Simulation.Creatures.AskSwing(node, creature.Id);
            sim.Step(1);
            Assert.Equal(1, frames.Current.ShotCount);
            var shot = frames.Current.Shot(0);
            Assert.Equal(archer.AgentId, shot.Shooter);
            Assert.False(shot.ByCreature);

            var scene = new WorldScene { Mode = mode == "top-down" ? ViewMode.TopDown : ViewMode.Isometric };
            var map = sim.Host.Current.Map;
            int sprite = scene.Catalog.Piece(scene.Catalog.Id(ArtKeys.FxShot), 0f).Sprite;
            DrawItem[] Shots(float seconds)
            {
                scene.Advance(map, frames, seconds);
                scene.Present(map, frames, seconds);
                return Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).Where(d => d.Sheet == DrawSheet.World && d.SpriteIndex == sprite).ToArray();
            }
            var start = Assert.Single(Shots(10f));
            Assert.Equal(shot.From.X, start.X, 3);
            Assert.Equal(shot.From.Y, start.Y, 3);
            var half = Assert.Single(Shots(10f + WorldScene.ShotSeconds * 0.5f));
            Assert.Equal((shot.From.X + shot.To.X) * 0.5f, half.X, 3);
            Assert.Equal((shot.From.Y + shot.To.Y) * 0.5f, half.Y, 3);
            Assert.Empty(Shots(10f + WorldScene.ShotSeconds + 0.01f)); // landed: gone
            Assert.Equal(0, scene.ShotsInFlight);
        }
    }
}
