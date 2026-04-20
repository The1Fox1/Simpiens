namespace Simpiens.Cognition
{
    /// <summary>
    /// A dummy struct representing the payload returned from a slow cognitive task.
    /// It captures the tick when the decision process started.
    /// </summary>
    public readonly struct AgentDecision
    {
        public readonly string ActionId;
        public readonly long InitiatedTick;
        // In a real scenario, this would contain targeting info, string parameters, etc.

        public AgentDecision(string actionId, long initiatedTick)
        {
            ActionId = actionId;
            InitiatedTick = initiatedTick;
        }
    }
}
