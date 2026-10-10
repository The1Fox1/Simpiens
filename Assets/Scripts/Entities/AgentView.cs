using Simpiens.Cognition;
using UnityEngine;

namespace Simpiens.Entities
{
    /// <summary>
    /// Dumb view component responsible for rendering agent action-state sprites and procedural micro-animations.
    /// Strictly handles rendering and visual feedback without executing simulation or cognitive logic.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(AutonomousAgent))]
    public class AgentView : MonoBehaviour
    {
        private SpriteRenderer _spriteRenderer;
        private AutonomousAgent _agent;
        private Vector3 _baseScale = Vector3.one;

        /// <summary>
        /// Minimum logical ticks a displayed state is held before switching. Sub-tick simulation flashes
        /// (Thinking > Idle > Walking on instant intents) are real but unreadable; the view smooths them out.
        /// 2 ticks keeps at most 3 switches per 0.6 sim-seconds, below the thrashing threshold (4 per 0.5 s).
        /// </summary>
        public const int MinDisplayTicks = 2;

        /// <summary>The state currently rendered (after overrides and the display dwell).</summary>
        public AgentVisualState DisplayedState { get; private set; } = AgentVisualState.Idle;
        private long _displayedSinceTick = long.MinValue;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _agent = GetComponent<AutonomousAgent>();
            _baseScale = transform.localScale;
        }

        private void Start()
        {
            // Set initial sprite
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sprite = AgentSpriteLibrary.GetAgentSprite(AgentVisualState.Idle, 0);
            }
        }

        private void Update()
        {
            if (_agent == null || _spriteRenderer == null) return;

            // Determine active visual state
            var state = _agent.VisualState;

            // Tantrum / Starvation panic visual persistence: keep showing panicking during crisis
            if (_agent.IsInTantrum || _agent.IsStarvationPanicking)
            {
                state = AgentVisualState.Panicking;
            }
            // Yielding visual persistence: keep showing yielding for 1.2s after yielding
            else if (_agent.IsYielding)
            {
                state = AgentVisualState.Yielding;
            }
            // Sharing visual persistence: keep showing sharing for 1.2s during food transfer
            else if (_agent.IsSharing)
            {
                state = AgentVisualState.Sharing;
            }
            // Gossip visual persistence: keep showing gossip for 1.5s after exchange
            else if (_agent.IsGossiping)
            {
                state = AgentVisualState.Gossiping;
            }

            state = ApplyDisplayDwell(state);

            // Calculate animation frame
            float animSpeed = (state == AgentVisualState.Panicking) ? 8f : 4f;
            int frame = (int)(Time.time * animSpeed) & 1;

            // Update sprite if changed
            var targetSprite = AgentSpriteLibrary.GetAgentSprite(state, frame);
            if (_spriteRenderer.sprite != targetSprite)
            {
                _spriteRenderer.sprite = targetSprite;
            }

            // Apply zero-allocation procedural micro-animations
            ApplyMicroAnimation(state);
        }

        private AgentVisualState ApplyDisplayDwell(AgentVisualState desired)
        {
            if (desired == DisplayedState) return desired;

            long tick = _agent.CurrentSimTick;
            if (tick >= 0 && _displayedSinceTick != long.MinValue && tick - _displayedSinceTick < MinDisplayTicks)
            {
                return DisplayedState; // hold the current state until the dwell elapses
            }

            DisplayedState = desired;
            _displayedSinceTick = tick;
            return desired;
        }

        private void ApplyMicroAnimation(AgentVisualState state)
        {
            float t = Time.time;

            switch (state)
            {
                case AgentVisualState.Walking:
                    // Walking bob and slight body tilt
                    float walkTilt = Mathf.Sin(t * 10f) * 3f;
                    transform.localRotation = Quaternion.Euler(0f, 0f, walkTilt);
                    transform.localScale = _baseScale;
                    break;

                case AgentVisualState.Gossiping:
                    // Gentle interaction bounce
                    float gossipPulse = 1f + Mathf.Abs(Mathf.Sin(t * 5f)) * 0.08f;
                    transform.localScale = _baseScale * gossipPulse;
                    transform.localRotation = Quaternion.identity;
                    break;

                case AgentVisualState.Harvesting:
                    // Rhythmic gathering lean
                    float harvestLean = Mathf.Sin(t * 7f) * 6f;
                    transform.localRotation = Quaternion.Euler(0f, 0f, harvestLean);
                    transform.localScale = _baseScale;
                    break;

                case AgentVisualState.Panicking:
                    // Frantic tremor jitter
                    float panicTilt = Mathf.Sin(t * 22f) * 8f;
                    float jitterScale = 1f + (Mathf.Sin(t * 18f) * 0.1f);
                    transform.localRotation = Quaternion.Euler(0f, 0f, panicTilt);
                    transform.localScale = _baseScale * jitterScale;
                    break;

                case AgentVisualState.Thinking:
                    // Gentle thought float
                    float thinkSway = Mathf.Sin(t * 3f) * 2f;
                    transform.localRotation = Quaternion.Euler(0f, 0f, thinkSway);
                    transform.localScale = _baseScale;
                    break;

                case AgentVisualState.Resting:
                    // Snoozing breathing cycle and relaxed head slump
                    float restBreath = 1f + Mathf.Sin(t * 1.5f) * 0.04f;
                    transform.localScale = new Vector3(_baseScale.x * restBreath, _baseScale.y * (2f - restBreath), _baseScale.z);
                    transform.localRotation = Quaternion.Euler(0f, 0f, 2f);
                    break;

                case AgentVisualState.Yielding:
                    // Submissive cowering flinch: backwards body tilt and slight scale shrink
                    float yieldTilt = Mathf.Sin(t * 8f) * -5f;
                    float yieldSquash = 1f - (Mathf.Abs(Mathf.Sin(t * 6f)) * 0.08f);
                    transform.localRotation = Quaternion.Euler(0f, 0f, yieldTilt);
                    transform.localScale = new Vector3(_baseScale.x * yieldSquash, _baseScale.y * yieldSquash, _baseScale.z);
                    break;

                case AgentVisualState.Sharing:
                    // Compassionate offering: gentle forward tilt and soft pulsing scale
                    float sharePulse = 1f + (Mathf.Abs(Mathf.Sin(t * 6f)) * 0.08f);
                    transform.localRotation = Quaternion.Euler(0f, 0f, -3f);
                    transform.localScale = _baseScale * sharePulse;
                    break;

                case AgentVisualState.Rallying:
                    // Disciplined marching bob & upright posture
                    float rallyBob = 1f + Mathf.Abs(Mathf.Sin(t * 8f)) * 0.08f;
                    float rallyTilt = Mathf.Sin(t * 6f) * 2f;
                    transform.localRotation = Quaternion.Euler(0f, 0f, rallyTilt);
                    transform.localScale = _baseScale * rallyBob;
                    break;

                case AgentVisualState.Defiant:
                    // Rebellious rigid stance with defiant jitter
                    float defiantTilt = Mathf.Sin(t * 14f) * 4f;
                    float defiantScale = 1f + (Mathf.Sin(t * 12f) * 0.05f);
                    transform.localRotation = Quaternion.Euler(0f, 0f, defiantTilt);
                    transform.localScale = _baseScale * defiantScale;
                    break;

                case AgentVisualState.Idle:
                default:
                    // Calm rhythmic breathing
                    float breath = 1f + Mathf.Sin(t * 2f) * 0.03f;
                    transform.localScale = new Vector3(_baseScale.x * breath, _baseScale.y * (2f - breath), _baseScale.z);
                    transform.localRotation = Quaternion.identity;
                    break;
            }
        }
    }
}
