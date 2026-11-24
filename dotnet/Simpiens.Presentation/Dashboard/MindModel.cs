using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Simulation;
using Simpiens.Simulation.Life;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// A crawler's mind, as the dossier's Mind tab shows it (Epic LIFE-1, L1.5, D10): its mood and band, the need terms and
    /// the live thoughts that make it, each with its value (and a thought with the time it has left), the largest first; and
    /// the reason its mood reads as it does. Reads the crawler; writes nothing.
    /// </summary>
    public sealed class MindModel
    {
        /// <summary>One line of the mind: its words, its value to mood, and for a thought the time it has left.</summary>
        public readonly struct Line
        {
            public Line(string words, float value, string left)
            {
                Words = words;
                Value = value;
                Left = left;
            }

            public string Words { get; }
            public float Value { get; }

            /// <summary>"2 days left"; empty for a need term.</summary>
            public string Left { get; }

            /// <summary>"Rescued by Bram +10 · 2 days left".</summary>
            public string Text => Left.Length > 0 ? Words + " " + LifeTexts.Value(Value) + " · " + Left : Words + " " + LifeTexts.Value(Value);
        }

        private readonly List<Line> _lines = new List<Line>(24);

        public bool HasCrawler { get; private set; }

        /// <summary>The mood, -100 to 100.</summary>
        public int Mood { get; private set; }

        public MoodBand Band { get; private set; }

        /// <summary>"Good (+12)".</summary>
        public string MoodText { get; private set; } = string.Empty;

        /// <summary>What weighs most in the mood's direction: "Rescued by Bram", "Ama died", "Hungry"; empty when nothing does.</summary>
        public string Reason { get; private set; } = string.Empty;

        /// <summary>The need terms, then the thoughts, each by size, the largest first.</summary>
        public IReadOnlyList<Line> Lines => _lines;

        /// <summary>Reads <paramref name="agent"/>'s mind at <paramref name="tick"/>, naming the crawlers its thoughts name by <paramref name="nameOf"/>.</summary>
        public void Refresh(AgentState agent, long tick, Func<EntityId, string> nameOf)
        {
            _lines.Clear();
            HasCrawler = agent != null;
            Mood = 0;
            Band = MoodBand.None;
            MoodText = Reason = string.Empty;
            if (agent == null) return;

            var life = agent.Life;
            Mood = MoodRules.Of(agent.Needs, agent.Health, agent.IsDowned, life, tick);
            Band = MoodRules.BandOf(Mood);
            MoodText = LifeTexts.BandWord(Band) + " (" + LifeTexts.Value(Mood) + ")";

            Span<MoodTerm> terms = stackalloc MoodTerm[4];
            int n = MoodRules.TermsOf(agent.Needs, agent.Health, agent.IsDowned, terms);
            int needLines = 0;
            for (int i = 0; i < n; i++)
            {
                _lines.Add(new Line(LifeTexts.Term(terms[i]), MoodRules.Term(terms[i]).Value, string.Empty));
                needLines++;
            }
            int firstThought = _lines.Count;
            for (int i = 0; i < CrawlerLife.ThoughtCapacity; i++)
            {
                var slot = life.ThoughtAt(i);
                if (!ThoughtRules.IsLive(slot, tick)) continue;
                _lines.Add(new Line(LifeTexts.Thought(slot, life, nameOf), ThoughtRules.Worth(slot, tick), LifeTexts.Left(ThoughtRules.EndsAt(slot) - tick)));
            }
            _lines.Sort(0, needLines, BySize.Instance);
            _lines.Sort(firstThought, _lines.Count - firstThought, BySize.Instance);
            Reason = ReasonOf(_lines, Band);
        }

        /// <summary>
        /// The line that weighs most in the mood's direction: the most negative for a low or breaking mood, the most positive for
        /// a good or happy one, the largest either way for a steady one. "Comfortable" is never a reason on its own.
        /// </summary>
        public static string ReasonOf(IReadOnlyList<Line> lines, MoodBand band)
        {
            int best = -1;
            float bestScore = 0f;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.Words == LifeTexts.Term(MoodTerm.Comfortable)) continue;
                float score = band <= MoodBand.Low && band != MoodBand.None ? -line.Value
                    : band >= MoodBand.Good ? line.Value
                    : Math.Abs(line.Value);
                if (score > bestScore) { bestScore = score; best = i; }
            }
            if (best < 0) return string.Empty;
            string words = lines[best].Words;
            int stacks = words.IndexOf(" ×", StringComparison.Ordinal);
            return stacks >= 0 ? words.Substring(0, stacks) : words;
        }

        private sealed class BySize : IComparer<Line>
        {
            public static readonly BySize Instance = new BySize();
            public int Compare(Line a, Line b)
            {
                int bySize = Math.Abs(b.Value).CompareTo(Math.Abs(a.Value));
                return bySize != 0 ? bySize : string.CompareOrdinal(a.Words, b.Words); // ties by their words, so the order never depends on the slots
            }
        }
    }
}
