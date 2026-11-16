using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Planning.Goals;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Loot;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Picking up (Epic 10 Part 3, K4.3), on the real outpost: mechanism tests, the piles dropped by hand. The loot rule (D24):
    /// a better piece is worn and what it replaces left in its place, a worse one left, materials into the pack while they fit.
    /// And crawlers at work: a pile by the stall is picked over within a game day by whoever passes, and a claimed one only by
    /// its victor until the claim runs out. The live evidence is K4's DoD (K4.4), once the stall buys the materials.
    /// </summary>
    public class PickUpTests
    {
        [Fact]
        public void ABetterPieceIsWornAndAWorseOneLeft()
        {
            using var sim = Outpost();
            var piles = sim.Simulation.Piles;
            var world = (IPileWorld)sim.Simulation;
            var taker = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            Empty(taker.Inventory);
            Assert.True(taker.Inventory.TryEquip(Outfitting.NewPiece(ItemType.IronBlade, Grades.Standard)));
            uint tick = (uint)sim.Clock.CurrentTick;
            var pile = piles.Drop(world, taker.Position, new[]
            {
                Outfitting.NewPiece(ItemType.Cudgel, Grades.Standard),
                Outfitting.NewPiece(ItemType.FangKnife, Grades.Standard),
                new ItemInstance(1, ItemType.Hide, 2, 0),
            }, 0, taker.AgentId, tick);
            Assert.True(LootRules.WouldTakeAny(taker.Inventory, new PileView(pile)));

            int units = piles.Take(world, pile.Id, taker.Inventory, taker.AgentId, tick + 1, out _, out int worn);
            Assert.Equal(1, worn);
            Assert.Equal(3, units); // the knife, and two hides
            Assert.Equal(ItemType.FangKnife, taker.Inventory.GetEquipped(EquipmentSlot.Weapon).Type);
            Assert.Equal(2, taker.Inventory.GetItemCount(ItemType.Hide));
            Assert.True(piles.TryGet(pile.Id, out var left), "The cudgel and the blade it replaced are left.");
            var kinds = Enumerable.Range(0, left.Count).Select(i => left.StackAt(i).Type).OrderBy(t => t).ToArray();
            Assert.Equal(new[] { ItemType.Cudgel, ItemType.IronBlade }, kinds);
            Assert.All(Enumerable.Range(0, left.Count), i => Assert.True(left.StackAt(i).Durability > 0, "Left whole."));
            Assert.False(LootRules.WouldTakeAny(taker.Inventory, new PileView(left)), "Nothing left it would take: it won't walk back for it.");
        }

        [Fact]
        public void APieceFromAKillComesWhole()
        {
            System.Span<ItemInstance> drops = stackalloc ItemInstance[CreatureLoot.MaxDrops];
            bool seen = false;
            for (long tick = 0; tick < 20000 && !seen; tick++)
            {
                int n = LootRoller.Roll(CreatureLoot.Tables, CreatureLoot.Rat, 424242UL, new EntityId(1u, 2u, 3u, 4u), tick, drops);
                for (int i = 0; i < n; i++)
                {
                    if (drops[i].Type != ItemType.FangKnife) continue;
                    seen = true;
                    Assert.Equal(ItemCatalog.Get(ItemType.FangKnife).Equipment.DurabilityAt(Grades.Standard), drops[i].Durability);
                    Assert.Equal(1, drops[i].Quantity);
                }
            }
            Assert.True(seen, "A rat drops its fang knife now and then.");
        }

        [Fact]
        public void AFullPackLeavesTheMaterials()
        {
            using var sim = Outpost();
            var piles = sim.Simulation.Piles;
            var world = (IPileWorld)sim.Simulation;
            var taker = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            Empty(taker.Inventory);
            for (int i = 0; i < taker.Inventory.Capacity; i++) Assert.True(taker.Inventory.TryAddItem(new ItemInstance(1, ItemType.IronOre, ItemCatalog.Get(ItemType.IronOre).MaxStack, 0), out _));
            uint tick = (uint)sim.Clock.CurrentTick;
            var pile = piles.Drop(world, taker.Position, new[] { new ItemInstance(1, ItemType.Hide, 1, 0) }, 0, default, tick);
            Assert.False(LootRules.WouldTakeAny(taker.Inventory, new PileView(pile)), "No room: nothing it would take.");
            Assert.Equal(0, piles.Take(world, pile.Id, taker.Inventory, taker.AgentId, tick, out _, out _));
            Assert.True(piles.TryGet(pile.Id, out _));
        }

        [Fact]
        public void APileByTheStallIsPickedOver()
        {
            using var sim = Outpost();
            var spot = ByTheStall(sim);
            var pile = sim.Simulation.Piles.Drop((IPileWorld)sim.Simulation, spot, new[] { new ItemInstance(1, ItemType.Hide, 1, 0) }, 0, default, (uint)sim.Clock.CurrentTick);
            var collectors = new HashSet<EntityId>();
            long gone = -1;
            for (long t = 0; t < DayClock.TicksPerDay / 2 && gone < 0; t++)
            {
                sim.Step(1);
                foreach (var crawler in sim.Host.CrawlerStates) if (crawler.ActiveGoal is CollectLootGoal) collectors.Add(crawler.AgentId);
                if (!sim.Simulation.Piles.TryGet(pile.Id, out _)) gone = sim.Clock.CurrentTick;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"picked over at t{gone} (dropped by the stall at {spot}); {collectors.Count} set out for it");
            Assert.True(gone > 0 && gone < pile.FadesAt, "Somebody picked it over before it faded.");
            Assert.Equal(1, sim.Simulation.Piles.UnitsTaken);
            Assert.Contains(sim.Host.CrawlerStates, c => c.Inventory.GetItemCount(ItemType.Hide) == 1 && collectors.Contains(c.AgentId));
        }

        [Fact]
        public void AClaimedPileWaitsForItsVictor()
        {
            using var sim = Outpost();
            var spot = ByTheStall(sim);
            // Its victor far off, at the warren's side of the outpost: nobody else may take it for the claim's hour
            var victor = sim.Host.CrawlerStates.OrderByDescending(c => Vec2.Distance(c.Position, spot)).First(c => !c.IsDrifter);
            var pile = sim.Simulation.Piles.Drop((IPileWorld)sim.Simulation, spot, new[] { new ItemInstance(1, ItemType.Hide, 1, 0) }, 0, victor.AgentId, (uint)sim.Clock.CurrentTick);
            long gone = -1;
            EntityId taker = default;
            for (long t = 0; t < DayClock.TicksPerDay / 2 && gone < 0; t++)
            {
                var before = sim.Host.CrawlerStates.Where(c => c.Inventory.GetItemCount(ItemType.Hide) > 0).Select(c => c.AgentId).ToHashSet();
                sim.Step(1);
                if (sim.Simulation.Piles.TryGet(pile.Id, out _)) continue;
                gone = sim.Clock.CurrentTick;
                taker = sim.Host.CrawlerStates.First(c => c.Inventory.GetItemCount(ItemType.Hide) > 0 && !before.Contains(c.AgentId)).AgentId;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"picked over at t{gone} by {taker} (the victor {victor.AgentId}; its claim to t{pile.ClaimUntil})");
            Assert.True(gone > 0, "Somebody picked it over.");
            Assert.True(taker == victor.AgentId || gone >= pile.ClaimUntil, "Only its victor took it before the claim ran out.");
        }

        private static void Empty(AgentInventory inventory)
        {
            for (int i = 0; i < inventory.Capacity; i++)
            {
                var slot = inventory.GetSlot(i);
                if (!slot.IsEmpty) inventory.TryRemoveItem(slot.Type, slot.Quantity);
            }
            foreach (var slot in EquipmentSlots.All) inventory.Unequip(slot.Id);
        }

        // Where a crawler stood by the stall: walkable, charted, and where everyone passes
        private static Vec2 ByTheStall(LocalSimulation sim)
        {
            var stall = sim.Simulation.ActiveStalls[0].Position;
            for (int t = 0; t < 4000; t++)
            {
                foreach (var crawler in sim.Host.CrawlerStates)
                {
                    float d = Vec2.Distance(crawler.Position, stall);
                    if (d > 1.2f && d < 2.5f) return crawler.Position;
                }
                sim.Step(1);
            }
            Assert.Fail("Nobody came by the stall.");
            return default;
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
