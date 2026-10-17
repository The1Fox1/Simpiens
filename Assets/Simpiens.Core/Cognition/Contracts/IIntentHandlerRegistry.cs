using System;
using Simpiens.Entities;
using Simpiens.Simulation;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Registry for intent execution handlers, dispatching intents in zero heap allocations.
    /// </summary>
    public interface IIntentHandlerRegistry
    {
        void RegisterHandler(IIntentHandler handler);
        bool TryGetHandler(Type intentType, out IIntentHandler handler);
        IntentResult ExecuteIntent(WorldEntity node, ActiveIntentState state, IIntentExecutionContext context);
    }
}
