using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Loot;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The fallen and the dead on the map (Epic 10 Part 3, K5.8a–b), in both view modes, set by hand: a collapsed crawler lies
    /// on its side where it fell; a carried one lies across its rescuer's back, raised, with no shadow of its own; a body is a
    /// cairn.
    /// </summary>
    public class FallenViewTests
    {
        [Theory]
        [InlineData("top-down")]
        [InlineData("isometric")]
        public void TheFallenLieTheCarriedRideAndTheDeadAreCairns(string mode)
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(20);
            var residents = sim.Host.CrawlerStates.Where(s => !s.IsDrifter).Take(3).ToList();
            var collapsed = residents[0];
            collapsed.IsDowned = true;
            collapsed.BleedsOutAt = (uint)sim.Clock.CurrentTick + 1000; // by hand: collapsed in danger
            var carried = residents[1];
            carried.IsDowned = true;
            carried.BleedsOutAt = (uint)sim.Clock.CurrentTick + 1000;
            carried.CarriedBy = residents[2].AgentId; // by hand: on its rescuer's back
            var body = sim.Simulation.Piles.Drop((IPileWorld)sim.Simulation, new Vec2(1f, 1f), System.ReadOnlySpan<ItemInstance>.Empty, 0, default, (uint)sim.Clock.CurrentTick, bodyOf: new EntityId(9u, 9u, 9u, 9u));

            var frames = TestFrames.Of(sim);
            Assert.True(frames.Current.TryGetIndex(collapsed.AgentId, out int i));
            Assert.True((frames.Current[i].Flags & CrawlerFlags.Collapsed) != 0);
            Assert.True(frames.Current.TryGetIndex(carried.AgentId, out i));
            Assert.True((frames.Current[i].Flags & CrawlerFlags.Carried) != 0);
            Assert.True(frames.Current.TryGetIndex(body.Id, out i) && frames.Current[i].IsBody);

            var scene = new WorldScene { Mode = mode == "top-down" ? ViewMode.TopDown : ViewMode.Isometric };
            scene.Build(sim.Host.Current.Map, frames, 0f);
            var items = Enumerable.Range(0, scene.Items.Count).Select(k => scene.Items[k]).ToList();
            var lying = items.Where(d => d.Pick >= 0 && scene.Items.PickedEntity(d.Pick) == collapsed.AgentId && !d.Silhouette).ToList();
            Assert.Contains(lying, d => System.MathF.Abs(d.Rotation - System.MathF.PI / 2f) < 0.001f && d.Z == 0f);
            var riding = items.Where(d => d.Pick >= 0 && scene.Items.PickedEntity(d.Pick) == carried.AgentId && !d.Silhouette).ToList();
            Assert.Contains(riding, d => System.MathF.Abs(d.Rotation - System.MathF.PI / 2f) < 0.001f
                && d.Z == scene.Catalog.RideHeight && d.X == carried.Position.X + scene.Catalog.RideOffset.X && d.Y == carried.Position.Y + scene.Catalog.RideOffset.Y
                && (d.Z > 0f || scene.Catalog.RideOffset != Vec2.Zero));
            var cairn = scene.Catalog.Piece(scene.Visuals.Cairn, 0f);
            Assert.Contains(items, d => d.Sheet == DrawSheet.World && d.SpriteIndex == cairn.Sprite && d.X == 1f && d.Y == 1f);
        }
    }
}
