using System;
using System.Threading;

namespace Simpiens.Cognition.Pathfinding
{
    public class PathResponse
    {
        public Vec2[] Waypoints;
        public int Length;
        public bool IsValid;
        public bool IsComplete;
        
        private Action<PathResponse> _returnAction;
        private int _isRented;

        public bool IsRented => _isRented == 1;

        public void Initialize(int maxPathLength, Action<PathResponse> returnAction)
        {
            if (Waypoints == null || Waypoints.Length != maxPathLength)
            {
                Waypoints = new Vec2[maxPathLength];
            }
            _returnAction = returnAction;
            _isRented = 0;
            Clear();
        }

        public void MarkRented()
        {
            _isRented = 1;
        }

        public void Clear()
        {
            Length = 0;
            IsValid = false;
            IsComplete = false;
        }

        public void ReturnToPool()
        {
            if (_returnAction != null && Interlocked.Exchange(ref _isRented, 0) == 1)
            {
                _returnAction(this);
            }
        }
    }
}
