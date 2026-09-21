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
        Thinking = 5
    }
}
