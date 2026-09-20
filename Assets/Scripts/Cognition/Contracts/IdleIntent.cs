namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Represents an intent to do nothing or stay idle.
    /// </summary>
    public class IdleIntent : AgentIntent
    {
        public readonly float Duration;

        public IdleIntent(UnityEngine.GUID agentId, float duration = 2.0f) : base(agentId)
        {
            Duration = duration;
        }
    }
}
