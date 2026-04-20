namespace Simpiens.Simulation
{
    public interface ISimulationManager
    {
        void Pause();
        void Resume();
        bool IsPaused { get; }
    }
}
