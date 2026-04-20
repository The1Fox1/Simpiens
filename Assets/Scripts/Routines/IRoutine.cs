namespace Simpiens.Routines
{
    /// <summary>
    /// Represents a continuous visual/physical action (like moving or animating)
    /// that can be processed smoothly on the main thread by an AgentMotor.
    /// </summary>
    public interface IRoutine
    {
        bool IsComplete { get; }
        
        void OnStart();
        void OnUpdate(float deltaTime);
        void OnCancel();
    }
}
