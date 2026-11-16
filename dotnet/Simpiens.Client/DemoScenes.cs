using Simpiens.Core.Session.Layouts;
using Simpiens.Entities;
using Simpiens.Narrative;
using Simpiens.Presentation.Cutscenes;

namespace Simpiens.Client
{
    /// <summary>
    /// Demonstration cutscenes for development builds (M3 plan step 3.5; producer P3): one of each kind, until the
    /// narrative work writes the real ones. Release builds have none (<see cref="ClientOptions.DemoScenes"/>).
    /// </summary>
    public static class DemoScenes
    {
        /// <summary>A hard cutscene, played with F9: two crawlers at the outpost at dusk, then at the deep seam.</summary>
        public static CutsceneScript Arrival { get; } = new CutsceneBuilder("demo-arrival", CutsceneKind.Hard)
            .Character("tara", "Tara", AgentVisualState.Idle)
            .Character("bob", "Bob", AgentVisualState.Walking)
            .Backdrop(CutsceneArt.Outpost)
            .Enter("tara", StageSlot.Left)
            .Say("tara", "The caravan left at noon. We hold the outpost until it comes back.")
            .Enter("bob", StageSlot.Right, facingLeft: true)
            .Say("bob", "Then we'd better find out what's in the deep seam before the rations run out.")
            .Pose("tara", AgentVisualState.Thinking)
            .Say("tara", "Nobody who went past the quarry has charted it yet.")
            .Wait(0.4f)
            .Backdrop(CutsceneArt.DeepSeam)
            .Pose("bob", AgentVisualState.Harvesting)
            .Say("bob", "Iron. Enough to buy the whole caravan.")
            .Leave("tara")
            .Say("bob", "...Tara?")
            .Build();

        /// <summary>Two crawlers gossip (their first gossip in this world): the one who started it, and the other.</summary>
        public static CutsceneScript FirstGossip { get; } = new CutsceneBuilder("demo-first-gossip", CutsceneKind.Dynamic)
            .Actors("talker", "listener")
            .Focus("talker")
            .Say("talker", "Heard anything from the deep seam?")
            .Focus("listener")
            .Say("listener", "Only that nobody's come back from it yet.")
            .Build();

        /// <summary>
        /// The first crawler into the deep seam: in play, the scout, while the room is still uncharted, so the fog hides
        /// it and its lines show in the box at the bottom of the window.
        /// </summary>
        public static CutsceneScript DeepSeamEntry { get; } = new CutsceneBuilder("demo-deep-seam-entry", CutsceneKind.Dynamic)
            .Actors("walker")
            .Focus("walker")
            .Say("walker", "So this is the deep seam...")
            .Wait(0.5f)
            .Say("walker", "The walls are full of iron.")
            .Build();

        /// <summary>
        /// A rescue (Epic 10 Part 3, K5.8d): the one carried out of danger, or stood up by a draught, and the friend who came,
        /// as the chronicle tells it (its first actor the rescuer, its second the rescued).
        /// </summary>
        public static CutsceneScript Rescue { get; } = new CutsceneBuilder("demo-rescue", CutsceneKind.Dynamic)
            .Actors("rescuer", "rescued")
            .Focus("rescued")
            .Say("rescued", "I thought that was the end of me.")
            .Focus("rescuer")
            .Say("rescuer", "Not while I could hear you. Rest now.")
            .Build();

        /// <summary>A rescue's scene waits at least a game hour after the last (420 ticks).</summary>
        public const long RescueCooldownTicks = 420;

        /// <summary>
        /// Arms the dynamic demonstrations: the first gossip, and the first crawler into the deep seam, once per world each; and
        /// every rescue, a game hour apart at the most often (K5.8d).
        /// </summary>
        public static void AddTo(SceneDirector director)
        {
            director.Add(SceneTrigger.OnInteraction(FirstGossip, once: true, cooldownTicks: 0, ChronicleEventType.CampfireGossip));
            director.Add(SceneTrigger.OnEntering(Floor1Layout.DeepSeamSectorId, DeepSeamEntry));
            director.Add(SceneTrigger.OnInteraction(Rescue, once: false, cooldownTicks: RescueCooldownTicks, ChronicleEventType.CrawlerRescued));
        }
    }
}
