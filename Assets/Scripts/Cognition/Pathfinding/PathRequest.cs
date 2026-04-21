using UnityEngine;
using Simpiens.Simulation;
using System;

namespace Simpiens.Cognition.Pathfinding
{
    public struct PathRequest
    {
        public Vector2 StartPosition;
        public Vector2 TargetPosition;
        public GUID RequesterId;
    }
}
