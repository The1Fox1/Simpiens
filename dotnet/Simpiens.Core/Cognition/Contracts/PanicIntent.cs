namespace Simpiens.Cognition.Contracts
{
    public class PanicIntent : AgentIntent
    {
        public readonly Simpiens.Cognition.Pathfinding.PathResponse Path;

        public PanicIntent(EntityId agentId, Simpiens.Cognition.Pathfinding.PathResponse path) : base(agentId)
        {
            Path = path;
        }
    }
}
