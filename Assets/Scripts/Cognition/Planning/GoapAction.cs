using Simpiens.Cognition.Contracts;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Abstract base class for actions within the Goal-Oriented Action Planning system.
    /// Defines prerequisites (preconditions), outcomes (effects), and cost functions.
    /// </summary>
    public abstract class GoapAction
    {
        public string Name { get; }
        public float BaseCost { get; protected set; }

        public WorldState Preconditions { get; protected set; }
        public WorldState Effects { get; protected set; }

        protected GoapAction(string name, float baseCost = 1f)
        {
            Name = name;
            BaseCost = baseCost;
            Preconditions = WorldState.Empty;
            Effects = WorldState.Empty;
        }

        /// <summary>
        /// Checks whether this action's preconditions are satisfied by the given world state.
        /// </summary>
        public bool CanRunOn(in WorldState state)
        {
            return state.Satisfies(Preconditions);
        }

        /// <summary>
        /// Procedural validity check evaluated against agent context (e.g., verifying reachability
        /// or checking if a remembered target still exists). Defaults to true.
        /// </summary>
        public virtual bool IsValid(AgentContext context)
        {
            return true;
        }

        /// <summary>
        /// Computes the dynamic execution cost of this action for the given agent context.
        /// Defaults to BaseCost, but can be overridden to factor in distance, fatigue, or risk.
        /// </summary>
        public virtual float GetCost(AgentContext context)
        {
            return BaseCost;
        }

        /// <summary>
        /// Factory method that produces the executable AgentIntent when this action is triggered.
        /// </summary>
        public abstract AgentIntent CreateIntent(AgentContext context);

        /// <summary>
        /// Asynchronous factory method that produces an AgentIntent, allowing actions to perform
        /// asynchronous pathfinding or queries on worker threads.
        /// </summary>
        public virtual Cysharp.Threading.Tasks.UniTask<AgentIntent> CreateIntentAsync(AgentContext context, System.Threading.CancellationToken ct = default)
        {
            return Cysharp.Threading.Tasks.UniTask.FromResult(CreateIntent(context));
        }

        public override string ToString() => $"[Action: {Name}, Cost={BaseCost}]";
    }
}
