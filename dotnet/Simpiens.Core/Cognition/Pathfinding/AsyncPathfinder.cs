using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Simpiens.Simulation.Spatial;
using Simpiens.Diagnostics;
using ZLogger;

namespace Simpiens.Cognition.Pathfinding
{
    public interface IAsyncPathfinder
    {
        ValueTask<PathResponse> CalculatePathAsync(PathRequest request, SharedWorldSnapshot snapshot);
    }

    /// <summary>
    /// High-performance, background-threaded 2D Grid A* pathfinder.
    /// Operates entirely off the main thread with zero heap allocations in hot paths.
    /// </summary>
    public class AsyncPathfinder : IAsyncPathfinder
    {
        private readonly ConcurrentQueue<PathResponse> _responsePool = new ConcurrentQueue<PathResponse>();
        private readonly ConcurrentQueue<GridAStarSearcher> _searcherPool = new ConcurrentQueue<GridAStarSearcher>();

        private const int MaxPathLength = 200;
        private const int InitialPoolSize = 2000;
        private const int InitialSearcherPoolSize = 8;

        /// <summary>Fills the response and searcher pools up front (the composition root calls it once at boot).</summary>
        public void Start()
        {
            for (int i = 0; i < InitialPoolSize; i++)
            {
                var response = new PathResponse();
                response.Initialize(MaxPathLength, ReturnToPool);
                _responsePool.Enqueue(response);
            }

            for (int i = 0; i < InitialSearcherPoolSize; i++)
            {
                _searcherPool.Enqueue(new GridAStarSearcher());
            }
        }

        private void ReturnToPool(PathResponse response)
        {
            response.Clear();
            _responsePool.Enqueue(response);
        }

        /// <summary>
        /// Plans a path on the calling thread. Cognition already runs off the main thread (<see cref="CognitionScheduler"/>),
        /// so this no longer hops to the thread pool; the result is synchronous and the ValueTask allocates nothing.
        /// </summary>
        public ValueTask<PathResponse> CalculatePathAsync(PathRequest request, SharedWorldSnapshot snapshot)
        {
            if (snapshot == null)
            {
                var emptyResponse = GetOrCreateResponse();
                emptyResponse.Length = 0;
                emptyResponse.IsValid = false;
                emptyResponse.IsComplete = true;
                return new ValueTask<PathResponse>(emptyResponse);
            }

            // Retained for the search, as the caller may release its own hold meanwhile
            snapshot.Retain();

            try
            {
                var response = ProcessRequest(request, snapshot);
                if (!response.IsValid && SimLog.Traces(SimLog.Navigation, Microsoft.Extensions.Logging.LogLevel.Trace, request.RequesterId))
                {
                    SimLog.Navigation.ZLogTrace($"No path for {SimLog.Agent(request.RequesterId)} from {request.StartPosition} to {request.TargetPosition}");
                }
                return new ValueTask<PathResponse>(response);
            }
            finally
            {
                snapshot.Release();
            }
        }

        public PathResponse CalculatePathImmediate(PathRequest request, SharedWorldSnapshot snapshot)
        {
            if (snapshot == null)
            {
                var emptyResponse = GetOrCreateResponse();
                emptyResponse.Length = 0;
                emptyResponse.IsValid = false;
                emptyResponse.IsComplete = true;
                return emptyResponse;
            }

            return ProcessRequest(request, snapshot);
        }

        private PathResponse GetOrCreateResponse()
        {
            if (!_responsePool.TryDequeue(out var response))
            {
                response = new PathResponse();
                response.Initialize(MaxPathLength, ReturnToPool);
            }
            response.MarkRented();
            return response;
        }

        private PathResponse ProcessRequest(PathRequest request, SharedWorldSnapshot snapshot)
        {
            var response = GetOrCreateResponse();

            if (!_searcherPool.TryDequeue(out var searcher))
            {
                searcher = new GridAStarSearcher();
            }

            try
            {
                searcher.Search(in request, snapshot, response);
                return response;
            }
            finally
            {
                _searcherPool.Enqueue(searcher);
            }
        }
    }
}
