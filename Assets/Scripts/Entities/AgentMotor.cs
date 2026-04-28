using Simpiens.Core;
using Simpiens.Simulation;
using UnityEngine;

namespace Simpiens.Entities
{
    /// <summary>
    /// Dumb view component attached to an Agent.
    /// Executes continuous physical/visual routines on the main thread 
    /// while the CognitiveEngine handles the background reasoning.
    /// </summary>
    public class AgentMotor : MonoBehaviour
    {
        private IRoutine _currentRoutine;
        private ISimulationClock _simulationClock;

        [VContainer.Inject]
        public void Construct(ISimulationClock clock)
        {
            _simulationClock = clock;
        }

        public void SetRoutine(IRoutine newRoutine)
        {
            if (_currentRoutine != null && !_currentRoutine.IsComplete)
            {
                _currentRoutine.OnCancel();
            }

            _currentRoutine = newRoutine;
            _currentRoutine?.OnStart();
        }

        private void Update()
        {
            // This runs on normal Time.deltaTime scaled by the simulation multiplier,
            // ensuring smooth interpolations completely independent of the logical SimulationClock tick rate.
            if (_currentRoutine != null && !_currentRoutine.IsComplete)
            {
                float multiplier = _simulationClock != null ? _simulationClock.TimeMultiplier : 1f;
                _currentRoutine.OnUpdate(Time.deltaTime * multiplier);
            }
        }
    }
}
