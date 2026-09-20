using UnityEngine;

namespace Simpiens.Testing
{
    /// <summary>
    /// Headless/batch mode watcher that monitors log output for [Gossip] events
    /// and gracefully terminates the test run after gathering confirmation.
    /// </summary>
    public class BatchPlaymodeController : MonoBehaviour
    {
        private float _timer = 0f;
        private int _gossipDetected = 0;

        private void Awake()
        {
            Application.logMessageReceived += OnLogMessage;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (condition.Contains("[Gossip]"))
            {
                _gossipDetected++;
                Debug.Log($"[BatchPlaymodeController] >>> CONFIRMED GOSSIP EVENT #{_gossipDetected}: {condition} <<<");
            }
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            // Exit after capturing at least 3 gossip events, or timeout after 25 seconds
            if (_gossipDetected >= 3 || _timer > 25f)
            {
                Debug.Log($"[BatchPlaymodeController] Run complete. Total gossip events detected: {_gossipDetected} in {_timer:F2} seconds.");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
                UnityEditor.EditorApplication.Exit(0);
#else
                Application.Quit(0);
#endif
            }
        }
    }
}
