namespace Simpiens.Cognition.Contracts
{
    /// <summary>Read the first manual carried, at rest (Epic 11, S3.3, D11): two game hours, then its practice, and it is used up.</summary>
    public class ReadManualIntent : AgentIntent
    {
        public ReadManualIntent(EntityId agentId) : base(agentId)
        {
        }
    }
}
