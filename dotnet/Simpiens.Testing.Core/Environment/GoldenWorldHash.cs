using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Simpiens.Testing.Environment
{
    /// <summary>
    /// Golden world-hash baselines (Epic CORE C2). <see cref="WorldStateHash"/> samples of a session, per seed, kept in a
    /// text file in the repo. A change that must not alter behaviour proves it by reproducing them; Phase 1 compares the
    /// .NET runtime against the same file. A deliberate behaviour change re-records them in its own commit.
    /// Each sample is taken on its tick, after the simulation's tick and the agents' ticks.
    /// </summary>
    public static class GoldenWorldHash
    {
        /// <summary>The baseline file, relative to the repository root.</summary>
        public const string RelativePath = "Baselines/golden-world-hash.txt";

        public const long HorizonTicks = 3000;
        public const long SampleEveryTicks = 100;

        /// <summary>Set to 1 to record this run's samples instead of comparing them.</summary>
        public const string RecordVariable = "SIMPIENS_RECORD_GOLDEN";

        public static bool RecordRequested => global::System.Environment.GetEnvironmentVariable(RecordVariable) == "1";

        /// <summary>One seed's samples of one session.</summary>
        public sealed class Baseline
        {
            public ulong Seed;
            public string SessionId;
            public readonly List<WorldStateSample> Samples = new List<WorldStateSample>(32);
        }

        /// <summary>Every baseline in the file, by seed; empty when the file doesn't exist.</summary>
        public static Dictionary<ulong, Baseline> Read(string path)
        {
            var result = new Dictionary<ulong, Baseline>();
            if (!File.Exists(path)) return result;

            Baseline current = null;
            WorldStateSample sample = null;
            var lines = File.ReadAllLines(path);
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var f = line.Split(' ');
                switch (f[0])
                {
                    case "seed":
                        current = new Baseline { Seed = ulong.Parse(f[1], CultureInfo.InvariantCulture), SessionId = f[3] };
                        result[current.Seed] = current;
                        sample = null;
                        break;
                    case "t":
                        sample = new WorldStateSample
                        {
                            Tick = long.Parse(f[1], CultureInfo.InvariantCulture),
                            Total = Hex(f[3]),
                            Economy = Hex(f[5]),
                            Bounties = Hex(f[7]),
                            Map = Hex(f[9])
                        };
                        current?.Samples.Add(sample);
                        break;
                    case "a":
                        if (sample == null) break;
                        // a <16 hex digits> <name, may hold spaces> | <description>
                        int bar = line.IndexOf(" | ", StringComparison.Ordinal);
                        if (bar < 20) throw new FormatException($"{path}:{n + 1}: bad agent line '{line}'");
                        sample.AgentHashes.Add(Hex(line.Substring(2, 16)));
                        sample.AgentNames.Add(line.Substring(19, bar - 19));
                        sample.AgentLines.Add(line.Substring(bar + 3));
                        break;
                    case "end":
                        current = null;
                        sample = null;
                        break;
                    default:
                        throw new FormatException($"{path}:{n + 1}: unknown line '{line}'");
                }
            }
            return result;
        }

        /// <summary>Writes one seed's samples, keeping the other seeds' baselines (sorted by seed).</summary>
        public static void Write(string path, ulong seed, string sessionId, IReadOnlyList<WorldStateSample> samples)
        {
            var all = Read(path);
            var baseline = new Baseline { Seed = seed, SessionId = sessionId };
            for (int i = 0; i < samples.Count; i++) baseline.Samples.Add(samples[i]);
            all[seed] = baseline;

            var seeds = new List<ulong>(all.Keys);
            seeds.Sort();
            var sb = new StringBuilder(64 * 1024);
            sb.Append("# Simpiens golden world hashes (Epic CORE C2). Generated; do not edit by hand.\n");
            sb.Append("# Re-record only for a deliberate behaviour change, in its own commit: simpiens golden record --out Baselines/golden-world-hash.txt\n");
            sb.Append("# t <tick> total/economy/bounties/map <WorldStateHash parts>; a <agent hash> <name> | <description, for diagnosis only>\n");
            for (int s = 0; s < seeds.Count; s++)
            {
                var b = all[seeds[s]];
                sb.Append("seed ").Append(b.Seed.ToString(CultureInfo.InvariantCulture)).Append(" session ").Append(b.SessionId).Append('\n');
                for (int i = 0; i < b.Samples.Count; i++)
                {
                    var x = b.Samples[i];
                    sb.Append("t ").Append(x.Tick.ToString(CultureInfo.InvariantCulture))
                      .Append(" total ").Append(x.Total.ToString("x16"))
                      .Append(" economy ").Append(x.Economy.ToString("x16"))
                      .Append(" bounties ").Append(x.Bounties.ToString("x16"))
                      .Append(" map ").Append(x.Map.ToString("x16")).Append('\n');
                    for (int a = 0; a < x.AgentHashes.Count; a++)
                    {
                        sb.Append("  a ").Append(x.AgentHashes[a].ToString("x16")).Append(' ').Append(x.AgentNames[a])
                          .Append(" | ").Append(x.AgentLines[a]).Append('\n');
                    }
                }
                sb.Append("end\n");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
        }

        /// <summary>Where a run first differs from its baseline, or null when every sample matches.</summary>
        public static string Compare(Baseline baseline, string sessionId, IReadOnlyList<WorldStateSample> live, out long tick)
        {
            tick = -1;
            if (baseline == null) return "no baseline for this seed";
            if (baseline.SessionId != sessionId) return $"the baseline is of session '{baseline.SessionId}', the run of '{sessionId}'";

            int n = Math.Min(baseline.Samples.Count, live.Count);
            for (int i = 0; i < n; i++)
            {
                var expected = baseline.Samples[i];
                var actual = live[i];
                if (expected.Tick != actual.Tick) { tick = expected.Tick; return $"sample ticks differ (baseline t{expected.Tick}, run t{actual.Tick})"; }
                string what = expected.DescribeDifference(actual);
                if (what != null) { tick = expected.Tick; return $"baseline vs run: {what}"; }
            }
            return baseline.Samples.Count == live.Count ? null : $"sample counts differ (baseline {baseline.Samples.Count}, run {live.Count})";
        }

        private static ulong Hex(string text) => ulong.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
