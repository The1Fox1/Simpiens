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

            // Gossip visual persistence: keep showing gossip for 1.5s after exchange
            if (Time.time < _agent.LastGossipTimestamp + 1.5f)
            {
                state = AgentVisualState.Gossiping;
            }

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
