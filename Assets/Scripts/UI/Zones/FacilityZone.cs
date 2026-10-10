using UnityEngine;

namespace Simpiens.UI.Zones
{
    /// <summary>
    /// Lightweight zero-allocation struct representing a designated sector or room within the facility.
    /// Maps continuous 2D coordinates to human-readable locations.
    /// </summary>
    public readonly struct FacilityZone
    {
        public readonly int ZoneId;
        public readonly string Name;
        public readonly string Description;
        public readonly Rect Bounds;
        public readonly Vector2 AnchorPosition;
        public readonly float AnchorRadius;
        public readonly bool IsExplored;

        public FacilityZone(int zoneId, string name, string description, Rect bounds, Vector2 anchorPosition = default, float anchorRadius = 0f, bool isExplored = true)
        {
            ZoneId = zoneId;
            Name = name;
            Description = description;
            Bounds = bounds;
            AnchorPosition = anchorPosition;
            AnchorRadius = anchorRadius;
            IsExplored = isExplored;
        }

        public FacilityZone WithExplored(bool explored)
        {
            return new FacilityZone(ZoneId, Name, Description, Bounds, AnchorPosition, AnchorRadius, explored);
        }

        public bool Contains(Vector2 pos)
        {
            if (AnchorRadius > 0f && Vector2.Distance(pos, AnchorPosition) <= AnchorRadius)
            {
                return true;
            }
            return Bounds.Contains(pos);
        }
    }
}
