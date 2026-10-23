using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using Simpiens.Diagnostics;
using ZLogger;

namespace Simpiens.Hosting
{
    /// <summary>
    /// The .NET hosts' console sink for <see cref="SimLog"/> (the <c>simpiens</c> console, the desktop game): standard
    /// error, formatted on the logging thread (so each line keeps the tick it was logged at), leaving standard output to
    /// the console's results.
    /// </summary>
    public sealed class ConsoleSimLogSink : IAsyncLogProcessor
    {
        [ThreadStatic]
        private static ArrayBufferWriter<byte> _buffer;

        private readonly IZLoggerFormatter _formatter;
        private readonly Stream _stream = Console.OpenStandardError();
        private readonly object _gate = new object();

        private ConsoleSimLogSink(ZLoggerOptions options) => _formatter = options.CreateFormatter();

        public static void Install()
        {
            SimLog.ConsoleSinkFactory = configure =>
            {
                var options = new ZLoggerOptions();
                configure(options);
                return new ConsoleSimLogSink(options);
            };
        }

        public void Post(IZLoggerEntry log)
        {
            try
            {
                var buffer = _buffer ??= new ArrayBufferWriter<byte>(256);
                buffer.Clear();
                _formatter.FormatLogEntry(buffer, log);
                buffer.Write("\n"u8);
                lock (_gate) _stream.Write(buffer.WrittenSpan);
            }
            finally
            {
                log.Return();
            }
        }

        public ValueTask DisposeAsync()
        {
            lock (_gate) _stream.Flush();
            return default;
        }
    }
}
