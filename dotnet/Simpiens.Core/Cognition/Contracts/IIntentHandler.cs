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
        IntentResult Execute(WorldEntity node, ActiveIntentState state, IIntentExecutionContext context);
    }

    /// <summary>
    /// Strongly-typed base class for modular intent handlers.
    /// </summary>
    public abstract class IntentHandlerBase<TIntent> : IIntentHandler where TIntent : AgentIntent
    {
        public Type SupportedIntentType => typeof(TIntent);

        public IntentResult Execute(WorldEntity node, ActiveIntentState state, IIntentExecutionContext context)
        {
            if (state != null && state.Intent is TIntent typedIntent)
            {
                return Execute(node, typedIntent, state, context);
            }
            return IntentResult.Aborted;
        }

        protected abstract IntentResult Execute(WorldEntity node, TIntent intent, ActiveIntentState state, IIntentExecutionContext context);

        /// <summary>
        /// An action's work at its place (Epic 10 Part 2, T2.4, D22): true while it still takes time, so the handler returns
        /// InProgress and runs its checks again next tick. Work of 0 seconds finishes within the tick.
        /// </summary>
        protected static bool StillWorking(WorldEntity node, ActiveIntentState state, IIntentExecutionContext context, float seconds)
        {
            if (seconds <= 0f || state.ElapsedTime >= seconds) return false;
            state.ElapsedTime += context.FixedTickDeltaTime;
            var agent = node.Agent;
            if (agent != null && agent.VisualState != Simpiens.Entities.AgentVisualState.Sleeping) agent.VisualState = Simpiens.Entities.AgentVisualState.Idle;
            return true;
        }
    }
}
