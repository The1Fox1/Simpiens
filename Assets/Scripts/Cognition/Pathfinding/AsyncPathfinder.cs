using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Simpiens.Cognition.Pathfinding
{
    public interface IAsyncPathfinder
    {
        Task<PathResponse> CalculatePathAsync(PathRequest request, SharedWorldSnapshot snapshot);
    }

    /// <summary>
    /// A naive, background-threaded pathfinder that queries the SharedWorldSnapshot.
    /// Operates entirely off the main thread.
    /// </summary>
    public class AsyncPathfinder : IAsyncPathfinder, IStartable
    {
        private readonly ConcurrentQueue<PathResponse> _responsePool = new ConcurrentQueue<PathResponse>();

        private const int MaxPathLength = 200;
        private const int InitialPoolSize = 2000;

        public void Start()
        {
            for (int i = 0; i < InitialPoolSize; i++)
            {
                var response = new PathResponse();
                response.Initialize(MaxPathLength, ReturnToPool);
                _responsePool.Enqueue(response);
            }
        }

        private void ReturnToPool(PathResponse response)
        {
            response.Clear();
            _responsePool.Enqueue(response);
        }

        public Task<PathResponse> CalculatePathAsync(PathRequest request, SharedWorldSnapshot snapshot)
        {
            // Retain snapshot before sending to worker thread to ensure it stays alive.
            snapshot.Retain();

            // Task.Run explicitly schedules the heavy array iteration onto a ThreadPool worker thread.
            return Task.Run(() =>
            {
                try
                {
                    return ProcessRequest(request, snapshot);
                }
                finally
                {
                    // Safe release once the background work is completed.
                    snapshot.Release();
                }
            });
        }

        private PathResponse ProcessRequest(PathRequest request, SharedWorldSnapshot snapshot)
        {
            if (!_responsePool.TryDequeue(out var response))
            {
                response = new PathResponse();
                response.Initialize(MaxPathLength, ReturnToPool);
            }

            // A VERY naive "straight line" or "random walk" pathfinder for stress testing.
            // A real A* would allocate too much unless heavily optimized.
            // Since this is for validation testing, we'll generate a direct path, 
            // checking IsWalkable along the way. If blocked, we stop.

            Vector2 currentPos = request.StartPosition;
            Vector2 targetPos = request.TargetPosition;
            Vector2 direction = (targetPos - currentPos).normalized;

            float distance = Vector2.Distance(currentPos, targetPos);
            float stepSize = snapshot.CellSize;

            int steps = Mathf.Min(Mathf.CeilToInt(distance / stepSize), MaxPathLength);

            response.Length = 0;

            for (int i = 1; i <= steps; i++) // Start from 1 to avoid adding start position
            {
                Vector2 nextPos = currentPos + direction * (i * stepSize);

                // Convert to grid coords
                float offsetX = (snapshot.Width * snapshot.CellSize) * 0.5f;
                float offsetY = (snapshot.Height * snapshot.CellSize) * 0.5f;

                int cellX = Mathf.FloorToInt((nextPos.x + offsetX) / snapshot.CellSize);
                int cellY = Mathf.FloorToInt((nextPos.y + offsetY) / snapshot.CellSize);

                // Stricter validation
                if (cellX < 0 || cellX >= snapshot.Width || cellY < 0 || cellY >= snapshot.Height)
                {
                    break; // Out of bounds
                }

                if (snapshot.IsWalkable(cellX, cellY))
                {
                    response.Waypoints[response.Length] = nextPos;
                    response.Length++;
                }
                else
                {
                    // Blocked! We hit a wall or resource.
                    break;
                }
            }

            // If we generated at least one step, it's valid
            response.IsValid = response.Length > 0;
            response.IsComplete = true;

            return response;
        }
    }
}
