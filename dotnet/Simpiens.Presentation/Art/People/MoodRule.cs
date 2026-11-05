using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Entities;

namespace Simpiens.Presentation.Art.People
{
    /// <summary>What a crawler's face reads from it (Epic UI-Beta B3): its shown state, health and needs, as plain data.</summary>
    public readonly struct MoodSigns
    {
        public MoodSigns(AgentVisualState state, bool downed, bool tantrum, float health, float hunger, float energy, float frustration)
        {
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

        /// <summary>The signs of <paramref name="agent"/> now. Reads it; writes nothing.</summary>
        public static MoodSigns Of(AgentState agent)
            => new MoodSigns(agent.VisualState, agent.IsDowned, agent.IsInTantrum, agent.Health, agent.Hunger, agent.Energy, agent.Frustration);
    }

    /// <summary>
    /// The mood a crawler's portrait shows (Epic UI-Beta B3, D1), by priority: asleep, hurt, frustrated (a tantrum),
    /// scared, frustrated, hungry, tired, content. Each threshold has a way back past it (hysteresis), so a need hovering
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

        /// <summary>The mood <paramref name="signs"/> show, given the mood shown now (its threshold is crossed back further).</summary>
        public static Mood Of(in MoodSigns signs, Mood shown)
        {
            if (signs.State == AgentVisualState.Sleeping) return Mood.Asleep;
            if (signs.Downed || signs.Health < (shown == Mood.Hurt ? HurtUntil : HurtBelow)) return Mood.Hurt;
            if (signs.Tantrum || signs.State == AgentVisualState.Defiant) return Mood.Frustrated;
            if (signs.State == AgentVisualState.Panicking) return Mood.Scared;
            if (signs.Frustration >= (shown == Mood.Frustrated ? FrustratedUntil : FrustratedAt)) return Mood.Frustrated;
            if (signs.Hunger >= (shown == Mood.Hungry ? HungryUntil : HungryAt)) return Mood.Hungry;
            if (signs.Energy <= (shown == Mood.Tired ? TiredUntil : TiredAt)) return Mood.Tired;
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
