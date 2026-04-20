using UnityEngine;
using VContainer;
using Simpiens.Simulation;

namespace Simpiens.Entities
{
    // [RequireComponent] enforces that Unity will automatically add these components 
    // to the GameObject if they aren't already there. This is crucial for avoiding 
    // NullReferenceExceptions at runtime, which are common when relying on designer setup.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class NodeController : MonoBehaviour
    {
        private float _baseSpeed;
        private Rigidbody2D rb;
        private ISimulationClock _simulationClock;

        [Inject]
        public void Construct(ISimulationClock clock)
        {
            _simulationClock = clock;
            _simulationClock.OnTimeMultiplierChanged += HandleMultiplierChanged;
        }

        // Configuration is now injected via code, completely bypassing the Unity Inspector
        public void Initialize(NodeConfiguration config)
        {
            _baseSpeed = config.Speed;
            
            // Assign a random initial direction
            float randomAngle = Random.Range(0f, 360f);
            Vector2 initialDirection = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle)).normalized;

            // Instead of translating in Update(), we set velocity once. 
            // We scale the base speed by the clock's multiplier.
            float initialMultiplier = _simulationClock != null ? _simulationClock.TimeMultiplier : 1f;
            rb.linearVelocity = initialDirection * (_baseSpeed * initialMultiplier);
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            
            // For a top-down 2D simulation, we don't want nodes to fall or rotate when bumping.
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            
            // Continuous dynamic collision detection prevents fast-moving nodes from tunneling 
            // through boundaries, though it is slightly more CPU intensive.
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void HandleMultiplierChanged(float newMultiplier)
        {
            if (rb == null) return;
            
            // Re-scale the current velocity magnitude while preserving direction
            if (rb.linearVelocity.sqrMagnitude > 0.001f)
            {
                Vector2 currentDirection = rb.linearVelocity.normalized;
                rb.linearVelocity = currentDirection * (_baseSpeed * newMultiplier);
            }
            else
            {
                // Edge case: if velocity was zero, pick a random direction to resume
                float randomAngle = Random.Range(0f, 360f);
                Vector2 dir = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle)).normalized;
                rb.linearVelocity = dir * (_baseSpeed * newMultiplier);
            }
        }

        private void OnDestroy()
        {
            if (_simulationClock != null)
            {
                _simulationClock.OnTimeMultiplierChanged -= HandleMultiplierChanged;
            }
        }
    }
}
