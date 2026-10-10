using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Simpiens.Diagnostics
{
    /// <summary>
    /// Logging configuration for <see cref="SimLog"/>.
    /// Channel levels decide what is produced at all; each sink then applies its own floor on top.
    /// Sources, later wins: defaults &lt; SIMPIENS_LOG* environment variables &lt; -simlog* command-line args &lt; code.
    ///
    /// Spec syntax (env SIMPIENS_LOG / arg -simlog): "Default=Info;Planning=Debug;Economy=Trace".
    /// File sink: SIMPIENS_LOG_FILE / -simlog-file &lt;path&gt;, level via SIMPIENS_LOG_FILE_LEVEL / -simlog-file-level.
    /// Agent focus: SIMPIENS_LOG_FOCUS / -simlog-focus "Crawler,Pen3" (agent-name prefixes).
    /// </summary>
    public sealed class SimLogSettings
    {
        /// <summary>Level for channels without an override.</summary>
        public LogLevel DefaultLevel = LogLevel.Information;

        /// <summary>Per-channel overrides, keyed by channel name (e.g. "Planning").</summary>
        public readonly Dictionary<string, LogLevel> ChannelLevels = new Dictionary<string, LogLevel>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Floor for the Unity console sink. Keeps high-volume Debug/Trace out of the console by default.</summary>
        public LogLevel ConsoleMinimumLevel = LogLevel.Information;

        /// <summary>Optional plain-text file sink (relative paths resolve against the project/player working directory).</summary>
        public string FilePath;

        public LogLevel FileMinimumLevel = LogLevel.Trace;

        /// <summary>When non-empty, per-agent decision traces are limited to agents whose registered name starts with one of these.</summary>
        public readonly List<string> FocusAgentPrefixes = new List<string>();

        public SimLogSettings WithChannel(string channel, LogLevel level)
        {
            ChannelLevels[channel] = level;
            return this;
        }

        public LogLevel GetChannelLevel(string channel)
        {
            return ChannelLevels.TryGetValue(channel, out var level) ? level : DefaultLevel;
        }

        /// <summary>
        /// Defaults overlaid with SIMPIENS_LOG* environment variables and -simlog* command-line arguments.
        /// </summary>
        public static SimLogSettings FromEnvironment()
        {
            var settings = new SimLogSettings();

            ApplySpec(settings, System.Environment.GetEnvironmentVariable("SIMPIENS_LOG"));
            ApplyFile(settings, System.Environment.GetEnvironmentVariable("SIMPIENS_LOG_FILE"), System.Environment.GetEnvironmentVariable("SIMPIENS_LOG_FILE_LEVEL"));
            ApplyFocus(settings, System.Environment.GetEnvironmentVariable("SIMPIENS_LOG_FOCUS"));

            string[] args;
            try
            {
                args = System.Environment.GetCommandLineArgs();
            }
            catch (NotSupportedException)
            {
                args = Array.Empty<string>();
            }

            for (int i = 0; i < args.Length - 1; i++)
            {
                switch (args[i])
                {
                    case "-simlog": ApplySpec(settings, args[i + 1]); break;
                    case "-simlog-file": ApplyFile(settings, args[i + 1], null); break;
                    case "-simlog-file-level": ApplyFile(settings, null, args[i + 1]); break;
                    case "-simlog-focus": ApplyFocus(settings, args[i + 1]); break;
                }
            }

            return settings;
        }

        /// <summary>Parses "Default=Info;Planning=Debug;Console=Debug" into the settings.</summary>
        public static void ApplySpec(SimLogSettings settings, string spec)
        {
            if (string.IsNullOrWhiteSpace(spec)) return;

            var entries = spec.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < entries.Length; i++)
            {
                var pair = entries[i].Split('=');
                if (pair.Length != 2 || !TryParseLevel(pair[1], out var level)) continue;

                string key = pair[0].Trim();
                if (key.Equals("Default", StringComparison.OrdinalIgnoreCase)) settings.DefaultLevel = level;
                else if (key.Equals("Console", StringComparison.OrdinalIgnoreCase)) settings.ConsoleMinimumLevel = level;
                else settings.ChannelLevels[key] = level;
            }
        }

        private static void ApplyFile(SimLogSettings settings, string path, string level)
        {
            if (!string.IsNullOrWhiteSpace(path)) settings.FilePath = path.Trim();
            if (TryParseLevel(level, out var parsed)) settings.FileMinimumLevel = parsed;
        }

        private static void ApplyFocus(SimLogSettings settings, string focus)
        {
            if (string.IsNullOrWhiteSpace(focus)) return;
            settings.FocusAgentPrefixes.Clear();
            var prefixes = focus.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < prefixes.Length; i++) settings.FocusAgentPrefixes.Add(prefixes[i].Trim());
        }

        /// <summary>Accepts Microsoft.Extensions.Logging names plus the short forms Info/Warn/Err/Off.</summary>
        public static bool TryParseLevel(string text, out LogLevel level)
        {
            level = LogLevel.Information;
            if (string.IsNullOrWhiteSpace(text)) return false;

            switch (text.Trim().ToLowerInvariant())
            {
                case "trace": case "verbose": level = LogLevel.Trace; return true;
                case "debug": level = LogLevel.Debug; return true;
                case "info": case "information": level = LogLevel.Information; return true;
                case "warn": case "warning": level = LogLevel.Warning; return true;
                case "err": case "error": level = LogLevel.Error; return true;
                case "critical": case "fatal": level = LogLevel.Critical; return true;
                case "none": case "off": level = LogLevel.None; return true;
                default: return false;
            }
        }
    }
}
