using System;
using System.Collections.Generic;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Manages the collection of available GOAP actions and resolves prerequisite
    /// dependencies between actions, enabling zero-allocation graph traversal for planners.
    /// </summary>
    public class ActionGraph
    {
        private readonly List<GoapAction> _actions;

        public IReadOnlyList<GoapAction> Actions => _actions;

        public ActionGraph(int initialCapacity = 16)
        {
            _actions = new List<GoapAction>(initialCapacity);
        }

        /// <summary>
        /// Registers an action into the graph.
        /// </summary>
        public void RegisterAction(GoapAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!_actions.Contains(action))
            {
                _actions.Add(action);
            }
        }

        /// <summary>
        /// Zero-allocation forward query: populates the results list with all actions
        /// whose preconditions are satisfied by the current world state.
        /// </summary>
        public void GetApplicableActions(in WorldState currentState, List<GoapAction> results)
        {
            int count = _actions.Count;
            for (int i = 0; i < count; i++)
            {
                var action = _actions[i];
                if (action.CanRunOn(currentState))
                {
                    results.Add(action);
                }
            }
        }

        /// <summary>
        /// Zero-allocation backward query: populates results with all actions whose effects
        /// satisfy at least one required fact in targetState without contradicting other required facts.
        /// </summary>
        public void GetActionsSatisfying(in WorldState targetState, List<GoapAction> results)
        {
            int count = _actions.Count;
            for (int i = 0; i < count; i++)
            {
                var action = _actions[i];
                ulong overlapMask = action.Effects.Mask & targetState.Mask;

                // Action must contribute to at least one required fact
                if (overlapMask != 0)
                {
                    // And the contributed facts must match the required values (no contradictory effects)
                    if ((action.Effects.Values & overlapMask) == (targetState.Values & overlapMask))
                    {
                        results.Add(action);
                    }
                }
            }
        }

        /// <summary>
        /// Validates that an action's preconditions can be satisfied by other actions in the graph
        /// or by initial agent states.
        /// </summary>
        public bool HasProducerFor(StateFact fact, bool requiredValue)
        {
            var targetState = WorldState.Empty.With(fact, requiredValue);
            int count = _actions.Count;
            for (int i = 0; i < count; i++)
            {
                var action = _actions[i];
                ulong bit = 1UL << (int)fact;
                if ((action.Effects.Mask & bit) != 0 && ((action.Effects.Values & bit) != 0) == requiredValue)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
