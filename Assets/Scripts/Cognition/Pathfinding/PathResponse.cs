using UnityEngine;
using System.Collections.Concurrent;
using System;

namespace Simpiens.Cognition.Pathfinding
{
    public class PathResponse
    {
        public Vector2[] Waypoints;
        public int Length;
        public bool IsValid;
        public bool IsComplete;
        
        private Action<PathResponse> _returnAction;

        public void Initialize(int maxPathLength, Action<PathResponse> returnAction)
        {
            if (Waypoints == null || Waypoints.Length != maxPathLength)
            {
                Waypoints = new Vector2[maxPathLength];
            }
            _returnAction = returnAction;
            Clear();
        }

        public void Clear()
        {
            Length = 0;
            IsValid = false;
            IsComplete = false;
        }

        public void ReturnToPool()
        {
            if (_returnAction != null)
            {
                _returnAction(this);
            }
        }
    }
}
