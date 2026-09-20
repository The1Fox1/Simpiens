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
        private ICognitiveEvaluator[] _evaluators;
        private ISimulationManager _simulationManager;
        private ISimulationClock _clock;

        // Private fields
        private Simpiens.Cognition.Memory.AgentMemory _memory;
        private bool _isEvaluating;
        private CancellationTokenSource _cts;


        // Public properties
        public UnityEngine.GUID AgentId { get; private set; }
        public Simpiens.Cognition.Memory.AgentMemory Memory => _memory;

        // State
        public float Hunger { get; set; } = 50f;
        public float Energy { get; set; } = 100f;
        public float Frustration { get; set; } = 0f;

        public bool HasActiveIntent { get; private set; }

        public void RelieveFrustration(float amount)
        {
            Frustration = Mathf.Max(0f, Frustration - amount);
        }



        public void Initialize(UnityEngine.GUID id, ISpatialPartition spatialPartition, ICognitiveEvaluator[] evaluators, ISimulationManager simulationManager, ISimulationClock clock)
        {
            AgentId = id;
            _spatialPartition = spatialPartition;
            _evaluators = evaluators;
            _simulationManager = simulationManager;
            _clock = clock;

            _memory = new Simpiens.Cognition.Memory.AgentMemory();
            _cts = new CancellationTokenSource();
        }

        public void ManualUpdate() // Called by SwarmSpawner to avoid Monobehaviour Update overhead
        {
            if (_isEvaluating || HasActiveIntent) return;

            // Simple drive simulation
            Hunger += Time.deltaTime * 2f; // Hunger increases over time
            Frustration = Mathf.Max(0f, Frustration - Time.deltaTime * 1f); // Decays slowly

            EvaluateCognitionAsync().Forget();
        }

        private async UniTaskVoid EvaluateCognitionAsync()
        {
            var snapshot = _spatialPartition.GetActiveSnapshot();
            if (snapshot == null) return;

            _isEvaluating = true;

            Vector2Int currentPosInt = new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.y));
            _memory.UpdateMemory(snapshot, currentPosInt, visionRadius: 20, currentTick: (uint)_clock.CurrentTick);

            // The ContextBuilder essentially aggregates this
            var context = new AgentContext(AgentId, transform.position, Hunger, Energy, Frustration, snapshot, _memory, (uint)_clock.CurrentTick);

            try
            {
                // Explicitly jump off the main thread for cognitive evaluation
                await UniTask.SwitchToThreadPool();

                // Ensure the snapshot stays alive while we process in background
                snapshot.Retain();

                AgentIntent intent = null;
                for (int i = 0; i < _evaluators.Length; i++)
                {
                    intent = await _evaluators[i].EvaluateAsync(context, _cts.Token);
                    if (intent != null) break;
                }

                if (intent == null) intent = new IdleIntent(context.AgentId);

                // Set intent tracking so agent waits until it completes
                HasActiveIntent = true;
                intent.OnComplete = (result) => 
                {
                    HasActiveIntent = false;

                    uint currentTick = (uint)_clock.CurrentTick;

                    if (intent is PanicIntent)
                    {
                        Frustration = 0f;
                    }
                    else if (result == IntentResult.Success)
                    {
                        Frustration = Mathf.Max(0f, Frustration - 50f);
                        if (intent is IdleIntent) Frustration += 5f;
                    }
                    else if (result == IntentResult.TargetMissing || result == IntentResult.PathBlocked)
                    {
                        Frustration += 25f;
                    }

                    if (intent is HarvestResourceIntent harvestIntent)
                    {
                        if (result == IntentResult.TargetMissing)
                        {
                            _memory.RemoveMemory(harvestIntent.TargetEntityId);
                        }
                        else if (result == IntentResult.PathBlocked || result == IntentResult.Aborted)
                        {
                            _memory.BlacklistEntity(harvestIntent.TargetEntityId, currentTick + 100);
                        }
                    }
                };

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
