namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Represents an intent to sleep in a specific bunk the agent is standing at. The bunk is claimed on the main
    /// thread when the sleep starts; a bunk holds one sleeper (Epic MAP Phase 3).
    /// </summary>
    public class SleepAtBunkIntent : AgentIntent
    {
        public readonly EntityId BunkId;
        public readonly float Duration;

        public SleepAtBunkIntent(EntityId agentId, EntityId bunkId, float duration) : base(agentId)
        {
            BunkId = bunkId;
            Duration = duration;
        }
    }
}
