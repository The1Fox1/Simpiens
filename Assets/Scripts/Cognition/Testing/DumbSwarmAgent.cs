using System.Threading.Tasks;
using UnityEngine;
using Simpiens.Simulation.Spatial;
using Simpiens.Cognition.Pathfinding;

namespace Simpiens.Cognition.Testing
{
    public class DumbSwarmAgent : MonoBehaviour
    {
        private ISpatialPartition _spatialPartition;
        private IAsyncPathfinder _pathfinder;
        
        private PathResponse _currentPath;
        private int _currentWaypointIndex;
        private float _speed = 2.0f;
        
        public bool DrawPath { get; set; } = true;
        
        // Caching for zero-allocation
        private Vector2 _currentPos2D;
        private Vector2 _targetPos2D;

        public void Initialize(ISpatialPartition spatialPartition, IAsyncPathfinder pathfinder)
        {
            _spatialPartition = spatialPartition;
            _pathfinder = pathfinder;
            _currentPos2D = transform.position;
            
            // Initial request
            RequestNewPath();
        }

        private Task<PathResponse> _pendingPathTask;
        private float _retryTimer;

        private void RequestNewPath()
        {
            if (_spatialPartition == null || _pathfinder == null) return;

            var snapshot = _spatialPartition.GetActiveSnapshot();
            if (snapshot == null) return;
            
            // We pass ownership of the snapshot to the pathfinder, which will Retain/Release it internally.
            
            Vector2 target;
            // Pick a random walkable tile
            int rx, ry;
            int maxTries = 50;
            do
            {
                rx = Random.Range(0, snapshot.Width);
                ry = Random.Range(0, snapshot.Height);
                maxTries--;
            } while (!snapshot.IsWalkable(rx, ry) && maxTries > 0);
            
            float offsetX = (snapshot.Width * snapshot.CellSize) * 0.5f;
            float offsetY = (snapshot.Height * snapshot.CellSize) * 0.5f;
            
            target = new Vector2(
                (rx * snapshot.CellSize) + (snapshot.CellSize * 0.5f) - offsetX,
                (ry * snapshot.CellSize) + (snapshot.CellSize * 0.5f) - offsetY
            );

            var request = new PathRequest
            {
                StartPosition = _currentPos2D,
                TargetPosition = target
            };
            
            _pendingPathTask = _pathfinder.CalculatePathAsync(request, snapshot);
        }

        private void OnPathComplete(PathResponse response)
        {
            if (_currentPath != null)
            {
                _currentPath.ReturnToPool();
            }
            
            if (response.IsValid)
            {
                _currentPath = response;
                _currentWaypointIndex = 0;
            }
            else
            {
                response.ReturnToPool();
                // If invalid, try again later via ManualUpdate retry timer
                _retryTimer = 0.1f;
            }
        }

        public void ManualUpdate() // Called by SwarmSpawner to avoid 1000 Update() overhead
        {
            // Non-blocking polling for path resolution
            if (_pendingPathTask != null)
            {
                if (_pendingPathTask.IsCompleted)
                {
                    if (_pendingPathTask.IsFaulted)
                    {
                        Debug.LogError(_pendingPathTask.Exception);
                        _pendingPathTask = null;
                        _retryTimer = 0.5f; // Wait before retry on failure
                    }
                    else
                    {
                        var response = _pendingPathTask.Result;
                        _pendingPathTask = null;
                        OnPathComplete(response);
                    }
                }
                return; // wait until path is resolved
            }

            if (_retryTimer > 0)
            {
                _retryTimer -= Time.deltaTime;
                if (_retryTimer <= 0)
                {
                    RequestNewPath();
                }
                return;
            }

            if (_currentPath == null || !_currentPath.IsValid)
            {
                RequestNewPath();
                return;
            }

            _currentPos2D = transform.position;
            _targetPos2D = _currentPath.Waypoints[_currentWaypointIndex];

            // Area 2: Strict boundary validation against grid before moving
            var snapshot = _spatialPartition.GetActiveSnapshot();
            if (snapshot != null)
            {
                float offsetX = (snapshot.Width * snapshot.CellSize) * 0.5f;
                float offsetY = (snapshot.Height * snapshot.CellSize) * 0.5f;
                int nextCellX = Mathf.FloorToInt((_targetPos2D.x + offsetX) / snapshot.CellSize);
                int nextCellY = Mathf.FloorToInt((_targetPos2D.y + offsetY) / snapshot.CellSize);
                
                if (!snapshot.IsWalkable(nextCellX, nextCellY))
                {
                    _currentPath.ReturnToPool();
                    _currentPath = null;
                    RequestNewPath();
                    return;
                }
            }
            
            float dist = Vector2.Distance(_currentPos2D, _targetPos2D);
            if (dist < 0.05f)
            {
                _currentWaypointIndex++;
                if (_currentWaypointIndex >= _currentPath.Length)
                {
                    // Path complete!
                    _currentPath.ReturnToPool();
                    _currentPath = null;
                    RequestNewPath();
                    return;
                }
                _targetPos2D = _currentPath.Waypoints[_currentWaypointIndex];
            }

            // Interpolate
            Vector2 dir = (_targetPos2D - _currentPos2D).normalized;
            transform.position = _currentPos2D + (dir * (_speed * Time.deltaTime));
            
            // Path Visualization (Task 2)
            if (DrawPath)
            {
                DrawCurrentPath();
            }
        }
        
        public void DrawCurrentPath()
        {
            if (_currentPath == null || !_currentPath.IsValid) return;
            
            Vector3 start = transform.position;
            for (int i = _currentWaypointIndex; i < _currentPath.Length; i++)
            {
                Vector3 end = _currentPath.Waypoints[i];
                Debug.DrawLine(start, end, Color.yellow);
                start = end;
            }
        }

        private void OnDestroy()
        {
            if (_currentPath != null)
            {
                _currentPath.ReturnToPool();
                _currentPath = null;
            }
        }
    }
}
