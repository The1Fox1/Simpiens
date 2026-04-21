using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer;

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
        private CircleCollider2D col;
        private ISimulationClock _simulationClock;

        public GUID Id { get; private set; }

        // Expose radius for spatial hashing
        public float Radius => col != null ? col.radius : 0.5f;

        // Expose position and velocity for the snapshot
        public Vector2 Position => transform.position;
        public Vector2 Velocity => rb.linearVelocity;
        
        public EntityType Type { get; set; } = EntityType.Pawn;

        [Inject]
        public void Construct(ISimulationClock clock)
        {
            _simulationClock = clock;
            _simulationClock.OnTimeMultiplierChanged += HandleMultiplierChanged;
        }

        // Configuration is now injected via code, completely bypassing the Unity Inspector
        public void Initialize(GUID id, NodeConfiguration config)
        {
            Id = id;
            _baseSpeed = config.Speed;

            // Only apply automatic physics velocity to Dynamic bodies (e.g. bouncing nodes)
            // Kinematic bodies (like SwarmAgents) are moved explicitly via their own controllers.
            if (rb.bodyType == RigidbodyType2D.Dynamic)
            {
                // Assign a random initial direction
                float randomAngle = Random.Range(0f, 360f);
                Vector2 initialDirection = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle)).normalized;

                // Instead of translating in Update(), we set velocity once. 
                // We scale the base speed by the clock's multiplier.
                float initialMultiplier = _simulationClock != null ? _simulationClock.TimeMultiplier : 1f;
                rb.linearVelocity = initialDirection * (_baseSpeed * initialMultiplier);
            }
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<CircleCollider2D>();

            // For a top-down 2D simulation, we don't want nodes to fall or rotate when bumping.
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            // Continuous dynamic collision detection prevents fast-moving nodes from tunneling 
            // through boundaries, though it is slightly more CPU intensive.
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void HandleMultiplierChanged(float newMultiplier)
        {
            if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) return;

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
