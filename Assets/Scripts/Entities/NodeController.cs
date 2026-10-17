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

        public EntityId Id { get; private set; }
        public float BaseSpeed => _baseSpeed > 0f ? _baseSpeed : 2f;

#if UNITY_EDITOR
        [Header("Editor Debug Info")]
        [SerializeField] private string _inspectorGuid;
#endif

        // Expose radius for spatial hashing
        public float Radius => col != null ? col.radius : (GetComponent<CircleCollider2D>() != null ? GetComponent<CircleCollider2D>().radius : 0.5f);

        // The simulation's view of this node is engine-free (Epic CORE C4); the transform converts at this boundary
        public Vec2 LogicalPosition { get; private set; }
        public Vec2 PreviousLogicalPosition { get; private set; }
        private bool _hasInitializedLogicalPos;

        // Expose position and velocity for the snapshot and simulation
        public Vec2 Position
        {
            get
            {
                if (transform != null && transform.hasChanged)
                {
                    LogicalPosition = transform.position.ToSim();
                    PreviousLogicalPosition = LogicalPosition;
                    transform.hasChanged = false;
                    _hasInitializedLogicalPos = true;
                }
                return _hasInitializedLogicalPos ? LogicalPosition : (transform != null ? transform.position.ToSim() : Vec2.Zero);
            }
        }
        public Vec2 Velocity => rb != null ? rb.linearVelocity.ToSim() : Vec2.Zero;

        public void SetLogicalPosition(Vec2 pos)
        {
            PreviousLogicalPosition = _hasInitializedLogicalPos ? LogicalPosition : pos;
            LogicalPosition = pos;
            _hasInitializedLogicalPos = true;

            if (Agent != null && Agent.State != null)
            {
                Agent.State.Position = pos;
            }

            // In headless test mode or if clock is missing, immediately sync transform
            if (_simulationClock == null && transform != null)
            {
                transform.position = pos.ToUnity();
                transform.hasChanged = false;
            }
        }
        
        public EntityType Type { get; set; } = EntityType.Pawn;

        /// <summary>
        /// Spawn-time yield classification for resource nodes, mirrored into EntitySnapshot.
        /// The authoritative yield state lives in ResourceData.
        /// </summary>
        public Simpiens.Simulation.Economy.ItemType ResourceKind { get; private set; }

        public void ConfigureResourceKind(Simpiens.Simulation.Economy.ItemType kind)
        {
            ResourceKind = kind;
        }

        private Simpiens.Cognition.AutonomousAgent _agent;
        public Simpiens.Cognition.AutonomousAgent Agent
        {
            get
            {
                if (_agent == null) _agent = GetComponent<Simpiens.Cognition.AutonomousAgent>();
                return _agent;
            }
        }

        public Simpiens.Cognition.AgentState AgentState => Agent != null ? Agent.State : null;

        private IWorldRegistry _registry;

        public void SetRegistry(IWorldRegistry registry)
        {
            _registry = registry;
        }

        [Inject]
        public void Construct(ISimulationClock clock = null, IWorldRegistry registry = null)
        {
            _simulationClock = clock;
            if (_simulationClock != null)
            {
                _simulationClock.OnTimeMultiplierChanged += HandleMultiplierChanged;
            }
            if (registry != null)
            {
                _registry = registry;
            }
        }

        // Configuration is now injected via code, completely bypassing the Unity Inspector
        public void Initialize(EntityId id, NodeConfiguration config)
        {
            Id = id;
            _baseSpeed = config.Speed;

            if (transform != null)
            {
                LogicalPosition = transform.position.ToSim();
                PreviousLogicalPosition = LogicalPosition;
                transform.hasChanged = false;
                _hasInitializedLogicalPos = true;
            }

#if UNITY_EDITOR
            _inspectorGuid = id.ToString();
#endif

            if (rb == null) rb = GetComponent<Rigidbody2D>();
            if (col == null) col = GetComponent<CircleCollider2D>();

            // Only apply automatic physics velocity to Dynamic bodies (e.g. bouncing nodes)
            // Kinematic bodies (like SwarmAgents) are moved explicitly via their own controllers.
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
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

            if (transform != null)
            {
                LogicalPosition = transform.position.ToSim();
                PreviousLogicalPosition = LogicalPosition;
                transform.hasChanged = false;
                _hasInitializedLogicalPos = true;
            }

            // For a top-down 2D simulation, we don't want nodes to fall or rotate when bumping.
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            // Continuous dynamic collision detection prevents fast-moving nodes from tunneling 
            // through boundaries, though it is slightly more CPU intensive.
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void Update()
        {
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) return;

            if (_hasInitializedLogicalPos && transform != null)
            {
                float alpha = _simulationClock != null ? _simulationClock.InterpolationAlpha : 1f;
                transform.position = Vector2.Lerp(PreviousLogicalPosition.ToUnity(), LogicalPosition.ToUnity(), alpha);
                transform.hasChanged = false;
            }
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
            if (_registry != null)
            {
                _registry.UnregisterNode(this);
            }
        }
    }
}
