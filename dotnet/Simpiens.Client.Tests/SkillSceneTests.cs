using System.Linq;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Presentation;
using Simpiens.Presentation.Cutscenes;
using Simpiens.Simulation;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// A band reached is a moment (Epic 11, S4.8, D17): a technique picked by one of the patron's people starts a dynamic scene
    /// with the crawler as its one actor, written from the chronicle's event (its vow, the road not taken); a resident's pick
    /// starts none; one a game hour at the most; and it plays in the window.
    /// </summary>
    public class SkillSceneTests
    {
        private static INarrativeChronicleService Chronicle(LocalSimulation sim) => (INarrativeChronicleService)sim.Services.GetService(typeof(INarrativeChronicleService));

        private static void Picked(LocalSimulation sim, EntityId who, Technique technique, long tick) =>
            Chronicle(sim).RecordEvent(new ChronicleEvent(ChronicleEventType.TechniqueChosen, who, default, Floor1Layout.HubSectorId, (uint)tick, ChronicleIntensity.Notable, (byte)technique));

        private static Simpiens.Cognition.AgentState Recruit(LocalSimulation sim, Simpiens.Cognition.AgentState crawler)
        {
            crawler.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(60, 80, 0, 0, 1, 0));
            var check = sim.Simulation.Guild.Recruit((IGuildHost)sim.Simulation, crawler.AgentId, 30, 1);
            Assert.Equal(GuildRules.Check.Ok, check);
            return crawler;
        }

        [Fact]
        public void ATechniquePickedByOneOfOursIsASceneWrittenFromIt()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var frames = new WorldViewFrames();
            frames.Follow(sim.Host, sim.Registry, sim.Sectors, sim.Clock, Chronicle(sim));
            var director = new SceneDirector(frames);
            SkillScenes.AddTo(director);
            var residents = sim.Host.CrawlerStates.Where(c => !c.IsDrifter).Take(3).ToList();
            var member = Recruit(sim, residents[0]);

            Picked(sim, residents[1].AgentId, Technique.ShieldWall, 2); // a resident's pick: the chronicle's, no scene
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 2);
            Assert.Null(director.Current);

            Picked(sim, member.AgentId, Technique.ShieldWall, 3);
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 3);
            var scene = director.Current;
            Assert.NotNull(scene);
            Assert.Equal(SceneTriggerKind.Moment, scene.Trigger.Kind);
            Assert.Equal(member.AgentId, scene.ActorOf(SkillScenes.Crawler));
            var lines = scene.Script.Steps.Where(s => s.Kind == CutsceneStepKind.Say).Select(s => s.Text).ToList();
            Assert.Equal(new[] { "Let them come. I'll stand.", "Reckless Fury? Not for me. Shield Wall." }, lines);

            // One a game hour at the most
            director.End();
            var second = Recruit(sim, residents[2]);
            Picked(sim, second.AgentId, Technique.QuickDraw, 100);
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 100);
            Assert.Null(director.Current);
            Picked(sim, second.AgentId, Technique.QuickDraw, 3 + SkillScenes.ChoiceCooldownTicks);
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 3 + SkillScenes.ChoiceCooldownTicks);
            Assert.NotNull(director.Current);
            Assert.Equal(second.AgentId, director.Current.ActorOf(SkillScenes.Crawler));
            Assert.Equal("Something's bound to hit.", director.Current.Script.Steps.First(s => s.Kind == CutsceneStepKind.Say).Text);
        }

        /// <summary>First blood (Epic 11, S6.4, D30): one of ours slaying its first creature is a scene in the voice of its best skill; a resident's is the chronicle's only.</summary>
        [Fact]
        public void FirstBloodIsASceneInTheVoiceOfItsBestSkill()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var frames = new WorldViewFrames();
            frames.Follow(sim.Host, sim.Registry, sim.Sectors, sim.Clock, Chronicle(sim));
            var director = new SceneDirector(frames);
            SkillScenes.AddTo(director);
            var residents = sim.Host.CrawlerStates.Where(c => !c.IsDrifter).Take(2).ToList();
            var member = Recruit(sim, residents[0]);
            void Blooded(EntityId who, Skill best, long tick) => Chronicle(sim).RecordEvent(new ChronicleEvent(ChronicleEventType.FirstBlood, who, default, Floor1Layout.HubSectorId, (uint)tick,
                ChronicleIntensity.Dramatic, (byte)Simpiens.Simulation.Creatures.CreatureKind.StoneBeetle + 256 * (byte)best));

            Blooded(residents[1].AgentId, Skill.Melee, 2);
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 2);
            Assert.Null(director.Current);
            Blooded(member.AgentId, Skill.Mining, 3);
            frames.Capture(sim.Registry.ActiveEntities, sim.Sectors, 3);
            Assert.NotNull(director.Current);
            Assert.Equal(member.AgentId, director.Current.ActorOf(SkillScenes.Crawler));
            var lines = director.Current.Script.Steps.Where(s => s.Kind == CutsceneStepKind.Say).Select(s => s.Text).ToList();
            Assert.Equal(new[] { SkillTable.Get(Skill.Mining).Voice.FirstKill, "A stone beetle. My first." }, lines);
            foreach (var skill in SkillTable.All)
                Assert.NotNull(SkillScenes.FirstBloodFor(new ChronicleEvent(ChronicleEventType.FirstBlood, member.AgentId, default, 0, 0, ChronicleIntensity.Dramatic, 1 + 256 * (byte)skill.Id)));
        }

        [Fact]
        public void EveryTechniqueWritesAScene()
        {
            foreach (var row in TechniqueTable.All)
            {
                var script = SkillScenes.ChoiceFor(new ChronicleEvent(ChronicleEventType.TechniqueChosen, new EntityId(1, 2, 3, 4), default, 0, 0, ChronicleIntensity.Notable, (byte)row.Id));
                Assert.NotNull(script);
                Assert.Equal(CutsceneKind.Dynamic, script.Kind);
                Assert.Equal(new[] { SkillScenes.Crawler }, script.Actors);
                Assert.Contains(script.Steps, s => s.Kind == CutsceneStepKind.Say && s.Text == row.Vow);
            }
            Assert.Null(SkillScenes.ChoiceFor(new ChronicleEvent(ChronicleEventType.TechniqueChosen, new EntityId(1, 2, 3, 4), default, 0, 0, ChronicleIntensity.Notable, 0f)));
        }

        [Fact]
        public void TheChoicePlaysInTheWindow()
        {
            var ui = UiDriver.Start(new ClientOptions { TicksPerUpdate = 1, SkillScenes = true }, 1280, 720);
            var session = ui.Game.Session;
            var member = session.Simulation.Host.CrawlerStates.First(c => !c.IsDrifter);
            member.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(60, 80, 0, 0, 1, 0));
            Assert.Equal(GuildRules.Check.Ok, session.Simulation.Simulation.Guild.Recruit((IGuildHost)session.Simulation.Simulation, member.AgentId, 30, 1));
            Picked(session.Simulation, member.AgentId, Technique.SteadyAim, session.Simulation.Clock.CurrentTick);
            for (int i = 0; i < 6 && session.Scenes.Current == null; i++) ui.Frame();
            Assert.NotNull(session.Scenes.Current);
            Assert.Equal(member.AgentId, session.Scenes.Current.ActorOf(SkillScenes.Crawler));
            Assert.Equal(SceneTriggerKind.Moment, session.Scenes.Current.Trigger.Kind);
        }
    }
}
