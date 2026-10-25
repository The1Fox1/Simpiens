namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// A zero-allocation, 12-byte stack struct representing the agent's core physiological
    /// and psychological drives. Passed by value or in-parameter into AgentContext.
    /// </summary>
    public struct AgentNeeds
    {
        public float Hunger; // 0.0f (satiated) to 100.0f (starving)
        public float Energy; // 100.0f (fully rested) to 0.0f (exhausted)
        public float Social; // 100.0f (connected) to 0.0f (isolated)

        public AgentNeeds(float hunger, float energy, float social)
        {
            Hunger = hunger;
            Energy = energy;
            Social = social;
        }

        public static AgentNeeds Default => new AgentNeeds(50f, 100f, 50f);

        public override string ToString()
        {
            return $"[Needs: Hunger={Hunger:F1}, Energy={Energy:F1}, Social={Social:F1}]";
        }
    }
}
