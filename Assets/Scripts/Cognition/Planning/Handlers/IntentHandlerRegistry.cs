using System;
using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;

namespace Simpiens.Cognition.Contracts
{
    /// <summary>
    /// Pure C# registry for mapping and executing AgentIntent types without heap allocations during simulation ticks.
    /// </summary>
    public class IntentHandlerRegistry : IIntentHandlerRegistry
    {
        private readonly Dictionary<Type, IIntentHandler> _handlers = new Dictionary<Type, IIntentHandler>(16);

        public void RegisterHandler(IIntentHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers[handler.SupportedIntentType] = handler;
        }

        public bool TryGetHandler(Type intentType, out IIntentHandler handler)
        {
            return _handlers.TryGetValue(intentType, out handler);
        }

        public IntentResult ExecuteIntent(NodeController node, ActiveIntentState state, IIntentExecutionContext context)
        {
            if (state == null || state.Intent == null)
            {
                return IntentResult.Aborted;
            }

            if (_handlers.TryGetValue(state.Intent.GetType(), out var handler))
            {
                return handler.Execute(node, state, context);
            }

            // Fallback for intents without registered handlers
            return IntentResult.Success;
        }

        /// <summary>
        /// Creates a registry pre-populated with default handlers for all core game intents.
        /// </summary>
        public static IntentHandlerRegistry CreateDefault()
        {
            var registry = new IntentHandlerRegistry();
            registry.RegisterHandler(new Simpiens.Cognition.Planning.Handlers.WanderIntentHandler());
            registry.RegisterHandler(new Simpiens.Cognition.Planning.Handlers.PanicIntentHandler());
            registry.RegisterHandler(new Simpiens.Cognition.Planning.Handlers.HarvestResourceIntentHandler());
            registry.RegisterHandler(new Simpiens.Cognition.Planning.Handlers.ShareResourceIntentHandler());
            registry.RegisterHandler(new Simpiens.Cognition.Planning.Handlers.IdleIntentHandler());
            registry.RegisterHandler(new Simpiens.Cognition.Planning.Handlers.EatCarriedFoodIntentHandler());
            registry.RegisterHandler(new Simpiens.Cognition.Planning.Handlers.TradeIntentHandler());
            registry.RegisterHandler(new Simpiens.Cognition.Planning.Handlers.ClaimBountyIntentHandler());
            registry.RegisterHandler(new Simpiens.Cognition.Planning.Handlers.DeliverBountyIntentHandler());
            registry.RegisterHandler(new Simpiens.Cognition.Planning.Handlers.ScoutIntentHandler());
            registry.RegisterHandler(new Simpiens.Cognition.Planning.Handlers.SleepAtBunkIntentHandler());
            return registry;
        }
    }
}
