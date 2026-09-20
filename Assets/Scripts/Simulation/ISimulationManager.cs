namespace Simpiens.Simulation
{
    public interface ISimulationManager
    {
        void Pause();
        void Resume();
        bool IsPaused { get; }
        void EnqueueIntent(Simpiens.Cognition.Contracts.AgentIntent intent);
        bool AbortIntent(UnityEngine.GUID agentId);
        void RegisterResource(ResourceData data);
    }
}
