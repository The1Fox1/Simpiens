namespace Simpiens.Simulation
{
    public partial class SimulationManager
    {
        // --- Gossip, the hearth and succession (Epic 8), kept by SocialDynamics ---
        public void RegisterAnchor(EntityId id, Vec2 position, float radius = 3.0f) => _social.RegisterAnchor(id, position, radius);

        public void CheckGossipOpportunity(WorldEntity node) => _social.CheckGossipOpportunity(node);

        internal void EvaluateHearthProximity(uint currentTick) => _social.EvaluateHearthProximity(currentTick);

        internal void EvaluateLeadershipSuccession(uint currentTick) => _social.EvaluateLeadershipSuccession(currentTick);
    }
}
