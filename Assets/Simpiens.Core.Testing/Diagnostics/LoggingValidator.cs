using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Simpiens.Diagnostics;
using ZLogger;

namespace Simpiens.Testing.Diagnostics
{
    /// <summary>
    /// Validates the SimLog/ZLogger integration: disabled logs cost zero allocations, per-channel levels filter,
    /// agent focus filters per-agent traces, and the file sink writes tick-stamped lines.
    /// Temporarily reconfigures the global logger and restores the previous configuration afterwards.
    /// </summary>
    public class LoggingValidator
    {
        private static uint _guidSeed = 47000;
        private static EntityId NextGuid() => new EntityId(_guidSeed++, 0x106A, 0x2002, 0xFEED);

        public void Start()
        {
            TestLog.Log("[LoggingValidator] Beginning SimLog / ZLogger validation...");
            var previous = SimLog.Settings;
            long previousTick = SimLog.CurrentTick;

            try
            {
                ValidateDisabledLogsAllocateNothing();
                ValidateChannelLevels();
                ValidateAgentFocus();
                ValidateFileSinkFormat();
                ValidateFormattingFailureIsContained();
            }
            finally
            {
                SimLog.CurrentTick = previousTick;
                if (previous != null) SimLog.Configure(previous);
                else SimLog.Shutdown();
            }

            TestLog.Log("<color=green>[LoggingValidator] ALL 5 LOGGING TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateDisabledLogsAllocateNothing()
        {
            SimLog.Configure(new SimLogSettings { DefaultLevel = LogLevel.Information });
            var agentId = NextGuid();
            var position = new Vec2(1.5f, -2f);

            // Warm up the call path
            SimLog.Planning.ZLogDebug($"warm-up {SimLog.Agent(agentId)} {position}");

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                SimLog.Planning.ZLogDebug($"disabled {i} {SimLog.Agent(agentId)} at {position} plan {ExpensiveDescription()}");
                SimLog.Navigation.ZLogTrace($"disabled trace {i} {position}");
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert(after - before == 0, $"Disabled Debug/Trace logs allocated {after - before} bytes over 2,000 calls.");
            Assert(_expensiveCalls == 0, "Interpolation holes were evaluated for disabled log calls.");
            TestLog.Log("[LoggingValidator] Test 1 Passed: disabled logs allocate nothing and skip hole evaluation.");
        }

        private int _expensiveCalls;

        private string ExpensiveDescription()
        {
            _expensiveCalls++;
            return "expensive";
        }

        private void ValidateChannelLevels()
        {
            var settings = new SimLogSettings();
            SimLogSettings.ApplySpec(settings, "Default=Warn;Planning=Debug;Economy=Info;Console=Error");
            SimLog.Configure(settings);

            Assert(SimLog.Planning.IsEnabled(LogLevel.Debug) == false, "Console floor (Error) suppresses Planning Debug when no file sink is configured.");

            settings.FilePath = TempLogPath("levels");
            settings.FileMinimumLevel = LogLevel.Trace;
            SimLog.Configure(settings);

            Assert(SimLog.Planning.IsEnabled(LogLevel.Debug), "Planning=Debug override enables Debug (file sink).");
            Assert(!SimLog.Planning.IsEnabled(LogLevel.Trace), "Planning=Debug override excludes Trace.");
            Assert(SimLog.Economy.IsEnabled(LogLevel.Information) && !SimLog.Economy.IsEnabled(LogLevel.Debug), "Economy=Info override respected.");
            Assert(!SimLog.Social.IsEnabled(LogLevel.Information) && SimLog.Social.IsEnabled(LogLevel.Warning), "Default=Warn applies to channels without overrides.");

            SimLog.Shutdown();
            TryDelete(settings.FilePath);
            TestLog.Log("[LoggingValidator] Test 2 Passed: per-channel levels and sink floors filter correctly.");
        }

        private void ValidateAgentFocus()
        {
            var settings = new SimLogSettings();
            settings.FocusAgentPrefixes.Add("Crawler");
            SimLog.Configure(settings);

            var crawler = NextGuid();
            var penAgent = NextGuid();
            var unnamed = NextGuid();
            SimLog.RegisterAgentName(crawler, "Crawler3");
            SimLog.RegisterAgentName(penAgent, "Pen3.Agent4");

            Assert(SimLog.Traces(crawler), "Focused agent is traced.");
            Assert(!SimLog.Traces(penAgent), "Agent outside focus is not traced.");
            Assert(!SimLog.Traces(unnamed), "Unnamed agent is not traced while a focus is set.");
            Assert(SimLog.Agent(crawler) == "Crawler3", "Agent label resolves registered name.");

            SimLog.Configure(new SimLogSettings());
            Assert(SimLog.Traces(penAgent), "Without a focus every agent is traced.");
            TestLog.Log("[LoggingValidator] Test 3 Passed: agent focus filter.");
        }

        private void ValidateFileSinkFormat()
        {
            var settings = new SimLogSettings { DefaultLevel = LogLevel.Information, FilePath = TempLogPath("format") };
            settings.ChannelLevels["Planning"] = LogLevel.Debug;
            SimLog.Configure(settings);

            var agentId = NextGuid();
            SimLog.RegisterAgentName(agentId, "Crawler9");
            SimLog.CurrentTick = 4242;
            SimLog.Planning.ZLogDebug($"{SimLog.Agent(agentId)} chose EarnIncome");
            SimLog.Planning.ZLogTrace($"this trace line must be filtered");
            SimLog.Shutdown(); // flushes

            string text = File.ReadAllText(settings.FilePath);
            Assert(text.Contains("[t4242] DBG Planning | Crawler9 chose EarnIncome"), $"File sink line has tick/level/channel prefix. Got: {text}");
            Assert(!text.Contains("must be filtered"), "Trace below the Planning=Debug override was not written.");

            TryDelete(settings.FilePath);
            TestLog.Log("[LoggingValidator] Test 4 Passed: file sink writes tick-stamped, channel-prefixed lines.");
        }

        private void ValidateFormattingFailureIsContained()
        {
            var settings = new SimLogSettings { DefaultLevel = LogLevel.Information, ConsoleMinimumLevel = LogLevel.Information, FilePath = TempLogPath("format-failure") };
            SimLog.Configure(settings);

            sbyte delta = 5;
            bool threw = false;
            try
            {
                // ZLogger's UTF-8 formatter rejects custom numeric formats; the sink must drop the line, not throw
                SimLog.Economy.ZLogInformation($"bad format {delta:+0;-0}");
            }
            catch (Exception)
            {
                threw = true;
            }

            SimLog.Shutdown();
            TryDelete(settings.FilePath);
            Assert(!threw, "A log line that fails to format propagated an exception into the calling code.");
            TestLog.Log("[LoggingValidator] Test 5 Passed: formatting failures are contained by the sink.");
        }

        private static string TempLogPath(string name)
        {
            return Path.Combine(Path.GetTempPath(), $"simpiens-logging-validator-{name}-{Guid.NewGuid():N}.log");
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
                // Best effort cleanup of a temp file
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                TestLog.LogError($"[LoggingValidator] FAILED: {message}");
                throw new Exception($"Assertion failed: {message}");
            }
        }
    }
}
