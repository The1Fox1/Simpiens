using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Simpiens.Diagnostics
{
    /// <summary>
    /// Wraps a ZLogger sink so that (1) the sink applies its own minimum level on top of the channel levels,
    /// and (2) a failure while formatting or writing a line (e.g. an unsupported format specifier) is reported
    /// instead of propagating into the simulation code that logged it. Logging must never alter control flow.
    /// </summary>
    internal sealed class SafeSinkLogProcessor : IAsyncLogProcessor
    {
        private readonly IAsyncLogProcessor _inner;
        private readonly LogLevel _floor;

        public SafeSinkLogProcessor(IAsyncLogProcessor inner, LogLevel floor)
        {
            _inner = inner;
            _floor = floor;
        }

        public void Post(IZLoggerEntry log)
        {
            if (log.LogInfo.LogLevel < _floor)
            {
                log.Return();
                return;
            }

            // Read before posting: inner processors return the pooled entry when they finish
            var category = log.LogInfo.Category.Name;
            try
            {
                _inner.Post(log);
            }
            catch (Exception ex)
            {
                // Inner processors return the entry in their own finally blocks
                SimLog.SinkFailure?.Invoke($"[SimLog] Dropped a {category} log line that failed to format: {ex.GetType().Name}: {ex.Message}");
            }
        }

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
