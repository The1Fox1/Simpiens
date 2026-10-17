using Simpiens.Simulation;
using Simpiens.Simulation.Spatial;
using UnityEngine;

namespace Simpiens.Entities
{
    /// <summary>
    /// The view of one <see cref="WorldEntity"/> (Epic CORE C5): it draws the entity where the simulation says it is and
    /// never writes back. The body and collider stay for clicking and for the test harness's penetration probe; nothing
    /// in the simulation reads them.
    /// </summary>
    // [RequireComponent] enforces that Unity will automatically add these components
    // to the GameObject if they aren't already there. This is crucial for avoiding
    // NullReferenceExceptions at runtime, which are common when relying on designer setup.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class NodeController : MonoBehaviour
    {
        // The view drawing each entity, for harness code that measures what the player sees (the anomaly detector)
        private static readonly System.Collections.Generic.Dictionary<EntityId, NodeController> s_views = new System.Collections.Generic.Dictionary<EntityId, NodeController>(256);

        /// <summary>The live view drawing the entity <paramref name="id"/>, or null.</summary>
        public static NodeController ViewOf(EntityId id) => s_views.TryGetValue(id, out var view) && view != null ? view : null;

        private Rigidbody2D rb;
        private WorldEntity _entity;
        private ISimulationClock _clock;

#if UNITY_EDITOR
        [Header("Editor Debug Info")]
        [SerializeField] private string _inspectorGuid;
#endif

        /// <summary>The simulation entity this view draws.</summary>
        public WorldEntity Entity => _entity;

        public EntityId Id => _entity != null ? _entity.Id : default;
        public EntityType Type => _entity != null ? _entity.Type : EntityType.Pawn;

        /// <summary>The entity's logical position (where the simulation has it, not where it is drawn).</summary>
        public Vec2 Position => _entity != null ? _entity.Position : transform.position.ToSim();

        private Simpiens.Cognition.AutonomousAgent _agent;
        public Simpiens.Cognition.AutonomousAgent Agent
        {
            get
            {
                if (_agent == null) _agent = GetComponent<Simpiens.Cognition.AutonomousAgent>();
                return _agent;
            }
        }

        public Simpiens.Cognition.AgentState AgentState => _entity?.Agent;

        /// <summary>
        /// Draws <paramref name="entity"/> from now on. With a clock the view interpolates between the entity's last two
        /// positions; without one it draws the logical position (crawlers today).
        /// </summary>
        public void Bind(WorldEntity entity, ISimulationClock clock = null)
        {
            if (_entity != null && s_views.TryGetValue(_entity.Id, out var previous) && previous == this) s_views.Remove(_entity.Id);
            _entity = entity;
            _clock = clock;
            if (entity != null) s_views[entity.Id] = this;
#if UNITY_EDITOR
            _inspectorGuid = entity != null ? entity.Id.ToString() : string.Empty;
#endif
            SyncTransform();
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            // For a top-down 2D simulation, we don't want nodes to fall or rotate when bumping.
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        private void Update()
        {
            SyncTransform();
        }

        private void OnDestroy()
        {
            if (_entity != null && s_views.TryGetValue(_entity.Id, out var view) && view == this) s_views.Remove(_entity.Id);
        }

        private void SyncTransform()
        {
            if (_entity == null || transform == null) return;
            float alpha = _clock != null ? _clock.InterpolationAlpha : 1f;
            transform.position = Vector2.Lerp(_entity.PreviousPosition.ToUnity(), _entity.Position.ToUnity(), alpha);
            transform.hasChanged = false;
        }
    }
}
