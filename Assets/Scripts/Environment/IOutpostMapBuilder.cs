using System.Collections.Generic;
using UnityEngine;

namespace Simpiens.Environment
{
    /// <summary>
    /// Contract for procedural construction and lifecycle management of the utilitarian outpost environment.
    /// Provides deterministic generation of rectangular walls, doorways, and sector anchors.
    /// </summary>
    public interface IOutpostMapBuilder
    {
        void BuildMap();
        void ClearMap();
        int WallCount { get; }
        IReadOnlyList<Vector2> DoorwayPositions { get; }
    }
}
