using Simpiens.Simulation;
using UnityEngine;

namespace Simpiens.Cognition
{
    public class ContextValidator
    {
        private readonly ISimulationClock _simulationClock;

        // Configurable threshold: how many ticks can pass before a decision is deemed stale.
        // If simulation runs at 10 ticks/sec, 50 ticks is 5 seconds of logical time.
        private const long StaleTickThreshold = 50;

        public ContextValidator(ISimulationClock simulationClock)
        {
            _simulationClock = simulationClock;
        }

        /// <summary>
        /// Validates if an incoming decision is still relevant based on the current simulation tick.
        /// </summary>
        public bool IsDecisionValid(AgentDecision decision)
        {
            long tickDelta = _simulationClock.CurrentTick - decision.InitiatedTick;

            if (tickDelta > StaleTickThreshold)
            {
                Debug.LogWarning($"[ContextValidator] Decision '{decision.ActionId}' discarded. Delta: {tickDelta} ticks.");
                return false;
            }

            return true;
        }
    }
}
