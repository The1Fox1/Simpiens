using UnityEngine;

namespace Simpiens.Entities
{
    // [RequireComponent] enforces that Unity will automatically add these components 
    // to the GameObject if they aren't already there. This is crucial for avoiding 
    // NullReferenceExceptions at runtime, which are common when relying on designer setup.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class NodeController : MonoBehaviour
    {
        private float _speed;
        private Rigidbody2D rb;

        // Configuration is now injected via code, completely bypassing the Unity Inspector
        public void Initialize(NodeConfiguration config)
        {
            _speed = config.Speed;
            
            // Assign a random initial direction
            float randomAngle = Random.Range(0f, 360f);
            Vector2 initialDirection = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle)).normalized;

            // Instead of translating in Update(), we set velocity once. 
            // The physics engine handles continuous movement from here, eliminating 
            // per-frame C# overhead and garbage generation for basic movement.
            rb.linearVelocity = initialDirection * _speed;
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

        // We removed Update() entirely. 
        // Zero allocation in hot paths is easily achieved if there is no hot path to begin with!
        // The physics engine natively handles the bounds bouncing and inter-node collisions.
    }
}
