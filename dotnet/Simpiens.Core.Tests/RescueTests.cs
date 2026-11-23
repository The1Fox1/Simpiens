using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Carrying and rescue (Epic 10 Part 3, K5.2, K5.3, Q3b), on the real outpost: mechanism tests, the fallen brought low and
    /// its friend made and placed by hand. A friend who hears it cry and would go where it lies comes: it carries the fallen
    /// out of the Sump at half pace and sets it down out of danger, where its clock stops and it lives; with a draught it hands
    /// that over instead and the fallen stands. A carry broken off leaves the fallen where it is, its clock running.
    /// </summary>
    public class RescueTests
    {
        private static readonly Vec2 InTheSump = new Vec2(-16f, -12f);

        [Fact]
        public void AFriendCarriesTheFallenOutOfTheSump()
        {
            using var sim = Outpost();
            var (fallen, friend) = FallenWithAFriendBeside(sim, draught: false);
            var medicine = sim.Host.CrawlerStates.ToDictionary(c => c.AgentId, c => c.Skills.Experience(Simpiens.Simulation.Skills.Skill.Medicine));
            bool carried = false, halfPace = false;
            uint bleedsOut = fallen.BleedsOutAt;
            for (long t = 0; t < DayClock.Hours(SimulationManager.BleedOutHours) + 100 && (fallen.IsCollapsed || fallen.CarriedBy != default); t++)
            {
                var before = friend.Position;
                sim.Step(1);
                if (fallen.CarriedBy != default)
                {
                    carried = true;
                    Assert.True(Vec2.Distance(fallen.Position, sim.Registry.GetEntity(fallen.CarriedBy).Position) < 0.01f, "It rides on its rescuer.");
                    if (fallen.CarriedBy == friend.AgentId && Vec2.Distance(before, friend.Position) > 0f)
                        halfPace |= Vec2.Distance(before, friend.Position) <= friend.BaseSpeed * Vitality.CarrySpeed(friend) * SimulationManager.FixedTickDeltaTime + 0.001f; // half, and a little more by its medicine (K9.3d)
                }
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"set down at t{sim.Clock.CurrentTick} at {fallen.Position} (it would have bled out at t{bleedsOut})");
            Assert.True(carried, "Someone carried it.");
            Assert.True(halfPace, "At half pace.");
            Assert.False(sim.Simulation.InDanger(fallen.Position), "Set down out of danger.");
            Assert.Equal(0u, fallen.BleedsOutAt);
            Assert.Equal(default, fallen.CarriedBy);
            sim.Step((int)DayClock.Hours(SimulationManager.BleedOutHours));
            Assert.Contains(fallen, sim.Host.CrawlerStates);
            var told = new List<ChronicleEvent>();
            sim.Chronicle.GetAllRecentEvents(told, 256);
            Assert.Contains(told, e => e.EventType == ChronicleEventType.CrawlerRescued && e.ActorBId == fallen.AgentId && e.ExtraData == 1f);
            // A rescue is practice in medicine (Epic 10 Part 3, K9.2)
            var rescue = told.First(e => e.EventType == ChronicleEventType.CrawlerRescued && e.ActorBId == fallen.AgentId);
            var rescuer = sim.Host.CrawlerStates.First(c => c.AgentId == rescue.ActorAId);
            Assert.True(rescuer.Traits.Count(Simpiens.Simulation.Skills.Deed.Rescue) >= 1 && fallen.Traits.Count(Simpiens.Simulation.Skills.Deed.DowningSurvived) >= 1,
                "The rescue is the rescuer's deed, and the fallen survived a downing (Epic 11, S0.5).");
            Assert.True(rescuer.Skills.Experience(Simpiens.Simulation.Skills.Skill.Medicine) >= medicine[rescuer.AgentId] + Simpiens.Simulation.Skills.SkillRules.RescueExperience, "Its rescuer practised medicine.");
        }

        [Fact]
        public void AFriendWithADraughtHandsItOver()
        {
            using var sim = Outpost();
            var (fallen, friend) = FallenWithAFriendBeside(sim, draught: true);
            friend.Traits = default; // by hand: no passion or technique for medicine, so a rescue is K9.2's 100 (Epic 11's are their validators')
            int medicine = friend.Skills.Experience(Simpiens.Simulation.Skills.Skill.Medicine);
            bool given = false;
            for (long t = 0; t < DayClock.Hours(1) && fallen.IsDowned; t++)
            {
                sim.Step(1);
                given |= friend.CurrentIntent is GiveMedicineIntent;
            }
            Assert.True(given, "It went to hand the draught over.");
            Assert.False(fallen.IsDowned, "The draught stood it up.");
            Assert.Equal(0, friend.Inventory.GetItemCount(ItemType.HealingDraught));
            Assert.True(fallen.Health >= Vitality.RecoveredAt);
            Assert.Equal(medicine + Simpiens.Simulation.Skills.SkillRules.RescueExperience, friend.Skills.Experience(Simpiens.Simulation.Skills.Skill.Medicine)); // practice (K9.2)
        }

        [Fact]
        public void ACarryBrokenOffLeavesTheFallenWhereItIs()
        {
            using var sim = Outpost();
            var (fallen, friend) = FallenWithAFriendBeside(sim, draught: false);
            for (long t = 0; t < DayClock.Hours(1) && fallen.CarriedBy == default; t++) sim.Step(1);
            Assert.NotEqual(default, fallen.CarriedBy);
            var rescuer = sim.Host.CrawlerStates.First(c => c.AgentId == fallen.CarriedBy);
            sim.Step(3);
            if (!fallen.IsCollapsed && fallen.CarriedBy == default) return; // already out (the Sump's edge was a step away)
            rescuer.Health = 0f;
            rescuer.IsDowned = true; // by hand: the rescuer falls
            var at = fallen.Position;
            sim.Step(2);
            Assert.Equal(default, fallen.CarriedBy);
            Assert.Equal(default, rescuer.Carrying);
            Assert.True(Vec2.Distance(at, fallen.Position) < 0.5f, "It lies where the carry ended.");
            if (sim.Simulation.InDanger(fallen.Position)) Assert.True(fallen.IsCollapsed && fallen.BleedsOutAt > 0, "Still in danger, its clock runs.");
        }

        // By hand: a resident brought low in the Sump with nothing on it, and a bold friend in hazard gear beside it (warmth
        // well over the line), maybe carrying a draught; then both are left to themselves
        private static (AgentState Fallen, AgentState Friend) FallenWithAFriendBeside(LocalSimulation sim, bool draught)
        {
            var friend = sim.Host.CrawlerStates.First(s => !s.IsDrifter && s.Personality.RiskTolerancePercent >= Vitality.VentureRiskTolerancePercent);
            var fallen = sim.Host.CrawlerStates.First(s => !s.IsDrifter && s != friend);
            Empty(fallen);
            Empty(friend);
            Assert.True(friend.Inventory.TryEquip(Outfitting.NewPiece(ItemType.HazardGear, Grades.Standard)));
            if (draught) Assert.True(friend.Inventory.TryAddItem(new ItemInstance(ItemType.HealingDraught, 1, 0), out _));
            uint tick = (uint)sim.Clock.CurrentTick;
            friend.Memory.RecordInteraction(fallen.AgentId, deltaWarmth: 60, deltaTrust: 40, deltaFear: 0, deltaRespect: 0, tick);
            fallen.Health = 1f;
            for (int t = 0; t < 600 && !fallen.IsDowned; t++)
            {
                fallen.Position = InTheSump;
                friend.Position = InTheSump + new Vec2(1.5f, 0f);
                sim.Step(1);
            }
            Assert.True(fallen.IsDowned);
            friend.Position = InTheSump + new Vec2(1.5f, 0f);
            sim.Step(1);
            Assert.True(fallen.IsCollapsed);
            return (fallen, friend);
        }

        private static void Empty(AgentState crawler)
        {
            for (int i = 0; i < crawler.Inventory.Capacity; i++)
            {
                var slot = crawler.Inventory.GetSlot(i);
                if (!slot.IsEmpty) crawler.Inventory.TryRemoveItem(slot.Type, slot.Quantity);
            }
        }

        private static LocalSimulation Outpost()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(20);
            return sim;
        }
    }
}
