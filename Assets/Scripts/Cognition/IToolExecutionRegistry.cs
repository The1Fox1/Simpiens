namespace Simpiens.Cognition
{
    public interface IToolExecutionRegistry
    {
        /// <summary>
        /// Allows the cognitive layer to trigger hard-set game mechanics safely.
        /// </summary>
        /// <param name="agentId">The agent performing the action.</param>
        /// <param name="actionType">The predefined action identifier (e.g., "move_to", "gather").</param>
        /// <param name="parameters">JSON or defined struct of parameters.</param>
        void ExecuteAction(string agentId, string actionType, string parameters);
    }
}
