using System;
using System.Collections.Generic;
using System.Globalization;

namespace Simpiens.Host
{
    /// <summary>The command line: a command, then <c>--name value</c> options and <c>--flag</c> switches.</summary>
    internal sealed class Options
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Command { get; private set; } = "help";
        public string Verb { get; private set; }

        public static Options Parse(string[] args)
        {
            var options = new Options();
            int i = 0;
            if (args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal)) options.Command = args[i++].ToLowerInvariant();
            if (i < args.Length && !args[i].StartsWith("--", StringComparison.Ordinal)) options.Verb = args[i++].ToLowerInvariant();
            for (; i < args.Length; i++)
            {
                // -simlog* pairs belong to SimLogSettings, which reads them from the process's command line
                if (args[i].StartsWith("-simlog", StringComparison.Ordinal)) { i++; continue; }
                if (!args[i].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Unexpected argument '{args[i]}'.");
                string name = args[i].Substring(2);
                bool hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
                options._values[name] = hasValue ? args[++i] : "true";
            }
            return options;
        }

        public string Text(string name, string fallback = null) => _values.TryGetValue(name, out var value) ? value : fallback;

        public bool Flag(string name) => _values.TryGetValue(name, out var value) && value != "false";

        public long Long(string name, long fallback) =>
            _values.TryGetValue(name, out var value) ? long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) : fallback;

        /// <summary>Seeds as <c>a,b,c</c> or a range <c>first..last</c>; <paramref name="fallback"/> when absent.</summary>
        public ulong[] Seeds(ulong[] fallback)
        {
            string text = Text("seeds") ?? Text("seed");
            if (text == null) return fallback;
            var seeds = new List<ulong>();
            foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int range = part.IndexOf("..", StringComparison.Ordinal);
                if (range < 0) { seeds.Add(ulong.Parse(part, CultureInfo.InvariantCulture)); continue; }
                ulong first = ulong.Parse(part.Substring(0, range), CultureInfo.InvariantCulture);
                ulong last = ulong.Parse(part.Substring(range + 2), CultureInfo.InvariantCulture);
                if (last < first || last - first > 10000) throw new ArgumentException($"Bad seed range '{part}'.");
                for (ulong s = first; s <= last; s++) seeds.Add(s);
            }
            return seeds.ToArray();
        }
    }
}
