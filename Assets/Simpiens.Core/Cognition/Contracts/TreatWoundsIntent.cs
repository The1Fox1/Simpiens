namespace Simpiens.Cognition.Contracts
{
    /// <summary>A hurt crawler eats a medicinal herb it carries and heals at once (Epic 10 G2): herbs gain a use.</summary>
    public class TreatWoundsIntent : AgentIntent
    {
        public TreatWoundsIntent(EntityId agentId) : base(agentId)
        {
        }
    }
}
