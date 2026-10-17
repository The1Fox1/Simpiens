using Simpiens.Simulation;
using System;

namespace Simpiens.Cognition.Pathfinding
{
    public struct PathRequest
    {
        public Vec2 StartPosition;
        public Vec2 TargetPosition;
        public EntityId RequesterId;
    }
}
