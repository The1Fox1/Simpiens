namespace Simpiens.Simulation
{
    public interface ISimulationManager
    {
        void Pause();
        void Resume();
        bool IsPaused { get; }
        void EnqueueIntent(Simpiens.Cognition.Contracts.AgentIntent intent);
        void RegisterResource(ResourceData data);
    }
}
