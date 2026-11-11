using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The creatures on screen (Epic 10 Part 3, K2.5, drawn ahead of K2.3 at the producer's word): the frames carry each as
    /// its kind with what it is doing; the scene hides them under an uncharted sector's fog and, charted, draws each as its
    /// kind where it stands, scuttling while it moves and still at its place, mirrored when it last went left on screen
    /// (which way that is, the projection says). Renders show the warren in isometric and the hollow in top-down.
    /// </summary>
    public class CreatureViewTests
    {
        private const int Width = 800, Height = 600;

        private static LocalSimulation Outpost()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            return sim;
        }

        private static readonly HashSet<int> CreatureSpriteIndices = CreatureSprites.All.Select(s => SpriteSheets.World.IndexOf(s.Name)).ToHashSet();

        private static List<DrawItem> CreaturesDrawn(WorldScene scene)
        {
            var drawn = new List<DrawItem>();
            for (int i = 0; i < scene.Items.Count; i++)
            {
                var item = scene.Items[i];
                if (item.Sheet == DrawSheet.World && CreatureSpriteIndices.Contains(item.SpriteIndex)) drawn.Add(item);
            }
            return drawn;
        }

        private static void ChartTheLairs(LocalSimulation sim)
        {
            sim.Sectors.SetExplored(Floor1Layout.OvergrowthSectorId, true);
            sim.Sectors.SetExplored(Floor1Layout.DeepSeamSectorId, true);
        }

        [Fact]
        public void TheFramesCarryEveryCreatureAsItsKind()
        {
            using var sim = Outpost();
            sim.Step(200);
            var frame = TestFrames.Of(sim).Current;
            var creatures = sim.Simulation.Creatures.Creatures;
            Assert.NotEmpty(creatures);
            foreach (var creature in creatures)
            {
                Assert.True(frame.TryGetIndex(creature.Id, out int index), $"{creature} isn't in the frame.");
                ref readonly var view = ref frame[index];
                Assert.True(view.IsCreature && !view.IsCrawler);
                Assert.Equal(EntityType.Creature, view.Type);
                Assert.Equal(creature.Kind, view.Creature);
                Assert.Equal(creature.Mode, view.CreatureMode);
                Assert.Equal(creature.Position, view.Position);
            }
            int inFrame = 0;
            for (int i = 0; i < frame.Count; i++) if (frame[i].IsCreature) inFrame++;
            Assert.Equal(creatures.Count, inFrame);
        }

        [Theory]
        [InlineData("top-down")]
        [InlineData("isometric")]
        public void TheFogHidesThemAndChartedEachIsDrawnAsItsKind(string mode)
        {
            using var sim = Outpost();
            var scene = new WorldScene { Mode = mode == "top-down" ? ViewMode.TopDown : ViewMode.Isometric };
            scene.Build(sim.Host.Current.Map, TestFrames.Of(sim), 0f);
            Assert.Empty(CreaturesDrawn(scene)); // both lairs lie in sectors that open uncharted

            ChartTheLairs(sim);
            var frames = TestFrames.Of(sim);
            scene.Build(sim.Host.Current.Map, frames, 0f);
            var drawn = CreaturesDrawn(scene);
            var creatures = sim.Simulation.Creatures.Creatures;
            Assert.Equal(creatures.Count, drawn.Count);
            foreach (var creature in creatures)
            {
                var art = scene.Catalog.Piece(scene.Visuals.Creature(creature.Kind), 0f);
                Assert.Contains(drawn, d => d.SpriteIndex == art.Sprite && d.X == creature.Position.X && d.Y == creature.Position.Y && d.Tint.A == 255
                    && d.Pick >= 0 && scene.Items.PickedEntity(d.Pick) == creature.Id); // a click picks it (K2.5)
            }
            Assert.Contains(drawn, d => d.SpriteIndex == SpriteSheets.World.IndexOf(CreatureSprites.Name("burrow-rat", 0)));
            Assert.Contains(drawn, d => d.SpriteIndex == SpriteSheets.World.IndexOf(CreatureSprites.Name("stone-beetle", 0)));
        }

        [Fact]
        public void ACreatureScuttlesWhileItMovesAndStandsStillAtItsPlace()
        {
            using var sim = Outpost();
            ChartTheLairs(sim);
            using var frames = new WorldViewFrames();
            frames.Follow(sim.Host, sim.Registry, sim.Sectors, sim.Clock);
            var scene = new WorldScene { Mode = ViewMode.TopDown };
            var service = sim.Simulation.Creatures;
            var rat = service.Creatures.First(c => c.Kind == CreatureKind.BurrowRat);
            var map = sim.Host.Current.Map;
            int stride = SpriteSheets.World.IndexOf(CreatureSprites.Name("burrow-rat", 1));
            float half = 0.5f / TopDownArtTable.Creatures.Single(r => r.Key == "burrow-rat").FramesPerSecond; // half a frame in

            // At its place, resting: its first frame, whenever it is drawn
            Assert.Equal(CreatureMode.Rest, rat.Mode);
            scene.Advance(map, frames, 0f);
            Assert.False(scene.Creatures.Moving(rat.Id));

            // Roaming: it moved over the tick, so it runs through its frames
            for (int t = 0; t < 3000 && !(rat.Mode == CreatureMode.Roam && scene.Creatures.Moving(rat.Id)); t++)
            {
                sim.Step(1);
                scene.Advance(map, frames, sim.Clock.CurrentTick / 10f);
            }
            Assert.True(scene.Creatures.Moving(rat.Id), "The rat roamed.");
            var frameIndices = new HashSet<int>();
            for (int step = 0; step < 4; step++)
            {
                scene.Present(map, frames, step * 2 * half + half);
                frameIndices.Add(CreaturesDrawn(scene).Single(d => d.X == rat.Position.X && d.Y == rat.Position.Y).SpriteIndex);
            }
            Assert.Contains(stride, frameIndices);
            Assert.Equal(2, frameIndices.Count);

            // Back at its place and resting: still again
            for (int t = 0; t < 3000 && rat.Mode != CreatureMode.Rest; t++) sim.Step(1);
            sim.Step(1);
            scene.Advance(map, frames, sim.Clock.CurrentTick / 10f);
            Assert.Equal(CreatureMode.Rest, rat.Mode);
            Assert.False(scene.Creatures.Moving(rat.Id));
            for (int step = 0; step < 4; step++)
            {
                scene.Present(map, frames, step * 2 * half + half);
                Assert.NotEqual(stride, CreaturesDrawn(scene).Single(d => d.X == rat.Position.X && d.Y == rat.Position.Y).SpriteIndex);
            }
        }

        /// <summary>
        /// Facing is the view's (by hand, a creature walked about): left on screen mirrors it, right faces it the way its
        /// sprites face, and straight up or down the screen keeps the way it last went. Going +y on the floor is straight up a
        /// top-down screen, but up and to the left in isometric.
        /// </summary>
        [Fact]
        public void ACreatureFacesTheWayItWentOnScreen()
        {
            var state = new CreatureState(new EntityId(0, 0, 0, 7), CreatureKind.BurrowRat, 0, 0, new Vec2(5f, 5f));
            var entities = new List<WorldEntity> { new WorldEntity(state.Id, EntityType.Creature, state.Home, 0.25f, 2.2f, creature: state) };
            using var frames = new WorldViewFrames();
            frames.Restart(entities, null, 0);
            long tick = 0;
            bool Walk(Vec2 by, IViewProjection projection, CreatureAppearance appearance)
            {
                state.Position += by;
                frames.Capture(entities, null, ++tick);
                appearance.Advance(frames, projection);
                Assert.True(appearance.Moving(state.Id));
                return appearance.FacesLeft(state.Id);
            }

            var topDown = new CreatureAppearance();
            topDown.Advance(frames, TopDownProjection.Instance);
            Assert.False(topDown.FacesLeft(state.Id)); // never seen moving: the way its sprites face
            Assert.True(Walk(new Vec2(-0.2f, 0f), TopDownProjection.Instance, topDown));
            Assert.True(Walk(new Vec2(0f, 0.2f), TopDownProjection.Instance, topDown)); // straight up the screen: still left
            Assert.False(Walk(new Vec2(0.2f, 0f), TopDownProjection.Instance, topDown));
            Assert.False(Walk(new Vec2(0f, -0.2f), TopDownProjection.Instance, topDown));

            var isometric = new CreatureAppearance();
            var iso = ViewMode.Isometric.Projection;
            Assert.True(Walk(new Vec2(0f, 0.2f), iso, isometric)); // +y: up and to the left on screen
            Assert.False(Walk(new Vec2(0.2f, 0f), iso, isometric)); // +x: down and to the right
            Assert.False(Walk(new Vec2(0.2f, 0.2f), iso, isometric)); // straight up the screen: as it was

            // Standing, it keeps its facing and holds still; gone from the frame, it is forgotten
            frames.Capture(entities, null, ++tick);
            isometric.Advance(frames, iso);
            Assert.False(isometric.Moving(state.Id));
            entities.Clear();
            frames.Capture(entities, null, ++tick);
            isometric.Advance(frames, iso);
            Assert.Equal(0, isometric.Remembered);
        }

        /// <summary>
        /// The strike pose (K2.5, with live hunting): a rat that swings at a stray held by its warren lunges at it, facing
        /// it, deepest halfway through <see cref="CreatureAppearance.StrikeSeconds"/> and back where it stands at the end; its
        /// shadow stays put.
        /// </summary>
        [Fact]
        public void ARatLungesAtWhatItStrikes()
        {
            using var sim = Outpost();
            ChartTheLairs(sim);
            using var frames = new WorldViewFrames();
            frames.Follow(sim.Host, sim.Registry, sim.Sectors, sim.Clock);
            var scene = new WorldScene { Mode = ViewMode.TopDown };
            var map = sim.Host.Current.Map;
            var stray = sim.Host.CrawlerStates.First(c => !c.IsDrifter);
            int strikerIndex = -1;
            for (int t = 0; t < 300 && strikerIndex < 0; t++)
            {
                stray.Position = new Vec2(20f, 14f); // by hand: it stands by the warren
                sim.Step(1);
                var frame = frames.Current;
                for (int i = 0; i < frame.Count && strikerIndex < 0; i++) if (frame[i].IsCreature && frame[i].Striking) strikerIndex = i;
            }
            Assert.True(strikerIndex >= 0, "A rat struck the stray.");
            ref readonly var striker = ref frames.Current[strikerIndex];
            Assert.Equal(stray.AgentId, striker.CreatureTarget);
            float t0 = 100f;
            frames.Alpha = 1f;
            scene.Advance(map, frames, t0);
            var at = striker.Position;
            Assert.True(frames.TryGetDrawnPosition(stray.AgentId, out var quarry));
            var art = scene.Catalog.Piece(scene.Visuals.Creature(striker.Creature), 0f);
            DrawItem Drawn(float seconds)
            {
                scene.Present(map, frames, seconds);
                return CreaturesDrawn(scene).Where(d => d.SpriteIndex == art.Sprite || d.SpriteIndex == art.Sprite + 1)
                    .OrderBy(d => (new Vec2(d.X, d.Y) - at).SqrMagnitude).First();
            }

            var deepest = Drawn(t0 + CreatureAppearance.StrikeSeconds / 2f);
            float lunged = Vec2.Distance(new Vec2(deepest.X, deepest.Y), at);
            Assert.Equal(WorldScene.LungeDistance, lunged, 3);
            var toward = (quarry - at).Normalized;
            var went = (new Vec2(deepest.X, deepest.Y) - at).Normalized;
            Assert.True(Vec2.Dot(toward, went) > 0.99f, "It lunged at the stray.");
            Assert.Equal(quarry.X < at.X, deepest.FlipX); // facing it
            var after = Drawn(t0 + CreatureAppearance.StrikeSeconds);
            Assert.Equal(at.X, after.X, 4);
            Assert.Equal(at.Y, after.Y, 4);
        }

        /// <summary>
        /// K2.5: a click on a creature selects it (not as a crawler), with its sector, and the camera follows; the ring and its
        /// kind's name are drawn over it; a click on a crawler then selects the crawler and drops the creature.
        /// </summary>
        [Fact]
        public void AClickOnACreatureSelectsItWithItsRingAndName()
        {
            using var sim = Outpost();
            ChartTheLairs(sim);
            var identity = (Simpiens.Narrative.IAgentIdentityService)sim.Services.GetService(typeof(Simpiens.Narrative.IAgentIdentityService));
            var frames = TestFrames.Of(sim);
            var selection = new WorldSelection(frames, sim.Sectors, identity);
            var map = sim.Host.Current.Map;
            var scene = new WorldScene();
            scene.Build(map, frames, 0f);
            var view = new View(TopDownProjection.Instance, new ViewCamera(map.CameraFrame.Center, 32f, 1280, 720));
            void Click(Vec2 world)
            {
                var screen = view.WorldToScreen(world);
                WorldPicker.TryPick(scene.Items, view, screen, out var picked);
                selection.ClickAt(view.ScreenToFloor(screen), picked);
            }

            var beetle = sim.Simulation.Creatures.Creatures.First(c => c.Kind == CreatureKind.StoneBeetle);
            Click(beetle.Position);
            Assert.Equal(beetle.Id, selection.SelectedCreature);
            Assert.Equal(default, selection.SelectedCrawler);
            Assert.True(selection.Following);
            Assert.Equal(Floor1Layout.DeepSeamSectorId, selection.SelectedSector);
            Assert.Equal("Stone Beetle", selection.SelectedName());
            Assert.True(selection.TryGetSelectedPosition(out var at) && at == beetle.Position);
            scene.Build(map, frames, 0f, selection);
            Assert.Contains(scene.Labels, l => l.Text == "Stone Beetle");
            var ring = scene.Catalog.Piece(scene.Catalog.Id(ArtKeys.SelectionRing), 0f).Sprite;
            Assert.Contains(Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]), d => d.SpriteIndex == ring && d.X == beetle.Position.X && d.Y == beetle.Position.Y);

            var crawler = sim.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn);
            Click(crawler.Position);
            Assert.Equal(crawler.Id, selection.SelectedCrawler);
            Assert.Equal(default, selection.SelectedCreature);
        }

        /// <summary>
        /// K2.5: the map inspector shows a selected creature: its kind's name and card (its sprite three to one, in the frame
        /// of a wild thing) in the portrait's place, what it does, its health against its kind's, no needs and no dossier.
        /// The game's own picture: a beetle of the hollow, the Deep Seam charted by hand.
        /// </summary>
        [Fact]
        public void TheInspectorShowsACreature()
        {
            var game = Offscreen.Start(new ClientOptions { View = ViewMode.Isometric, TicksPerUpdate = 100 }, frames: 1, width: 1280, height: 720);
            var sim = game.Session.Simulation;
            ChartTheLairs(sim);
            game.RunOneFrame();
            game.RunOneFrame();
            game.Play.Dashboard.TogglePanels();
            var beetle = sim.Simulation.Creatures.Creatures.First(c => c.Kind == CreatureKind.StoneBeetle);
            game.Session.Selection.SelectCreature(beetle.Id, follow: false);
            game.Session.Simulation.Clock.SetTimeMultiplier(1f);
            game.Play.Dashboard.Refresh();
            var card = game.Play.Dashboard.Inspector;
            var model = game.Session.DashboardModel.Inspector;
            Assert.True(card.Root.Visible && model.IsCreature);
            Assert.Equal("Stone Beetle", card.NameShown);
            Assert.Equal("Wild. Its lair is in Deep Seam.", card.MoodShown);
            Assert.Equal(100f * beetle.Health / beetle.Definition.MaxHealth, model.Health, 3);
            Assert.False(card.OpenDossierButton.Visual.Visible);
            Assert.True(card.Portrait.Visible);
            var at = beetle.Position;
            game.World.Rig.ZoomAt(at, 0.3f);
            game.World.Rig.Pan(at - game.World.Rig.Position);
            RenderTests.CompareWithReference("render-iso-inspector-beetle-t300", FrameCapture.Capture(game, 1280, 720), 1280, 720);
        }

        /// <summary>A creature's card (K2.5) is its sprite three to one inside a framed portrait's square: the same pixels every bake.</summary>
        [Fact]
        public void ACreaturesCardIsItsSpriteThreeToOneInItsFrame()
        {
            var card = new Rgba32[CreatureCards.FramedSize * CreatureCards.FramedSize];
            var again = new Rgba32[card.Length];
            CreatureCards.BakeFramed(CreatureKind.BurrowRat, card);
            CreatureCards.BakeFramed(CreatureKind.BurrowRat, again);
            Assert.Equal(card, again);
            Assert.Equal(CreatureCards.FrameOuter, card[0]);
            Assert.Equal(CreatureCards.FrameInner, card[CreatureCards.FramedSize + 1]);
            var sprite = CreatureSprites.All.First(s => s.Name == CreatureSprites.Name("burrow-rat", 0));
            var eye = sprite.Pixels.First(p => p.R == 236 && p.G == 58 && p.B == 50); // the rat's red eye
            Assert.Equal(CreatureCards.Scale * CreatureCards.Scale, card.Count(p => p.Equals(eye))); // one sprite pixel, three by three
        }

        /// <summary>
        /// The game's own picture, close up: the rat warren in the Overgrowth at t300 (charted by hand at t100, its fog faded),
        /// the pack resting at its places and roaming about them.
        /// </summary>
        [Fact]
        public void TheWarrenInIsometricLooksAsRecorded()
            => RenderTests.CompareWithReference("render-iso-warren-t300", TheLair(ViewMode.Isometric, Floor1Layout.RatWarren), Width, Height);

        /// <summary>The beetle hollow in the Deep Seam at t300 in top-down, as above.</summary>
        [Fact]
        public void TheHollowInTopDownLooksAsRecorded()
            => RenderTests.CompareWithReference("render-floor1-hollow-t300", TheLair(ViewMode.TopDown, Floor1Layout.BeetleHollow), Width, Height);

        private static byte[] TheLair(ViewMode mode, string lairKey)
        {
            var game = Offscreen.Start(new ClientOptions { View = mode, TicksPerUpdate = 100 }, frames: 1, width: Width, height: Height);
            var sim = game.Session.Simulation;
            ChartTheLairs(sim);
            game.RunOneFrame();
            game.RunOneFrame();
            Assert.Equal(300L, sim.Clock.CurrentTick);
            Assert.True(sim.Host.Current.Map.TryGetLair(lairKey, out var lair));
            game.World.Rig.ZoomAt(lair.Position, 0.3f);
            game.World.Rig.Pan(lair.Position - game.World.Rig.Position);
            var frame = game.Session.Frames.Current;
            int shown = 0;
            for (int i = 0; i < frame.Count; i++) if (frame[i].IsCreature && sim.Host.Current.Map.FindSector(frame[i].Position).Id == sim.Host.Current.Map.FindSector(lair.Position).Id) shown++;
            Assert.True(shown > 0, "The lair's pack is in the frame.");
            return FrameCapture.CaptureMap(game, Width, Height);
        }
    }
}
