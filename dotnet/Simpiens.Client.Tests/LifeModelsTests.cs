using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Narrative;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation;
using Simpiens.Simulation.Life;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The dossier's Mind and Today, as models (Epic LIFE-1, L1.5, D10): a crawler's mood, band and reason, its terms and
    /// thoughts by size with their time left, and its day since midnight told from its thoughts and the chronicle's dramatic
    /// lines, newest first, nothing told twice.
    /// </summary>
    public sealed class LifeModelsTests
    {
        private static readonly EntityId Ama = new EntityId(1u, 0u, 0u, 1u), Bram = new EntityId(2u, 0u, 0u, 2u);
        private static string Names(EntityId id) => id == Bram ? "Bram" : id == Ama ? "Ama" : null;

        // Day 2's midnight: day 1 runs from 06:00 (tick 0) for 18 hours
        private static readonly long Midnight = 18L * DayClock.TicksPerHour;

        private static AgentState Crawler(in AgentNeeds needs, params (Thought Thought, EntityId Subject, uint Tick)[] thoughts)
        {
            var state = new AgentState(Ama) { Needs = needs };
            var life = default(CrawlerLife);
            foreach (var t in thoughts) ThoughtRules.Think(ref life, t.Thought, t.Subject, t.Tick);
            state.Life = life;
            return state;
        }

        [Fact]
        public void TheMindReadsMoodAndItsReasons()
        {
            long now = Midnight + 8 * DayClock.TicksPerHour;
            var state = Crawler(new AgentNeeds(30f, 80f, 60f),
                (Thought.SleptInABunk, default, (uint)(now - 60)), (Thought.FriendDied, Bram, (uint)(now - 30)), (Thought.AGoodTalk, default, (uint)(now - 20)), (Thought.AGoodTalk, default, (uint)(now - 10)));
            var mind = new MindModel();
            mind.Refresh(state, now, Names);
            Assert.True(mind.HasCrawler);
            Assert.Equal(-15, mind.Mood); // comfortable 4, a bunk 3, two talks 3, Bram's death -25
            Assert.Equal(MoodBand.Low, mind.Band);
            Assert.Equal("Low (-15)", mind.MoodText);
            Assert.Equal("Bram died", mind.Reason);
            Assert.Equal("Bram died", mind.ReasonInLine); // a name keeps its capital
            Assert.Equal("Low: Bram died. Health 100%.", DossierModel.MoodText(Simpiens.Presentation.Art.People.Mood.Low, 100f, false, mind.ReasonInLine));
            Assert.Equal("Comfortable +4", mind.Lines[0].Text); // the need terms first
            Assert.Equal("Bram died -25 · 4 days left", mind.Lines[1].Text);
            Assert.Equal("A good talk ×2 +3 · 5 h left", mind.Lines[2].Text);
            Assert.Equal("Slept in a bunk +3 · 13 h left", mind.Lines[3].Text);
            Assert.Equal(4, mind.Lines.Count);

            var good = new MindModel();
            good.Refresh(Crawler(new AgentNeeds(30f, 80f, 60f), (Thought.RescuedBy, Bram, (uint)now), (Thought.AGoodMeal, default, (uint)now)), now, Names);
            Assert.Equal(MoodBand.Good, good.Band);
            Assert.Equal("Rescued by Bram", good.Reason);
            Assert.Equal("rescued by Bram", good.ReasonInLine);
            Assert.Equal("Happy: rescued by Bram. Health 100%.", DossierModel.MoodText(Simpiens.Presentation.Art.People.Mood.Happy, 100f, false, good.ReasonInLine));
            Assert.Equal("Hungry. Health 80%.", DossierModel.MoodText(Simpiens.Presentation.Art.People.Mood.Hungry, 80f, false, "hungry")); // the body's moods keep their words

            var hungry = new MindModel();
            hungry.Refresh(Crawler(new AgentNeeds(90f, 80f, 60f)), now, Names);
            Assert.Equal("Starving", hungry.Reason);
            Assert.Equal(MoodBand.Low, hungry.Band);

            // A want (Epic LIFE-1 plan 2, W.4): listed after the thoughts, with its time left, never the reason
            var wanting = Crawler(new AgentNeeds(30f, 80f, 60f));
            var life = wanting.Life;
            WantRules.Add(ref life, new WantSlot { Want = (ushort)Want.SeeAFriend, ExpiresAt = (uint)(now + 9 * DayClock.TicksPerHour), Friend = Bram });
            wanting.Life = life;
            var wants = new MindModel();
            wants.Refresh(wanting, now, Names);
            Assert.Equal("Wants: to see Bram · 9 h left", wants.Lines[wants.Lines.Count - 1].Text);
            Assert.NotEqual("to see Bram", wants.Reason);

            var none = new MindModel();
            none.Refresh(null, now, Names);
            Assert.False(none.HasCrawler);
            Assert.Empty(none.Lines);
        }

        [Fact]
        public void AThoughtNamesItsSubjectOrSomeone()
        {
            var life = default(CrawlerLife);
            ThoughtRules.Think(ref life, Thought.FriendDied, Bram, 10u);
            ThoughtRules.Think(ref life, Thought.RescuedBy, new EntityId(9u, 0u, 0u, 9u), 10u);
            Assert.Equal("Bram died", LifeTexts.Thought(life.ThoughtAt(0), life, Names));
            Assert.Equal("Rescued by someone", LifeTexts.Thought(life.ThoughtAt(1), life, Names));
            Assert.Equal("Someone died", LifeTexts.Thought(life.ThoughtAt(0), life, null));
            Assert.Equal("2 days left", LifeTexts.Left(50L * DayClock.TicksPerHour));
            Assert.Equal("5 h left", LifeTexts.Left(5L * DayClock.TicksPerHour + 10));
            Assert.Equal("40 min left", LifeTexts.Left(40L * DayClock.TicksPerMinute));
            Assert.Equal("07:40", LifeTexts.Clock(100L * DayClock.TicksPerMinute));
            Assert.Equal(0L, LifeTexts.DayStart(5L * DayClock.TicksPerHour));
            Assert.Equal(Midnight, LifeTexts.DayStart(Midnight + 3L * DayClock.TicksPerHour));
        }

        [Fact]
        public void TodayTellsTheDayNewestFirst()
        {
            long now = Midnight + 12 * DayClock.TicksPerHour; // 12:00 on day 2
            uint woke = (uint)(Midnight + 7 * DayClock.TicksPerHour), ate = (uint)(Midnight + 8 * DayClock.TicksPerHour), rescued = (uint)(Midnight + 10 * DayClock.TicksPerHour);
            var state = Crawler(new AgentNeeds(30f, 80f, 60f),
                (Thought.AGoodTalk, default, (uint)(Midnight - 60)), // yesterday evening: not today
                (Thought.SleptInABunk, default, woke), (Thought.AGoodMeal, default, ate), (Thought.RescuedBy, Bram, rescued));
            var chronicle = new NarrativeChronicleService();
            uint tantrum = (uint)(Midnight + 9 * DayClock.TicksPerHour);
            chronicle.RecordEvent(new ChronicleEvent(ChronicleEventType.TantrumStarted, Ama, default, 0, tantrum, ChronicleIntensity.Dramatic));
            chronicle.RecordEvent(new ChronicleEvent(ChronicleEventType.CrawlerRescued, Bram, Ama, 0, rescued, ChronicleIntensity.Dramatic, 1f)); // the thought tells it
            chronicle.RecordEvent(new ChronicleEvent(ChronicleEventType.CampfireGossip, Ama, Bram, 0, tantrum + 5, ChronicleIntensity.Notable)); // routine: not told
            chronicle.RecordEvent(new ChronicleEvent(ChronicleEventType.TantrumStarted, Ama, default, 0, (uint)(Midnight - 100), ChronicleIntensity.Dramatic)); // yesterday
            var today = new DayLogModel();
            today.Refresh(state, now, chronicle, new Words(), Names);
            Assert.True(today.HasCrawler);
            Assert.Equal(new[] { "10:00  Rescued by Bram", "09:00  TantrumStarted", "08:00  A good meal", "07:00  Slept in a bunk" }, Shown(today));
        }

        private static List<string> Shown(DayLogModel today)
        {
            var shown = new List<string>();
            foreach (var line in today.Lines) shown.Add(line.Shown);
            return shown;
        }

        /// <summary>The chronicle's words, by event type: enough to tell which lines are told.</summary>
        private sealed class Words : INarrativeFormatter
        {
            public string FormatEvent(in ChronicleEvent evt) => evt.EventType.ToString();
        }
    }
}
