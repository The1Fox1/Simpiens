using System.Threading;
using Cysharp.Threading.Tasks;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Evaluators;
using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Cognition
{
    public class AutonomousAgent : MonoBehaviour
    {
        // Dependencies
        private ISpatialPartition _spatialPartition;
        private ICognitiveEvaluator _evaluator;
        private ISimulationManager _simulationManager;

        // Private fields
        private bool _isEvaluating;
        private CancellationTokenSource _cts;


        // Public properties
        public UnityEngine.GUID AgentId { get; private set; }
        //    State
        public float Hunger { get; set; } = 50f;
        public float Energy { get; set; } = 100f;

        public bool HasActiveIntent { get; private set; }



        public void Initialize(UnityEngine.GUID id, ISpatialPartition spatialPartition, ICognitiveEvaluator evaluator, ISimulationManager simulationManager)
        {
            AgentId = id;
            _spatialPartition = spatialPartition;
            _evaluator = evaluator;
            _simulationManager = simulationManager;

            _cts = new CancellationTokenSource();
        }

        public void ManualUpdate() // Called by SwarmSpawner to avoid Monobehaviour Update overhead
        {
            if (_isEvaluating || HasActiveIntent) return;

            // Simple drive simulation
            Hunger += Time.deltaTime * 2f; // Hunger increases over time

            EvaluateCognitionAsync().Forget();
        }

        private async UniTaskVoid EvaluateCognitionAsync()
        {
            var snapshot = _spatialPartition.GetActiveSnapshot();
            if (snapshot == null) return;

            _isEvaluating = true;

            // The ContextBuilder essentially aggregates this
            var context = new AgentContext(AgentId, transform.position, Hunger, Energy, snapshot);

            try
            {
                // Explicitly jump off the main thread for cognitive evaluation
                await UniTask.SwitchToThreadPool();

                // Ensure the snapshot stays alive while we process in background
                snapshot.Retain();

                var intent = await _evaluator.EvaluateAsync(context, _cts.Token);

                // Set intent tracking so agent waits until it completes
                HasActiveIntent = true;
                intent.OnComplete = () => HasActiveIntent = false;

                // Pass back to simulation manager queue
                _simulationManager.EnqueueIntent(intent);
            }
            catch (System.OperationCanceledException)
            {
                // Task was canceled
            }
            finally
            {
                snapshot.Release();
                _isEvaluating = false;
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
