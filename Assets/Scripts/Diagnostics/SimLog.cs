using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZLogger;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Simpiens.Diagnostics
{
    /// <summary>
    /// Project-wide logging façade over ZLogger (Microsoft.Extensions.Logging levels: Trace, Debug, Information,
    /// Warning, Error, Critical) with one logger per simulation channel.
    ///
    /// Usage rules (ROADMAP §1A.3):
    /// - Always log through the interpolated ZLogger API: <c>SimLog.Planning.ZLogDebug($"...{x}")</c>.
    ///   The handler checks IsEnabled first, so a disabled call evaluates no holes and allocates nothing.
    /// - Per-agent decision traces are additionally gated by <see cref="Traces"/> (agent focus filter).
    /// - Never build strings ahead of the call and never mutate simulation state from a log statement.
    /// - Until <see cref="Configure"/> runs (e.g. headless validators) every channel is a silent NullLogger.
    /// Lines are prefixed with the simulation tick at log time: <c>[t1234] DBG Planning | ...</c>.
    /// </summary>
    public static class SimLog
    {
        public const string CategoryPrefix = "Simpiens.";

        public static readonly string[] ChannelNames =
        {
            "Bootstrap", "Simulation", "Cognition", "Planning", "Economy", "Supply",
            "Navigation", "Social", "Groups", "Drives", "Intervention"
        };

        /// <summary>Session setup, configuration and spawn summaries.</summary>
        public static ILogger Bootstrap { get; private set; } = NullLogger.Instance;
        /// <summary>SimulationManager tick-level events: resource depletion/regrowth hand-off, intent lifecycle.</summary>
        public static ILogger Simulation { get; private set; } = NullLogger.Instance;
        /// <summary>Cognition tiers (reflex / GOAP / fallback) and intent outcomes.</summary>
        public static ILogger Cognition { get; private set; } = NullLogger.Instance;
        /// <summary>Goal selection, plan construction, plan advance/abort.</summary>
        public static ILogger Planning { get; private set; } = NullLogger.Instance;
        /// <summary>Trades, bounties, escrow and ledger.</summary>
        public static ILogger Economy { get; private set; } = NullLogger.Instance;
        /// <summary>Supply caravan and resource regrowth.</summary>
        public static ILogger Supply { get; private set; } = NullLogger.Instance;
        /// <summary>Pathfinding and motor watchdog.</summary>
        public static ILogger Navigation { get; private set; } = NullLogger.Instance;
        /// <summary>Gossip, contention, yielding, sharing, hearth bonding.</summary>
        public static ILogger Social { get; private set; } = NullLogger.Instance;
        /// <summary>Group formation, directives, compliance and succession.</summary>
        public static ILogger Groups { get; private set; } = NullLogger.Instance;
        /// <summary>Biological drives and mental breaks.</summary>
        public static ILogger Drives { get; private set; } = NullLogger.Instance;
        /// <summary>Player interventions and the bounty board.</summary>
        public static ILogger Intervention { get; private set; } = NullLogger.Instance;

        private static long _currentTick;

        /// <summary>Simulation tick stamped into every line; set by SimulationManager at the start of each tick.</summary>
        public static long CurrentTick
        {
            get => Interlocked.Read(ref _currentTick);
            set => Interlocked.Exchange(ref _currentTick, value);
        }

        public static bool IsConfigured => _factory != null;
        public static SimLogSettings Settings { get; private set; }

        private static readonly object ConfigureGate = new object();
        private static ILoggerFactory _factory;
        private static readonly ConcurrentDictionary<EntityId, string> AgentNames = new ConcurrentDictionary<EntityId, string>();
        private static readonly ConcurrentDictionary<EntityId, bool> FocusCache = new ConcurrentDictionary<EntityId, bool>();
        private static string[] _focusPrefixes = Array.Empty<string>();

        /// <summary>
        /// Builds the host's console sink, given the formatter every sink shares (Epic CORE C6: the engine-free core
        /// knows no console). Set by the host before <see cref="Configure"/>; Unity sets its Debug-console sink.
        /// </summary>
        public static Func<Action<ZLoggerOptions>, IAsyncLogProcessor> ConsoleSinkFactory { get; set; }

        /// <summary>Where a sink reports a line it had to drop (Unity: a console warning). Defaults to stderr.</summary>
        public static Action<string> SinkFailure { get; set; } = message => Console.Error.WriteLine(message);

        /// <summary>
        /// (Re)configures all channels. Safe to call again (e.g. a PlayMode scenario raising verbosity);
        /// the previous factory is flushed and disposed.
        /// </summary>
        public static void Configure(SimLogSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            lock (ConfigureGate)
            {
                var previous = _factory;

                bool hasFile = !string.IsNullOrWhiteSpace(settings.FilePath);
                LogLevel lowestSinkFloor = hasFile && settings.FileMinimumLevel < settings.ConsoleMinimumLevel
                    ? settings.FileMinimumLevel
                    : settings.ConsoleMinimumLevel;

                _factory = LoggerFactory.Create(builder =>
                {
                    builder.ClearProviders();

                    // Channel filters decide IsEnabled (and therefore whether a call costs anything at all);
                    // each sink then drops entries below its own floor inside SafeSinkLogProcessor.
                    builder.SetMinimumLevel(Max(settings.DefaultLevel, lowestSinkFloor));
                    for (int i = 0; i < ChannelNames.Length; i++)
                    {
                        builder.AddFilter(CategoryPrefix + ChannelNames[i], Max(settings.GetChannelLevel(ChannelNames[i]), lowestSinkFloor));
                    }

                    // The console is the host's (Unity's Debug console in the editor and player); none without one
                    var consoleSink = ConsoleSinkFactory?.Invoke(ConfigureFormatter);
                    if (consoleSink != null)
                    {
                        builder.AddZLoggerLogProcessor(new SafeSinkLogProcessor(consoleSink, settings.ConsoleMinimumLevel));
                    }

                    if (hasFile)
                    {
                        string path = settings.FilePath;
                        LogLevel fileFloor = settings.FileMinimumLevel;
                        builder.AddZLoggerLogProcessor(options =>
                        {
                            ConfigureFormatter(options);
                            return new SafeSinkLogProcessor(new TickStampedFileLogProcessor(path, options), fileFloor);
                        });
                    }
                });

                Bootstrap = _factory.CreateLogger(CategoryPrefix + "Bootstrap");
                Simulation = _factory.CreateLogger(CategoryPrefix + "Simulation");
                Cognition = _factory.CreateLogger(CategoryPrefix + "Cognition");
                Planning = _factory.CreateLogger(CategoryPrefix + "Planning");
                Economy = _factory.CreateLogger(CategoryPrefix + "Economy");
                Supply = _factory.CreateLogger(CategoryPrefix + "Supply");
                Navigation = _factory.CreateLogger(CategoryPrefix + "Navigation");
                Social = _factory.CreateLogger(CategoryPrefix + "Social");
                Groups = _factory.CreateLogger(CategoryPrefix + "Groups");
                Drives = _factory.CreateLogger(CategoryPrefix + "Drives");
                Intervention = _factory.CreateLogger(CategoryPrefix + "Intervention");

                _focusPrefixes = settings.FocusAgentPrefixes.ToArray();
                FocusCache.Clear();
                Settings = settings;

                previous?.Dispose();
            }

            Bootstrap.ZLogInformation($"SimLog configured: default={settings.DefaultLevel}, console>={settings.ConsoleMinimumLevel}, file={(string.IsNullOrWhiteSpace(settings.FilePath) ? "off" : settings.FilePath)}>={settings.FileMinimumLevel}, focus={(settings.FocusAgentPrefixes.Count == 0 ? "all" : string.Join(",", settings.FocusAgentPrefixes))}");
        }

        /// <summary>Flushes sinks and returns every channel to the silent NullLogger.</summary>
        public static void Shutdown()
        {
            lock (ConfigureGate)
            {
                var previous = _factory;
                _factory = null;
                Settings = null;

                Bootstrap = Simulation = Cognition = Planning = Economy = Supply = NullLogger.Instance;
                Navigation = Social = Groups = Drives = Intervention = NullLogger.Instance;

                previous?.Dispose();
            }
        }

        /// <summary>Registers a human-readable label (e.g. "Crawler2", "Pen3.RivalA") used in log lines and focus filtering.</summary>
        public static void RegisterAgentName(EntityId id, string name)
        {
            AgentNames[id] = name;
            FocusCache.TryRemove(id, out _);
        }

        public static void ClearAgentNames()
        {
            AgentNames.Clear();
            FocusCache.Clear();
        }

        /// <summary>
        /// Readable label for an entity. Only evaluated inside an enabled log hole, so unregistered ids
        /// (which format a short EntityId) never allocate when logging is off.
        /// </summary>
        public static string Agent(EntityId id)
        {
            if (id == default) return "-";
            if (AgentNames.TryGetValue(id, out var name)) return name;
            var label = id.ToString().Substring(0, 6);
            AgentNames.TryAdd(id, label);
            return label;
        }

        /// <summary>
        /// Agent focus filter for per-agent decision traces: true when no focus is configured or the agent's
        /// registered name starts with a focus prefix. Check the channel level first:
        /// <c>if (SimLog.Planning.IsEnabled(LogLevel.Debug) &amp;&amp; SimLog.Traces(id))</c>.
        /// </summary>
        public static bool Traces(EntityId agentId)
        {
            var prefixes = _focusPrefixes;
            if (prefixes.Length == 0) return true;
            if (FocusCache.TryGetValue(agentId, out var cached)) return cached;

            bool match = false;
            if (AgentNames.TryGetValue(agentId, out var name))
            {
                for (int i = 0; i < prefixes.Length; i++)
                {
                    if (name.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase))
                    {
                        match = true;
                        break;
                    }
                }
            }

            FocusCache[agentId] = match;
            return match;
        }

        /// <summary>Convenience: channel enabled at level AND agent in focus.</summary>
        public static bool Traces(ILogger channel, LogLevel level, EntityId agentId)
        {
            return channel.IsEnabled(level) && Traces(agentId);
        }

        private static void ConfigureFormatter(ZLoggerOptions options)
        {
            options.UsePlainTextFormatter(formatter =>
            {
                formatter.SetPrefixFormatter($"[t{0}] {1:short} {2} | ",
                    (in MessageTemplate template, in LogInfo info) => template.Format(CurrentTick, info.LogLevel, ShortCategory(info.Category.Name)));
                formatter.SetExceptionFormatter((writer, ex) => Utf8StringInterpolation.Utf8String.Format(writer, $"\n{ex}"));
            });
        }

        private static string ShortCategory(string category)
        {
            // Channel names are interned constants, so this is a lookup rather than an allocation
            for (int i = 0; i < ChannelNames.Length; i++)
            {
                if (category.Length == CategoryPrefix.Length + ChannelNames[i].Length && category.EndsWith(ChannelNames[i], StringComparison.Ordinal))
                {
                    return ChannelNames[i];
                }
            }
            return category;
        }

        private static LogLevel Max(LogLevel a, LogLevel b) => a > b ? a : b;
    }
}
