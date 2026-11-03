using System;
using Simpiens.Entities;
using Simpiens.Presentation.Art.People;
using Simpiens.Simulation;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>
    /// The mood rule (Epic UI-Beta B3, D1): each mood from its signs, by priority; no flicker at a threshold; a mood held
    /// three seconds unless the next is asleep or hurt; nothing allocated per update.
    /// </summary>
    public class MoodRuleTests
    {
        private static MoodSigns Signs(AgentVisualState state = AgentVisualState.Idle, bool downed = false, bool tantrum = false,
            float health = 100f, float hunger = 10f, float energy = 90f, float frustration = 0f)
            => new MoodSigns(state, downed, tantrum, health, hunger, energy, frustration);

        [Fact]
        public void EachMoodComesFromItsSigns()
        {
            Assert.Equal(Mood.Content, MoodRule.Of(Signs(), Mood.Content));
            Assert.Equal(Mood.Asleep, MoodRule.Of(Signs(AgentVisualState.Sleeping, health: 10f), Mood.Content));
            Assert.Equal(Mood.Hurt, MoodRule.Of(Signs(health: 39f, hunger: 99f), Mood.Content));
            Assert.Equal(Mood.Hurt, MoodRule.Of(Signs(downed: true), Mood.Content));
            Assert.Equal(Mood.Frustrated, MoodRule.Of(Signs(AgentVisualState.Panicking, tantrum: true), Mood.Content));
            Assert.Equal(Mood.Frustrated, MoodRule.Of(Signs(AgentVisualState.Defiant), Mood.Content));
            Assert.Equal(Mood.Scared, MoodRule.Of(Signs(AgentVisualState.Panicking, hunger: 90f), Mood.Content));
            Assert.Equal(Mood.Frustrated, MoodRule.Of(Signs(frustration: 60f, hunger: 90f), Mood.Content));
            Assert.Equal(Mood.Hungry, MoodRule.Of(Signs(hunger: 70f, energy: 10f), Mood.Content));
            Assert.Equal(Mood.Tired, MoodRule.Of(Signs(energy: 25f), Mood.Content));
        }

        [Theory]
        [InlineData(Mood.Hungry)]
        [InlineData(Mood.Tired)]
        [InlineData(Mood.Frustrated)]
        [InlineData(Mood.Hurt)]
        public void ANeedHoveringAtItsThresholdDoesntFlicker(Mood mood)
        {
            // Back and forth across the way in, short of the way back: once shown, the mood stays
            MoodSigns At(bool past) => mood switch
            {
                Mood.Hungry => Signs(hunger: past ? MoodRule.HungryAt : MoodRule.HungryAt - 1f),
                Mood.Tired => Signs(energy: past ? MoodRule.TiredAt : MoodRule.TiredAt + 1f),
                Mood.Frustrated => Signs(frustration: past ? MoodRule.FrustratedAt : MoodRule.FrustratedAt - 1f),
                _ => Signs(health: past ? MoodRule.HurtBelow - 1f : MoodRule.HurtBelow + 1f),
            };
            var tracker = new MoodTracker();
            var id = new EntityId(1u, 0u, 0u, 0u);
            Assert.Equal(Mood.Content, tracker.Update(id, At(false), 0));
            Assert.Equal(mood, tracker.Update(id, At(true), 100));
            for (long t = 101; t < 1000; t++) Assert.Equal(mood, tracker.Update(id, At((t & 1) == 0), t));
        }

        [Fact]
        public void AMoodIsHeldThreeSecondsButSleepAndHurtShowAtOnce()
        {
            var tracker = new MoodTracker();
            var id = new EntityId(2u, 0u, 0u, 0u);
            Assert.Equal(Mood.Hungry, tracker.Update(id, Signs(hunger: 80f), 0));
            Assert.Equal(Mood.Hungry, tracker.Update(id, Signs(), 10)); // fed, but the face holds
            Assert.Equal(Mood.Hungry, tracker.Update(id, Signs(), MoodRule.HoldTicks - 1));
            Assert.Equal(Mood.Content, tracker.Update(id, Signs(), MoodRule.HoldTicks));
            Assert.Equal(Mood.Hurt, tracker.Update(id, Signs(health: 20f), MoodRule.HoldTicks + 1)); // at once
            Assert.Equal(Mood.Asleep, tracker.Update(id, Signs(AgentVisualState.Sleeping, health: 20f), MoodRule.HoldTicks + 2));
        }

        [Fact]
        public void AnUpdateAllocatesNothing()
        {
            var tracker = new MoodTracker();
            var ids = new EntityId[20];
            for (uint i = 0; i < ids.Length; i++)
            {
                ids[i] = new EntityId(i + 1u, 0u, 0u, 0u);
                tracker.Update(ids[i], Signs(), 0);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (long t = 1; t < 200; t++)
                for (int i = 0; i < ids.Length; i++) tracker.Update(ids[i], Signs(hunger: (t + i) % 100), t);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}
