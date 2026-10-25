namespace Simpiens.Entities
{
    /// <summary>
    /// Visual action states for autonomous agents.
    /// Used by the view layer (AgentView) to render appropriate sprites, emotes, and animations.
    /// </summary>
    public enum AgentVisualState : byte
    {
        Idle = 0,
        Walking = 1,
        Harvesting = 2,
        Gossiping = 3,
        Panicking = 4,
        Thinking = 5,
        Resting = 6,
        Yielding = 7,
        Sharing = 8,
        Rallying = 9,
        Defiant = 10,
        Sleeping = 11 // lying in a bunk, under the blanket (Epic MAP Phase 3 rest anchor)
    }
}
