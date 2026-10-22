using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Presentation;
using Simpiens.Presentation.Cutscenes;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Dynamic scenes over the live world (M3 design D9, plan step 3.4; producers P3, P7, P8): what starts them, one at a
    /// time, and how they show while the game goes on.
    /// </summary>
    public class DynamicSceneTests
    {
        private static CutsceneScript Chat() => new CutsceneBuilder("test-chat", CutsceneKind.Dynamic)
            .Actors("talker", "listener")
            .Focus("talker")
            .Say("talker", "Did you see the caravan?")
            .Say("listener", "Not since noon.")
            .Build();

        private static CutsceneScript Arrive() => new CutsceneBuilder("test-arrive", CutsceneKind.Dynamic)
            .Actors("walker").Say("walker", "Here at last.").Build();

        private static HeadlessSimulation Outpost()
        {
            var sim = HeadlessSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            return sim;
        }

        private static INarrativeChronicleService Chronicle(HeadlessSimulation sim) => (INarrativeChronicleService)sim.Services.GetService(typeof(INarrativeChronicleService));

        /// <summary>A crawler's interaction with another, as the simulation records it (recorded by hand: a fixture).</summary>
        private static void Gossip(HeadlessSimulation sim, EntityId a, EntityId b, long tick)
            => Chronicle(sim).RecordEvent(new ChronicleEvent(ChronicleEventType.CampfireGossip, a, b, Floor1Layout.HubSectorId, (uint)tick));

        [Fact]
        public void EnteringAZoneStartsItsSceneOnThatTick()
        {
            using var sim = Outpost();
            var frames = new WorldViewFrames();
            frames.Follow(sim.Host, sim.Registry, sim.Sectors, sim.Clock, Chronicle(sim));
            var director = new SceneDirector(frames);
            director.Add(SceneTrigger.OnEntering(Floor1Layout.GardenSectorId, Arrive()));
            var walker = sim.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn && sim.Sectors.GetSectorAt(e.Position).Id != Floor1Layout.GardenSectorId);

            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 1);
            Assert.Null(director.Current);
            walker.SetPosition(sim.Host.Current.Map.Sectors.First(s => s.Id == Floor1Layout.GardenSectorId).Bounds.Center); // a fixture: it walks in
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 2);

            Assert.NotNull(director.Current);
            Assert.Equal(2L, director.Current.StartedTick);
            Assert.Equal(walker.Id, director.Current.ActorOf("walker"));

            // Already inside is not entering: the next capture starts nothing new, and the scene plays on
            director.End();
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 3);
            Assert.Null(director.Current);
        }

        [Fact]
        public void AnInteractionStartsASceneWithBothCrawlersAndOneScenePlaysAtATime()
        {
            using var sim = Outpost();
            var frames = new WorldViewFrames();
            frames.Follow(sim.Host, sim.Registry, sim.Sectors, sim.Clock, Chronicle(sim));
            var director = new SceneDirector(frames);
            director.Add(SceneTrigger.OnInteraction(Chat(), once: false, cooldownTicks: 50, ChronicleEventType.CampfireGossip));
            var crawlers = sim.Registry.ActiveEntities.Where(e => e.Type == EntityType.Pawn).Take(4).ToList();

            Chronicle(sim).RecordEvent(new ChronicleEvent(ChronicleEventType.TradeTransaction, crawlers[0].Id, crawlers[1].Id, 1, 1)); // not counted
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 1);
            Assert.Null(director.Current);

            Gossip(sim, crawlers[0].Id, crawlers[1].Id, 2);
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 2);
            var first = director.Current;
            Assert.NotNull(first);
            Assert.Equal(new[] { crawlers[0].Id, crawlers[1].Id }, first.Actors);
            Assert.Equal(crawlers[1].Id, first.ActorOf("listener"));

            Gossip(sim, crawlers[2].Id, crawlers[3].Id, 3); // while one plays: dropped
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 3);
            Assert.Same(first, director.Current);

            director.End();
            Gossip(sim, crawlers[2].Id, crawlers[3].Id, 20); // within the cooldown
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 20);
            Assert.Null(director.Current);
            Gossip(sim, crawlers[2].Id, crawlers[3].Id, 52); // after it
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 52);
            Assert.Equal(crawlers[2].Id, director.Current.ActorOf("talker"));
        }

        [Fact]
        public void AOncePerWorldTriggerReArmsInANewWorldAndTheOldScenesEnds()
        {
            using var sim = Outpost();
            var frames = new WorldViewFrames();
            frames.Follow(sim.Host, sim.Registry, sim.Sectors, sim.Clock, Chronicle(sim));
            var director = new SceneDirector(frames);
            director.Add(SceneTrigger.OnInteraction(Chat(), once: true, cooldownTicks: 0, ChronicleEventType.CampfireGossip));
            var crawlers = sim.Registry.ActiveEntities.Where(e => e.Type == EntityType.Pawn).Take(2).ToList();

            Gossip(sim, crawlers[0].Id, crawlers[1].Id, 1);
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 1);
            Assert.NotNull(director.Current);
            director.End();
            Gossip(sim, crawlers[0].Id, crawlers[1].Id, 2);
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 2);
            Assert.Null(director.Current); // once

            Gossip(sim, crawlers[0].Id, crawlers[1].Id, 3);
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 3);
            Assert.Null(director.Current);
            sim.Respawn(OutpostSession.SessionId); // a new world (the frames follow the host): its chronicle's past isn't new
            Assert.Null(director.Current);
            crawlers = sim.Registry.ActiveEntities.Where(e => e.Type == EntityType.Pawn).Take(2).ToList();
            Gossip(sim, crawlers[0].Id, crawlers[1].Id, 1);
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 1);
            Assert.NotNull(director.Current);
            frames.Dispose();
        }

        /// <summary>The outpost running 2 ticks a frame at the game's size, with scripted input and a chat trigger armed.</summary>
        private static UiDriver Running()
        {
            var ui = UiDriver.Start(new ClientOptions { TicksPerUpdate = 2 }, 1280, 720);
            ui.Game.Session.Simulation.Clock.SetTimeMultiplier(1f);
            ui.Game.Session.Scenes.Add(SceneTrigger.OnInteraction(Chat(), once: false, cooldownTicks: 0, ChronicleEventType.CampfireGossip));
            ui.Game.Play.Dashboard.TogglePanels(); // to the map, as a player would to watch the outpost
            ui.Frame();
            return ui;
        }

        /// <summary>Starts the chat between the two crawlers nearest the hub (a recorded gossip: a fixture), close up on them.</summary>
        private static (EntityId Talker, EntityId Listener) StartChat(UiDriver ui)
        {
            var game = ui.Game;
            var hub = game.Session.Simulation.Host.Current.Map.Labels.First(l => l.Text == "Outpost Hub").Position;
            var pair = game.Session.Simulation.Registry.ActiveEntities.Where(e => e.Type == EntityType.Pawn).OrderBy(e => (e.Position - hub).SqrMagnitude).Take(2).ToList();
            game.World.Rig.ZoomAt(pair[0].Position, 0.4f);
            Gossip(game.Session.Simulation, pair[0].Id, pair[1].Id, game.Session.Simulation.Clock.CurrentTick);
            ui.Frame();
            Assert.NotNull(game.Session.Scenes.Current);
            return (pair[0].Id, pair[1].Id);
        }

        [Fact]
        public void TheWorldRunsWhileItPlaysAndItsBubbleFollowsTheSpeaker()
        {
            var ui = Running();
            var game = ui.Game;
            var (talker, _) = StartChat(ui);
            Assert.Equal(talker, game.World.DirectedAt); // the scene's focus
            long tick = game.Session.Simulation.Clock.CurrentTick;
            ui.Frame(3);
            Assert.Equal(tick + 6, game.Session.Simulation.Clock.CurrentTick); // the game goes on

            var view = game.Play.Scenes;
            Assert.True(view.Bubble.Shown);
            Assert.False(view.Box.Shown);
            Assert.Equal(talker, view.Speaker);
            Assert.Equal(game.Session.Identity.GetFriendlyName(talker), view.Bubble.Speaker.Text);
            AssertOverHead(game, talker, view.Bubble);
            ui.Frame(5); // it walks; the bubble goes with it
            AssertOverHead(game, talker, view.Bubble);
        }

        private static void AssertOverHead(SimpiensGame game, EntityId crawler, Simpiens.Client.Ui.CutsceneTextBox bubble)
        {
            FrameCapture.Capture(game, 1280, 720);
            game.Session.Frames.TryGetDrawnPosition(crawler, out var at);
            var head = game.World.View.WorldToScreen(at, game.World.Mode.Art.NameHeight + 0.25f);
            Assert.InRange(bubble.Root.X + DynamicSceneView.BubbleWidth / 2f, head.X - 1f, head.X + 1f);
            Assert.InRange(bubble.Root.Y + DynamicSceneView.BubbleHeight, head.Y - 1f, head.Y + 1f);
        }

        [Fact]
        public void ASpeakerTheFogHidesSpeaksFromTheBoxAndAClickThereBringsTheCameraBack()
        {
            var ui = Running();
            var game = ui.Game;
            var (talker, _) = StartChat(ui);
            var uncharted = game.Session.Simulation.Host.Current.Map.Sectors.First(s => !game.Session.Simulation.Sectors.IsExplored(s.Id));
            game.Session.Simulation.Registry.ActiveEntities.First(e => e.Id == talker).SetPosition(uncharted.Bounds.Center); // a fixture: into the fog
            ui.Frame(2);
            var view = game.Play.Scenes;
            Assert.Equal(talker, view.Speaker);
            Assert.True(view.Box.Shown);
            Assert.False(view.Bubble.Shown);

            // The player pans away: the camera is theirs; a click on the box sends it back to the speaker
            ui.Script.Keys = new KeyInput { Left = true };
            ui.Frame(3);
            ui.Script.Keys = default;
            Assert.Equal(default, game.World.DirectedAt);
            ui.Click(new Vec2(view.Box.Root.X + 40f, view.Box.Root.Y + 30f));
            Assert.Equal(talker, game.World.DirectedAt);
        }

        [Fact]
        public void ThePlayerCanClickAwayAndTheSceneGoesOn()
        {
            var ui = Running();
            var game = ui.Game;
            var (talker, listener) = StartChat(ui);
            var other = game.Session.Simulation.Registry.ActiveEntities.First(e => e.Type == EntityType.Pawn && e.Id != talker && e.Id != listener
                && game.Session.Simulation.Sectors.IsExplored(game.Session.Simulation.Sectors.GetSectorAt(e.Position).Id));
            game.World.ClickMap(game.World.View.WorldToScreen(other.Position)); // the player goes to watch another crawler
            ui.Frame();
            Assert.Equal(other.Id, game.Session.Selection.SelectedCrawler);
            Assert.Equal(default, game.World.DirectedAt); // following their own crawler took the camera back
            Assert.NotNull(game.Session.Scenes.Current); // the scene plays on
            Assert.Equal(talker, game.Play.Scenes.Speaker);

            ui.Script.Keys = new KeyInput { AdvancePressed = true }; // Space hurries it along
            ui.Frame();
            ui.Script.Keys = new KeyInput { AdvancePressed = true };
            ui.Frame();
            ui.Script.Keys = default;
            Assert.Equal(listener, game.Play.Scenes.Speaker);
        }

        [Fact]
        public void LinesMoveOnByThemselvesAndTheSceneEnds()
        {
            var ui = Running();
            var game = ui.Game;
            StartChat(ui);
            for (int i = 0; i < 400 && game.Session.Scenes.Current != null; i++) ui.Frame();
            Assert.Null(game.Session.Scenes.Current);
            Assert.False(game.Play.Scenes.Bubble.Shown || game.Play.Scenes.Box.Shown);
            Assert.Equal(default, game.World.DirectedAt);
        }
    }
}
