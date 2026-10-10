using UnityEngine;

namespace Simpiens.Testing.Diagnostics
{
    /// <summary>
    /// Lightweight, zero-heap record of a detected visual, motor, or physical anomaly.
    /// </summary>
    public readonly struct VisualAnomalyRecord
    {
        public readonly long Tick;
        public readonly float Timestamp;
        public readonly VisualAnomalyType Type;
        public readonly GUID PrimaryAgentId;
        public readonly GUID SecondaryAgentId;
        public readonly Vector2 Position;
        public readonly string Description;
        public readonly string ScreenshotPath;

        public VisualAnomalyRecord(
            long tick,
            float timestamp,
            VisualAnomalyType type,
            GUID primaryAgentId,
            GUID secondaryAgentId,
            Vector2 position,
            string description,
            string screenshotPath = null)
        {
            Tick = tick;
            Timestamp = timestamp;
            Type = type;
            PrimaryAgentId = primaryAgentId;
            SecondaryAgentId = secondaryAgentId;
            Position = position;
            Description = description;
            ScreenshotPath = screenshotPath;
        }

        public override string ToString()
        {
            string partnerInfo = !SecondaryAgentId.Empty() ? $" <=> Partner: {SecondaryAgentId}" : string.Empty;
            return $"[VISUAL_ANOMALY: {Type}] Tick {Tick} ({Timestamp:F2}s) Agent: {PrimaryAgentId}{partnerInfo} at ({Position.x:F2}, {Position.y:F2}) - {Description}";
        }
    }
}
