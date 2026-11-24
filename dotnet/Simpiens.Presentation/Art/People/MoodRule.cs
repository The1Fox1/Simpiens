using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Entities;

namespace Simpiens.Presentation.Art.People
{
    /// <summary>What a crawler's face reads from it (Epic UI-Beta B3): its shown state, health and needs, as plain data.</summary>
    public readonly struct MoodSigns
    {
        public MoodSigns(AgentVisualState state, bool downed, bool tantrum, float health, float hunger, float energy, float frustration,
                         int lifeMood = 0, bool grieving = false)
        {
            LifeMood = lifeMood;
            Grieving = grieving;
            State = state;
            Downed = downed;
            Tantrum = tantrum;
            Health = health;
            Hunger = hunger;
            Energy = energy;
            Frustration = frustration;
        }

        public AgentVisualState State { get; }
        public bool Downed { get; }
        public bool Tantrum { get; }
        public float Health { get; }
        public float Hunger { get; }
        public float Energy { get; }
        public float Frustration { get; }

        /// <summary>Its mood in the life core's sense, -100 to 100 (Epic LIFE-1, L1.6): needs and thoughts (<see cref="Simpiens.Simulation.Life.MoodRules"/>).</summary>
        public int LifeMood { get; }

        /// <summary>Whether it holds a friend's death (L1.6).</summary>
        public bool Grieving { get; }

        /// <summary>The signs of <paramref name="agent"/> at <paramref name="tick"/>. Reads it; writes nothing.</summary>
        public static MoodSigns Of(AgentState agent, long tick)
            => new MoodSigns(agent.VisualState, agent.IsDowned, agent.IsInTantrum, agent.Health, agent.Hunger, agent.Energy, agent.Frustration,
                Simpiens.Simulation.Life.MoodRules.Of(agent.Needs, agent.Health, agent.IsDowned, agent.Life, tick),
                Simpiens.Simulation.Life.ThoughtRules.Holds(agent.Life, Simpiens.Simulation.Life.Thought.FriendDied, tick));
    }

    /// <summary>
    /// The mood a crawler's portrait shows (Epic UI-Beta B3, D1), by priority: asleep, hurt, frustrated (a tantrum),
    /// scared, grieving, frustrated, hungry, tired, low, happy, content (the life core's three since Epic LIFE-1, L1.6). Each threshold has a way back past it (hysteresis), so a need hovering
    /// at a threshold doesn't flicker the face; <see cref="MoodTracker"/> holds a mood a few seconds besides.
    /// </summary>
    public static class MoodRule
    {
        /// <summary>A mood shown stays at least this long (3 s at 10 ticks a second), unless the next is asleep or hurt.</summary>
        public const long HoldTicks = 30;

        public const float HurtBelow = 40f, HurtUntil = 50f;
        public const float FrustratedAt = 60f, FrustratedUntil = 50f;
        public const float HungryAt = 70f, HungryUntil = 60f;
        public const float TiredAt = 25f, TiredUntil = 35f;

        /// <summary>The life core's mood lines (L1.6): low from its band's top (-10) until 3 above it; happy from 30 until 3 below.</summary>
        public const int LowAt = Simpiens.Simulation.Life.MoodRules.LowAtMost, LowUntil = LowAt + 3;
        public const int HappyAt = Simpiens.Simulation.Life.MoodRules.HappyFrom, HappyUntil = HappyAt - 3;

        /// <summary>The mood <paramref name="signs"/> show, given the mood shown now (its threshold is crossed back further).</summary>
        public static Mood Of(in MoodSigns signs, Mood shown)
        {
            if (signs.State == AgentVisualState.Sleeping) return Mood.Asleep;
            if (signs.Downed || signs.Health < (shown == Mood.Hurt ? HurtUntil : HurtBelow)) return Mood.Hurt;
            if (signs.Tantrum || signs.State == AgentVisualState.Defiant) return Mood.Frustrated;
            if (signs.State == AgentVisualState.Panicking) return Mood.Scared;
            if (signs.Grieving) return Mood.Grieving;
            if (signs.Frustration >= (shown == Mood.Frustrated ? FrustratedUntil : FrustratedAt)) return Mood.Frustrated;
            if (signs.Hunger >= (shown == Mood.Hungry ? HungryUntil : HungryAt)) return Mood.Hungry;
            if (signs.Energy <= (shown == Mood.Tired ? TiredUntil : TiredAt)) return Mood.Tired;
            if (signs.LifeMood <= (shown == Mood.Low ? LowUntil : LowAt)) return Mood.Low;
            if (signs.LifeMood >= (shown == Mood.Happy ? HappyUntil : HappyAt)) return Mood.Happy;
            return Mood.Content;
        }
    }

    /// <summary>
    /// Each crawler's shown mood over time (Epic UI-Beta B3, D1): the rule's mood, held at least
    /// <see cref="MoodRule.HoldTicks"/> once shown unless the next is asleep or hurt, which show at once. Ticks, not
    /// frames, so the same run shows the same faces. Allocates nothing once a crawler has been seen.
    /// </summary>
    public sealed class MoodTracker
    {
        private readonly Dictionary<EntityId, (Mood Mood, long Since)> _shown = new Dictionary<EntityId, (Mood, long)>(32);

        /// <summary>The mood <paramref name="crawler"/> shows at <paramref name="tick"/>, from its signs then.</summary>
        public Mood Update(EntityId crawler, in MoodSigns signs, long tick)
        {
            if (!_shown.TryGetValue(crawler, out var shown))
            {
                var first = MoodRule.Of(signs, Mood.Content);
                _shown[crawler] = (first, tick);
                return first;
            }
            var next = MoodRule.Of(signs, shown.Mood);
            if (next == shown.Mood) return next;
            bool atOnce = next is Mood.Asleep or Mood.Hurt || tick < shown.Since; // a rewound clock (a load) starts afresh
            if (!atOnce && tick - shown.Since < MoodRule.HoldTicks) return shown.Mood;
            _shown[crawler] = (next, tick);
            return next;
        }

        /// <summary>The mood last shown for <paramref name="crawler"/>, or false if it hasn't been seen.</summary>
        public bool TryGet(EntityId crawler, out Mood mood)
        {
            bool seen = _shown.TryGetValue(crawler, out var shown);
            mood = shown.Mood;
            return seen;
        }

        /// <summary>Forgets every crawler: a new world.</summary>
        public void Clear() => _shown.Clear();
    }
}
