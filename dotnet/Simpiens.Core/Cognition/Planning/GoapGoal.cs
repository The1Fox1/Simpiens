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

        /// <summary>What the goal is for (Epic LIFE-1, L0.2, D1): the motive model weighs goals by it.</summary>
        public Motive Motive { get; }

        /// <summary>The part of the day it belongs to (D1): the routine and the day's intention weigh goals by it. See <see cref="FamilyFor"/>.</summary>
        public ActivityFamily Family { get; }

        protected GoapGoal(string name, in WorldState desiredState, Motive motive = Motive.None, ActivityFamily family = ActivityFamily.Unweighted)
        {
            Name = name;
            DesiredState = desiredState;
            Motive = motive;
            Family = family;
        }

        /// <summary>The goal's family for <paramref name="context"/>: its declared <see cref="Family"/>, unless the goal names another by context (loot gathered under a clear's orders is unweighted, as orders are).</summary>
        public virtual ActivityFamily FamilyFor(AgentContext context) => Family;

        /// <summary>
        /// Checks whether the target goal state is already satisfied by the agent's current state.
        /// </summary>
        public bool IsSatisfied(in WorldState currentState)
        {
            return currentState.Satisfies(DesiredState);
        }

        /// <summary>
        /// The goal's own number for <paramref name="context"/>, its base (Epic LIFE-1, L0.5, D2): the motive model multiplies
        /// it by the weights every goal shares (<see cref="MotiveModel"/>), and only the model and the validators that pin a
        /// goal's number call it. A new goal's base is its motive's urgency (<see cref="Urgency"/>) times its own opportunity.
        /// Higher means more wanted.
        /// </summary>
        public abstract float BasePriority(AgentContext context);

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
