using Simpiens.Diagnostics;
using ZLogger.Unity;

namespace Simpiens.Core.Bootstrapping
{
    /// <summary>
    /// Unity's sinks for the engine-free <see cref="SimLog"/> (Epic CORE C6): its Debug console, and console warnings for
    /// dropped lines. Installed by <see cref="AppBootstrapper"/> before it configures logging.
    /// </summary>
    public static class UnitySimLogSinks
    {
        public static void Install()
        {
            SimLog.ConsoleSinkFactory = configure =>
            {
                var options = new ZLoggerUnityDebugOptions { PrettyStacktrace = false };
                configure(options);
                return new UnityDebugLogProcessor(options);
            };
            SimLog.SinkFailure = message => UnityEngine.Debug.LogWarning(message);
        }
    }
}
