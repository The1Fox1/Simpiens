namespace Simpiens.Narrative
{
    /// <summary>
    /// Service for mapping procedural GUIDs to human-friendly agent identities (e.g., Burt, Bob, Alice).
    /// Guarantees 100% deterministic naming across game ticks, scene reloads, and play sessions.
    /// </summary>
    public interface IAgentIdentityService
    {
        /// <summary>
        /// Returns the friendly persona name for the given agent (e.g. "Burt", "Bob", "Alice").
        /// </summary>
        string GetFriendlyName(EntityId agentId);

        /// <summary>
        /// Returns a narrative role or title for the agent (e.g. "Settler", "Forager", "Scout").
        /// </summary>
        string GetFriendlyTitle(EntityId agentId);

        /// <summary>
        /// Returns a formatted name combining the friendly persona with an optional technical identifier.
        /// (e.g. "Burt [SwarmAgent_6]").
        /// </summary>
        string GetFormattedDisplayName(EntityId agentId, string fallbackName = null);

        /// <summary>
        /// Allows registering or overriding an agent's name (e.g. player renamed pawn or narrative event).
        /// </summary>
        void RegisterCustomName(EntityId agentId, string name);

        /// <summary>
        /// Forgets every registered name (a respawn or a load: entity ids repeat per seed); hashed names stay the fallback.
        /// </summary>
        void ClearCustomNames();

        /// <summary>
        /// Allows registering or overriding an agent's title.
        /// </summary>
        void RegisterCustomTitle(EntityId agentId, string title);
    }
}
