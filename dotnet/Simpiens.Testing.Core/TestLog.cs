using System;

namespace Simpiens.Testing
{
    /// <summary>
    /// Validator output, framework-free (Epic CORE C6): validators log through this and fail by throwing, so the Unity
    /// Test Framework and xUnit can both run them. Unity routes it to its console (TestHarnessInstaller); elsewhere it
    /// writes to the standard streams unless a test framework installs its own sink.
    /// </summary>
    public static class TestLog
    {
        public static Action<string> InfoSink { get; set; } = Console.WriteLine;
        public static Action<string> WarningSink { get; set; } = Console.WriteLine;
        public static Action<string> ErrorSink { get; set; } = Console.Error.WriteLine;

        public static void Log(object message) => InfoSink?.Invoke(message?.ToString());
        public static void LogWarning(object message) => WarningSink?.Invoke(message?.ToString());
        public static void LogError(object message) => ErrorSink?.Invoke(message?.ToString());
    }
}
