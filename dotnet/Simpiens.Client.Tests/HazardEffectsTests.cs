using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Simpiens.Simulation.World;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The hazards' animations (Epic 10 Part 2, the stretch, D69): every hazard kind has its animation in both views; each
    /// hazard sector of Floor 1 gets spots clear of its walls and nodes; the scene draws them where the fog lets them show,
    /// ripples on the floor and puffs and pebbles standing; foul air and the flood play without pause, and a spot of falling
    /// rock drops its pebble and then waits. Presentation only: the simulation never sees them.
    /// </summary>
    public class HazardEffectsTests
    {
        public static TheoryData<string> Modes => new TheoryData<string> { "top-down", "isometric" };

        [Theory]
        [MemberData(nameof(Modes))]
        public void EveryHazardKindHasItsAnimation(string mode)
        {
            var catalog = mode == "top-down" ? ViewMode.TopDown.Catalog : ViewMode.Isometric.Catalog;
            foreach (HazardKind kind in System.Enum.GetValues(typeof(HazardKind)))
            {
                if (kind == HazardKind.None) continue;
                Assert.True(catalog.TryId(ArtKeys.Hazard(kind), out var art) && catalog[art].FrameCount > 1, $"{mode}: {kind} has an animation of several frames.");
            }
        }

        [Fact]
        public void EachHazardSectorGetsSpotsClearOfItsWallsAndNodes()
        {
            var map = Floor1Layout.Definition;
            var effects = new HazardEffects();
            effects.Load(map);
            foreach (var sector in map.Sectors.Where(s => s.Hazard.IsHazardous))
            {
                var spots = effects.Spots.Where(s => s.SectorId == sector.Id).ToList();
                Assert.True(spots.Count >= 3, $"{sector.Name} has {spots.Count} spots.");
                foreach (var spot in spots)
                {
                    Assert.Equal(sector.Hazard.Kind, spot.Kind);
                    Assert.Equal(sector.Id, map.FindSector(spot.Position).Id);
                    var b = sector.Bounds;
                    float wall = System.Math.Min(System.Math.Min(spot.Position.X - b.X, b.X + b.Width - spot.Position.X), System.Math.Min(spot.Position.Y - b.Y, b.Y + b.Height - spot.Position.Y));
                    Assert.True(wall >= HazardEffects.WallClearance - 0.5f, $"A spot in {sector.Name} at {spot.Position} is {wall:F1} from a wall.");
                    Assert.All(map.ResourceNodes, node => Assert.True((node.Position - spot.Position).Magnitude >= HazardEffects.NodeClearance));
                }
            }
            var again = new HazardEffects();
            again.Load(map);
            Assert.Equal(effects.Spots.ToArray(), again.Spots.ToArray()); // the same map, the same spots
        }

        [Fact]
        public void TheSceneDrawsThemWhereTheFogLetsThemShowAndTheyMove()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var scene = new WorldScene { Mode = ViewMode.TopDown };
            var frames = TestFrames.Of(sim);
            var catalog = scene.Catalog;
            int Drawn(HazardKind kind, float seconds)
            {
                scene.Build(sim.Host.Current.Map, frames, seconds);
                var names = HazardSprites.All.Select(s => s.Name).Where(n => kind switch
                {
                    HazardKind.FoulAir => n.StartsWith("Fx_FoulAir_"),
                    HazardKind.FallingRock => n.StartsWith("Fx_Rockfall_"),
                    _ => n.StartsWith("Fx_Ripple_"),
                }).Select(n => SpriteSheets.World.IndexOf(n)).ToHashSet();
                return Enumerable.Range(0, scene.Items.Count).Count(i => scene.Items[i].Sheet == DrawSheet.World && names.Contains(scene.Items[i].SpriteIndex));
            }

            int puffs = Drawn(HazardKind.FoulAir, 0f); // laid out by the first build
            Assert.Equal(scene.Hazards.Spots.Count(s => s.Kind == HazardKind.FoulAir), puffs); // the Sump is charted: every puff shows
            Assert.True(puffs > 0);
            Assert.Equal(scene.Hazards.Spots.Count(s => s.Kind == HazardKind.Flooding), Drawn(HazardKind.Flooding, 0f));
            int caves = scene.Hazards.Spots.Count(s => s.Kind == HazardKind.FallingRock);
            var rocks = Enumerable.Range(0, 40).Select(i => Drawn(HazardKind.FallingRock, i * 0.25f)).ToList();
            Assert.True(rocks.Max() > 0 && rocks.Min() < caves, $"Pebbles fall and then wait ({string.Join(",", rocks)} of {caves} spots).");

            // A puff's frame moves on with time
            scene.Build(sim.Host.Current.Map, frames, 0f);
            var first = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i].SpriteIndex).ToArray();
            scene.Build(sim.Host.Current.Map, frames, 0.6f);
            var later = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i].SpriteIndex).ToArray();
            Assert.NotEqual(first, later);
        }
    }
}
