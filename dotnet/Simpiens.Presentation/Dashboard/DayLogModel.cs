using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Life;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// A crawler's day, as the dossier's Today tab tells it (Epic LIFE-1, L1.5, D10; the epic's A10, F5): since midnight, the
    /// thoughts it began (its life as it felt it) and the chronicle's dramatic lines about it (a rescue, a tantrum, a
    /// technique), newest first. A thought begun on a line's tick stands for that line, so nothing is told twice. It stores
    /// nothing: the life record and the chronicle hold the day.
    /// </summary>
    public sealed class DayLogModel
    {
        /// <summary>One line of the day: when, and what.</summary>
        public readonly struct Line
        {
            public Line(long tick, string text)
            {
                Tick = tick;
                Text = text;
            }

            public long Tick { get; }
            public string Text { get; }

            /// <summary>"07:40 Slept in a bunk".</summary>
            public string Shown => LifeTexts.Clock(Tick) + "  " + Text;
        }

        /// <summary>The chronicle's lines are read from its newest this many about the crawler: enough for any day measured (L0.3: 44 a resident-day).</summary>
        public const int ChronicleWindow = 128;

        private readonly List<Line> _lines = new List<Line>(32);
        private readonly List<ChronicleEvent> _events = new List<ChronicleEvent>(ChronicleWindow);

        public bool HasCrawler { get; private set; }

        /// <summary>The day's lines, newest first.</summary>
        public IReadOnlyList<Line> Lines => _lines;

        /// <summary>"Nothing yet today." when the day holds nothing.</summary>
        public const string EmptyText = "Nothing yet today.";

        /// <summary>Reads <paramref name="agent"/>'s day up to <paramref name="tick"/> from its life record and <paramref name="chronicle"/>, in <paramref name="formatter"/>'s words.</summary>
        public void Refresh(AgentState agent, long tick, INarrativeChronicleService chronicle, INarrativeFormatter formatter, Func<EntityId, string> nameOf)
        {
            _lines.Clear();
            HasCrawler = agent != null;
            if (agent == null) return;
            long since = LifeTexts.DayStart(tick);

            var life = agent.Life;
            for (int i = 0; i < CrawlerLife.ThoughtCapacity; i++)
            {
                var slot = life.ThoughtAt(i);
                if (slot.Thought == 0 || slot.BegunAt < since || slot.BegunAt > tick) continue;
                _lines.Add(new Line(slot.BegunAt, LifeTexts.Thought(slot, life, nameOf)));
            }

            if (chronicle != null && formatter != null)
            {
                chronicle.GetActorEvents(agent.AgentId, _events, ChronicleWindow);
                for (int i = 0; i < _events.Count; i++)
                {
                    var e = _events[i];
                    if (e.SimulationTick < since || e.SimulationTick > tick || e.Intensity < ChronicleIntensity.Dramatic) continue;
                    if (ThoughtBegunAt(life, e.SimulationTick)) continue; // the thought tells it
                    string text = formatter.FormatEvent(e);
                    if (!string.IsNullOrEmpty(text)) _lines.Add(new Line(e.SimulationTick, text));
                }
            }
            _lines.Sort(NewestFirst.Instance);
        }

        private static bool ThoughtBegunAt(in CrawlerLife life, long tick)
        {
            for (int i = 0; i < CrawlerLife.ThoughtCapacity; i++)
            {
                var slot = life.ThoughtAt(i);
                if (slot.Thought != 0 && slot.BegunAt == tick) return true;
            }
            return false;
        }

        private sealed class NewestFirst : IComparer<Line>
        {
            public static readonly NewestFirst Instance = new NewestFirst();
            public int Compare(Line a, Line b) => b.Tick.CompareTo(a.Tick);
        }
    }
}
