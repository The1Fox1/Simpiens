using Simpiens.Entities;

namespace Simpiens.Presentation.Cutscenes
{
    /// <summary>
    /// The story's hard cutscenes, played by the game when the world reaches them (Epic 10 Part 3, K10.5d): placeholder lines
    /// until the narrative work (Epic N) writes them. The client plays each once a session (<c>ClientOptions.StoryScenes</c>).
    /// </summary>
    public static class StoryScenes
    {
        /// <summary>The breach (Q13: "told by the chronicle and a hard cutscene"): the Gatekeeper fallen, the gate open on the dark.</summary>
        public static CutsceneScript Breach { get; } = new CutsceneBuilder("story-breach", CutsceneKind.Hard)
            .Character("guild", "The guild", AgentVisualState.Defiant)
            .Backdrop(CutsceneArt.OldStair)
            .Enter("guild", StageSlot.Left)
            .Say("guild", "The Gatekeeper is down. The old gate stands open.")
            .Pose("guild", AgentVisualState.Thinking)
            .Say("guild", "Stairs, going down. Further than any lamp of ours can reach.")
            .Wait(0.4f)
            .Pose("guild", AgentVisualState.Idle)
            .Say("guild", "Whatever lives below has heard us knocking.")
            .Build();
    }
}
