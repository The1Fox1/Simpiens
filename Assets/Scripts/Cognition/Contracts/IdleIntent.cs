namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Represents an intent to do nothing or stay idle.
    /// </summary>
    public class IdleIntent : AgentIntent
    {
        public IdleIntent(UnityEngine.GUID agentId) : base(agentId)
        {
        }
    }
}
