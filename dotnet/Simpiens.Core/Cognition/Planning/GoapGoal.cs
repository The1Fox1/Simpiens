using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Abstract base class for goals in the GOAP architecture.
    /// Defines a desired target state and dynamic priority scoring.
    /// </summary>
    public abstract class GoapGoal
    {
        public string Name { get; }
        public WorldState DesiredState { get; protected set; }

        protected GoapGoal(string name, in WorldState desiredState)
        {
            Name = name;
            DesiredState = desiredState;
        }

        /// <summary>
        /// Checks whether the target goal state is already satisfied by the agent's current state.
        /// </summary>
        public bool IsSatisfied(in WorldState currentState)
        {
            return currentState.Satisfies(DesiredState);
        }

        /// <summary>
        /// Calculates the dynamic priority of this goal based on agent context
        /// (e.g., priority scales with hunger or danger level).
        /// Higher score means higher selection precedence.
        /// </summary>
        public abstract float CalculatePriority(AgentContext context);

        /// <summary>
        /// Validates whether this goal is currently viable or relevant for the agent.
        /// </summary>
        public virtual bool IsValid(AgentContext context)
        {
            return true;
        }

        public override string ToString() => $"[Goal: {Name}, Desired={DesiredState}]";
    }
}
