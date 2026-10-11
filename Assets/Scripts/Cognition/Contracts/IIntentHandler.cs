using System;
using Simpiens.Entities;
using Simpiens.Simulation;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Interface for modular intent execution handlers.
    /// Eliminates the giant switch-statement in SimulationManager.
    /// </summary>
    public interface IIntentHandler
    {
        Type SupportedIntentType { get; }
        IntentResult Execute(NodeController node, ActiveIntentState state, IIntentExecutionContext context);
    }

    /// <summary>
    /// Strongly-typed base class for modular intent handlers.
    /// </summary>
    public abstract class IntentHandlerBase<TIntent> : IIntentHandler where TIntent : AgentIntent
    {
        public Type SupportedIntentType => typeof(TIntent);

        public IntentResult Execute(NodeController node, ActiveIntentState state, IIntentExecutionContext context)
        {
            if (state != null && state.Intent is TIntent typedIntent)
            {
                return Execute(node, typedIntent, state, context);
            }
            return IntentResult.Aborted;
        }

        protected abstract IntentResult Execute(NodeController node, TIntent intent, ActiveIntentState state, IIntentExecutionContext context);
    }
}
