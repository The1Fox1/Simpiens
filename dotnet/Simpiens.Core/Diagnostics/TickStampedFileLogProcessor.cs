using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using ZLogger;

namespace Simpiens.Diagnostics
{
    /// <summary>
    /// Plain-text file sink for ZLogger that formats each entry synchronously on the logging thread.
    /// ZLogger's built-in file provider formats on a background writer, which would stamp lines with the
    /// simulation tick current at write time rather than at log time; this processor keeps the
    /// <see cref="SimLog.CurrentTick"/> prefix accurate. Writes are serialized with a lock into a buffered stream.
    /// </summary>
    internal sealed class TickStampedFileLogProcessor : IAsyncLogProcessor
    {
        private static readonly byte[] NewLine = { (byte)'\n' };

        [ThreadStatic]
        private static ArrayBufferWriter<byte> _buffer;

        private readonly IZLoggerFormatter _formatter;
        private readonly object _gate = new object();
        private Stream _stream;

        public TickStampedFileLogProcessor(string path, ZLoggerOptions options)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 64 * 1024);
            _formatter = options.CreateFormatter();
        }

        public void Post(IZLoggerEntry log)
        {
            try
            {
                var buffer = _buffer ??= new ArrayBufferWriter<byte>(256);
                buffer.Clear();
                _formatter.FormatLogEntry(buffer, log);

                bool flush = log.LogInfo.LogLevel >= Microsoft.Extensions.Logging.LogLevel.Warning;
                lock (_gate)
                {
                    if (_stream == null) return;
                    _stream.Write(buffer.WrittenSpan);
                    _stream.Write(NewLine, 0, 1);
                    if (flush) _stream.Flush();
                }
            }
            finally
            {
                log.Return();
            }
        }

        public ValueTask DisposeAsync()
        {
            lock (_gate)
            {
                _stream?.Flush();
                _stream?.Dispose();
                _stream = null;
            }
            return default;
        }
    }
}
