using System.Collections.Generic;
using Simpiens.Entities;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// Each crawler state's two frames, by sprite name (graphics V5): the names <see cref="ProceduralSprites"/> gives them.
    /// It depends on nothing, so generators (<see cref="WornSprites"/>) and tables (<see cref="TopDownArtTable"/>) can both
    /// read it without a cycle in their static initialisation.
    /// </summary>
    public static class CrawlerFrames
    {
        public static IReadOnlyList<(AgentVisualState State, string[] Sprites)> All { get; } = new[]
        {
            (AgentVisualState.Idle, Frames("Idle")),
            (AgentVisualState.Walking, Frames("Walk")),
            (AgentVisualState.Harvesting, Frames("Harvest")),
            (AgentVisualState.Gossiping, Frames("Gossip")),
            (AgentVisualState.Panicking, Frames("Panic")),
            (AgentVisualState.Thinking, Frames("Think")),
            (AgentVisualState.Resting, Frames("Rest")),
            (AgentVisualState.Yielding, Frames("Yield")),
            (AgentVisualState.Sharing, Frames("Share")),
            (AgentVisualState.Rallying, Frames("Rally")),
            (AgentVisualState.Defiant, Frames("Defiant")),
            (AgentVisualState.Sleeping, Frames("Sleep")),
        };

        private static string[] Frames(string stem) => new[] { "Agent_" + stem + "_0", "Agent_" + stem + "_1" };
    }
}
