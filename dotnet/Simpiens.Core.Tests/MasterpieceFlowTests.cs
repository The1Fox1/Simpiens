using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Workshops;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// A masterpiece through every hand (Epic 11, S8.3, D43–D44), on the live outpost: one waiting on the shelf, a resident
    /// buys it (the shelf's masterpiece first, at its grade and marked, its owner recorded) and keeps it across a load; its
    /// wearer dies and its body is picked over by another who wears it, a second owner; a kill counts on it, a named
    /// creature's by whom it was named for; handed back, it goes onto the shelf whole, and a gift hands it out again. The
    /// masterpiece is placed on the shelf by hand: inspiration is the forge's (InspirationValidator, the DoD).
    /// </summary>
    public class MasterpieceFlowTests
    {
        [Fact]
        public void AMasterpiecePassesThroughEveryHand()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(DayClock.TicksPerHour); // they know the quarry
            var sm = (SimulationManager)sim.Simulation;
            var maker = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            ushort id = sm.Masterpieces.Add(new MasterpieceRecord { Item = ItemType.ToolPickaxe, Station = StationKind.Forge, Place = MasterpiecePlace.Shelf, Adjective = 0, Noun = 1, Maker = maker.AgentId, MadeAt = 10u });
            sm.UpdateStall(sm.ActiveStalls[0].WithStocked(ItemType.ToolPickaxe, 1, Grades.Masterwork)); // the masterpiece on the shelf, by hand
            foreach (var crawler in sim.Host.CrawlerStates) crawler.Inventory.Gold = 100;

            // Bought: the shelf's masterpiece first, at its grade, marked; its owner recorded
            for (long t = 0; t < 6 * DayClock.TicksPerHour && sm.ActiveStalls[0].StockOf(ItemType.ToolPickaxe) > 0; t += 10) sim.Step(10);
            var buyer = sim.Host.CrawlerStates.Single(s => s.Activity.ToolPurchases > 0);
            var piece = buyer.Inventory.GetEquipped(EquipmentSlot.Tool);
            Assert.Equal(id, piece.Masterpiece);
            Assert.Equal(Grades.Masterpiece, piece.Quality);
            Assert.True(sm.Masterpieces.TryGet(id, out var record) && record.Place == MasterpiecePlace.World && record.OwnerCount == 1 && record.FirstOwner == buyer.AgentId,
                "Bought: out in the world, its buyer its first owner.");
            Assert.True(sm.ActiveStalls[0].TryGetGood(ItemType.ToolPickaxe, out var shelf) && shelf.Masterwork == 0, "The shelf's Masterwork count fell with it.");

            // Kept across a load
            byte[] saved = SaveSerializer.Serialize(sim.Host.Capture());
            sim.Step(20);
            Assert.True(sim.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out string refusal), refusal);
            sm = (SimulationManager)sim.Simulation;
            buyer = sim.Host.CrawlerStates.Single(s => s.AgentId == buyer.AgentId);
            Assert.Equal(id, buyer.Inventory.GetEquipped(EquipmentSlot.Tool).Masterpiece);
            Assert.True(sm.Masterpieces.TryGet(id, out record) && record.FirstOwner == buyer.AgentId && record.Maker == maker.AgentId, "A load keeps its mark on its wearer and its record.");

            // A blade masterpiece handed to the buyer by the shelf's door (as the guild's gift does); its wearer dies, and another,
            // unarmed, picks its body over and wears it: a second owner (a pickup wears only what helps in danger or a fight)
            ushort blade = sm.Masterpieces.Add(new MasterpieceRecord { Item = ItemType.IronBlade, Station = StationKind.Forge, Place = MasterpiecePlace.Shelf, Maker = maker.AgentId, MadeAt = 20u });
            var given = sm.Masterpieces.PieceFor(ItemType.IronBlade, Grades.Masterwork, 40, out ushort handedOut);
            Assert.Equal(blade, handedOut);
            Assert.True(buyer.Inventory.TryEquip(given));
            sm.Masterpieces.HandOut(blade, buyer.AgentId);
            var node = sim.Registry.GetEntity(buyer.AgentId);
            sm.Die(node, DeathCause.Creature);
            var body = sm.Piles.Piles.Single(p => p.BodyOf == buyer.AgentId);
            Assert.Contains(Enumerable.Range(0, body.Count).Select(i => body.StackAt(i)), s => s.Masterpiece == blade);
            Assert.Contains(Enumerable.Range(0, body.Count).Select(i => body.StackAt(i)), s => s.Masterpiece == id);
            var heir = sim.Host.CrawlerStates.First(s => !s.IsDrifter && !s.HasLeft && s.AgentId != buyer.AgentId && s.Inventory.GetEquipped(EquipmentSlot.Weapon).IsEmpty);
            sm.Piles.Take(sm, body.Id, heir.Inventory, heir.AgentId, (uint)sim.Clock.CurrentTick + 100000u, out _, out int worn);
            Assert.True(worn > 0 && heir.Inventory.GetEquipped(EquipmentSlot.Weapon).Masterpiece == blade, "The heir wears the blade.");
            Assert.True(sm.Masterpieces.TryGet(blade, out record) && record.OwnerCount == 2 && record.FirstOwner == buyer.AgentId && record.LastOwner == heir.AgentId, "Its second owner.");

            // A kill counts on it
            var rat = sm.Creatures.Creatures.First(c => !c.Definition.IsBoss);
            ((ICreatureWorld)sm).CreatureSlain(heir.AgentId, rat, System.ReadOnlySpan<ItemInstance>.Empty, (uint)sim.Clock.CurrentTick);
            Assert.True(sm.Masterpieces.TryGet(blade, out record) && record.Kills == 1 && record.NamedSlain == 0, "A kill while it is worn.");

            // Handed back, it goes onto the shelf whole; the next sale or gift hands it out first
            var heirsBlade = heir.Inventory.Unequip(EquipmentSlot.Weapon);
            int masterworkBefore = sm.ActiveStalls[0].TryGetGood(ItemType.IronBlade, out var blades) ? blades.Masterwork : 0;
            int wornBefore = blades.Worn;
            sm.UpdateStall(sm.Masterpieces.HandBack(sm.ActiveStalls[0], heirsBlade));
            Assert.True(sm.ActiveStalls[0].TryGetGood(ItemType.IronBlade, out blades) && blades.Masterwork == masterworkBefore + 1 && blades.Worn == wornBefore,
                "Handed back: among the Masterwork pieces, not the worn shelf.");
            Assert.True(sm.Masterpieces.TryGet(blade, out record) && record.Place == MasterpiecePlace.Shelf, "On the shelf again.");
            var again = sm.Masterpieces.PieceFor(ItemType.IronBlade, sm.ActiveStalls[0].BestGradeOf(ItemType.IronBlade), 40, out ushort next);
            Assert.True(next == blade && again.Masterpiece == blade && again.Quality == Grades.Masterpiece, "The next sale or gift hands it out first.");
        }
    }
}
