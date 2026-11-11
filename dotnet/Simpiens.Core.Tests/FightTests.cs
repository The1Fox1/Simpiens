using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Fighting back (Epic 10 Part 3, K3.3, Q6), on the real outpost: mechanism tests, the crawler armed and put by a lair by
    /// hand and held there until the first blow. A bold crawler with a weapon fights the creature that struck it, its swings
    /// decided in the creatures' pass, and slays it (the chronicle tells it; its weapon wears; the slain creature's place
    /// waits for its return); a timid one, or one bare-handed, flees as before; a fighter under the retreat line flees.
    /// </summary>
    public class FightTests
    {
        private static readonly Vec2 BySeamOne = new Vec2(-20f, 14f); // seam.1, 2.2 from the beetle hollow's heart

        [Fact]
        public void ABoldArmedCrawlerFightsBackAndSlaysWhatStruckIt()
        {
            using var sim = Outpost();
            var fighter = Bold(sim);
            Arm(fighter, ItemType.GlowstoneEdge, ItemType.RingCoat);
            ushort durability = fighter.Inventory.GetEquipped(EquipmentSlot.Weapon).Durability;
            var creatures = sim.Simulation.Creatures;
            int beetles = creatures.Creatures.Count(c => c.Kind == CreatureKind.StoneBeetle);
            HeldUntilStruck(sim, fighter, BySeamOne);
            Assert.NotEqual(0u, fighter.StruckAtTick);
            var target = fighter.StruckBy;

            bool fought = false;
            long slainAt = -1;
            for (int t = 0; t < 900 && slainAt < 0; t++)
            {
                sim.Step(1);
                fought |= fighter.CurrentIntent is FightIntent;
                Assert.False(fighter.CurrentIntent is FleeIntent && fighter.Health >= Vitality.RetreatBelow, "Bold and armed, it doesn't flee while it can fight.");
                if (!creatures.TryGet(target, out _)) slainAt = sim.Clock.CurrentTick;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"slain at t{slainAt}; the fighter's health {fighter.Health:0.#}, its edge {fighter.Inventory.GetEquipped(EquipmentSlot.Weapon).Durability} of {durability}");
            Assert.True(fought, "It fought back.");
            Assert.True(slainAt > 0, "It slew the creature that struck it.");
            Assert.Equal(1, creatures.Kills);
            Assert.Equal(beetles - 1, creatures.Creatures.Count(c => c.Kind == CreatureKind.StoneBeetle));
            Assert.False(fighter.IsDowned);
            Assert.True(fighter.Inventory.GetEquipped(EquipmentSlot.Weapon).Durability < durability, "Its edge wore by the blows it landed.");
            var recent = new System.Collections.Generic.List<Simpiens.Narrative.ChronicleEvent>();
            sim.Chronicle.GetAllRecentEvents(recent, 64);
            Assert.Contains(recent, e => e.EventType == Simpiens.Narrative.ChronicleEventType.CreatureSlain && e.ActorAId == fighter.AgentId && e.ActorBId == target);
            var hollow = creatures.Lairs.First(l => l.Definition.Key == Floor1Layout.BeetleHollow);
            Assert.True(Enumerable.Range(0, hollow.Pack).Any(s => hollow.MemberIn(s) == default && hollow.ReturnAt(s) > 0), "Its place waits for its return.");
        }

        [Theory]
        [InlineData(true, false)] // bold and bare-handed
        [InlineData(false, true)] // timid with a blade
        public void TheTimidAndTheUnarmedStillFlee(bool bold, bool armed)
        {
            using var sim = Outpost();
            var crawler = bold ? Bold(sim) : sim.Host.CrawlerStates.First(s => !s.IsDrifter && s.Personality.RiskTolerancePercent < Vitality.VentureRiskTolerancePercent);
            if (armed) Arm(crawler, ItemType.IronBlade);
            HeldUntilStruck(sim, crawler, BySeamOne);
            bool fled = false, fought = false;
            for (int t = 0; t < 60; t++)
            {
                sim.Step(1);
                fled |= crawler.CurrentIntent is FleeIntent;
                fought |= crawler.CurrentIntent is FightIntent;
            }
            Assert.True(fled && !fought, $"bold {bold}, armed {armed}: it fled ({fled}) and didn't fight ({fought}).");
        }

        [Fact]
        public void AFighterUnderTheRetreatLineFlees()
        {
            using var sim = Outpost();
            var fighter = Bold(sim);
            Arm(fighter, ItemType.Cudgel);
            fighter.Health = Vitality.RetreatBelow + 15f; // by hand: a few blows from the line (a beetle takes 7)
            HeldUntilStruck(sim, fighter, BySeamOne);
            bool fought = false, fledUnder = false;
            for (int t = 0; t < 600 && !fledUnder; t++)
            {
                sim.Step(1);
                fought |= fighter.CurrentIntent is FightIntent;
                fledUnder |= fighter.CurrentIntent is FleeIntent && fighter.Health < Vitality.RetreatBelow;
                if (fighter.IsDowned) break;
            }
            Assert.True(fought, "It fought while it could.");
            Assert.True(fledUnder || fighter.IsDowned, "Under the retreat line it fled (or was downed first).");
        }

        private static LocalSimulation Outpost()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Sectors.SetExplored(Floor1Layout.DeepSeamSectorId, true);
            sim.Step(20);
            return sim;
        }

        private static AgentState Bold(LocalSimulation sim) => sim.Host.CrawlerStates.First(s => !s.IsDrifter && s.Personality.RiskTolerancePercent >= Vitality.VentureRiskTolerancePercent);

        // By hand: the pieces worn, new
        private static void Arm(AgentState crawler, params ItemType[] pieces)
        {
            foreach (var piece in pieces) Assert.True(crawler.Inventory.TryEquip(Outfitting.NewPiece(piece, Grades.Standard)), $"{piece} worn.");
        }

        // By hand: it stands there until the first blow, then it is left to itself
        private static void HeldUntilStruck(LocalSimulation sim, AgentState crawler, Vec2 at)
        {
            for (int t = 0; t < 400 && crawler.StruckAtTick == 0; t++)
            {
                crawler.Position = at;
                sim.Step(1);
            }
        }
    }
}
