using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using VContainer;

namespace Simpiens.Testing.Diagnostics
{
    /// <summary>
    /// Dumb view MonoBehaviour responsible for rendering diagnostic Gizmos in the Scene View
    /// and capturing rate-limited screenshots to disk upon anomaly detection.
    /// Strictly handles rendering and I/O feedback without executing simulation logic.
    /// </summary>
    public class VisualAnomalyRecorderView : MonoBehaviour
    {
        private struct GizmoIncident
        {
            public Vector2 Position;
            public VisualAnomalyType Type;
            public float ExpiryTime;
        }

        private IVisualAnomalyDetector _detector;
        private float _lastScreenshotTime = -10f;
        private const float ScreenshotCooldown = 1.5f;

        private readonly List<GizmoIncident> _activeGizmos = new List<GizmoIncident>(16);

        [Inject]
        public void Construct(IVisualAnomalyDetector detector)
        {
            _detector = detector;
            if (_detector != null)
            {
                _detector.OnAnomalyDetected += HandleAnomalyDetected;
            }
        }

        private void OnDestroy()
        {
            if (_detector != null)
            {
                _detector.OnAnomalyDetected -= HandleAnomalyDetected;
            }
        }

        private void HandleAnomalyDetected(VisualAnomalyRecord record)
        {
            // Register visual gizmo marker (active for 2.5s)
            _activeGizmos.Add(new GizmoIncident
            {
                Position = record.Position,
                Type = record.Type,
                ExpiryTime = Time.time + 2.5f
            });

            // Rate-limited screenshot capture
            if (Time.time - _lastScreenshotTime >= ScreenshotCooldown)
            {
                _lastScreenshotTime = Time.time;
                CaptureAnomalyScreenshot(record);
            }
        }

        private void CaptureAnomalyScreenshot(VisualAnomalyRecord record)
        {
            try
            {
                string directory = Path.Combine(Application.dataPath, "../Screenshots/Anomalies");
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string filename = $"Anomaly_{timestamp}_Tick{record.Tick}_{record.Type}.png";
                string fullPath = Path.Combine(directory, filename);

                ScreenCapture.CaptureScreenshot(fullPath);
                Debug.Log($"[VisualAnomalyRecorderView] Anomaly screenshot captured: {fullPath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VisualAnomalyRecorderView] Failed to capture anomaly screenshot: {ex.Message}");
            }
        }

        private void Update()
        {
            // Clean expired gizmo markers
            float currentTime = Time.time;
            for (int i = _activeGizmos.Count - 1; i >= 0; i--)
            {
                if (currentTime >= _activeGizmos[i].ExpiryTime)
                {
                    _activeGizmos.RemoveAt(i);
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (_activeGizmos.Count == 0) return;

            for (int i = 0; i < _activeGizmos.Count; i++)
            {
                var incident = _activeGizmos[i];
                switch (incident.Type)
                {
                    case VisualAnomalyType.OscillationJitter:
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawWireSphere(incident.Position, 0.6f);
                        break;

                    case VisualAnomalyType.PawnStacking:
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawWireSphere(incident.Position, 0.4f);
                        break;

                    case VisualAnomalyType.ObstaclePenetration:
                        Gizmos.color = Color.red;
                        Gizmos.DrawWireCube(incident.Position, new Vector3(0.8f, 0.8f, 0.8f));
                        break;

                    case VisualAnomalyType.VisualStateThrashing:
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawWireSphere(incident.Position, 0.5f);
                        break;

                    default:
                        Gizmos.color = Color.white;
                        Gizmos.DrawWireSphere(incident.Position, 0.3f);
                        break;
                }
            }
        }
    }
}
