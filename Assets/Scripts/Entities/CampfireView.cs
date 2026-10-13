using UnityEngine;

namespace Simpiens.Entities
{
    /// <summary>
    /// Dumb view component for a GatheringAnchor (Campfire).
    /// Handles procedural 2-frame flame/ember sprite animation and gentle thermal pulsing.
    /// Zero simulation logic!
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CampfireView : MonoBehaviour
    {
        private SpriteRenderer _spriteRenderer;
        private Vector3 _baseScale;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            if (_baseScale == Vector3.zero) _baseScale = Vector3.one;

            _spriteRenderer.sprite = AgentSpriteLibrary.GetCampfireSprite(0);
            _spriteRenderer.sortingOrder = RenderOrder.Props;
        }

        private void Update()
        {
            if (_spriteRenderer == null) return;

            // 2-frame dancing flame animation at 6 Hz
            int frame = (int)(Time.time * 6f) & 1;
            _spriteRenderer.sprite = AgentSpriteLibrary.GetCampfireSprite(frame);

            // Subtle thermal respiration pulse
            float pulse = 1f + (Mathf.Sin(Time.time * 4f) * 0.03f);
            transform.localScale = _baseScale * pulse;
        }
    }
}
