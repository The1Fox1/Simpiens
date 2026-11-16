using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Persistence;
using Simpiens.Testing.Economy;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The equipment system's proof (Epic 10 Part 3, K1.6; the producer's Q11, "the foundation of a more complex appendable
    /// equipment system"): a slot is added to the game by data alone, on the real outpost. A feet slot (a row of the table,
    /// of a new kind) and boots that fit it (an item, with the stall's terms) join through the catalogues' test hooks
    /// (<see cref="TestCatalogs.FeetAndBoots"/>); no other code knows them. Stats sum the boots with what else is worn and a
    /// tick in danger wears them; a bold resident buys a pair for its bare feet on its own (K1.4, by stats); the guild's
    /// outfit gives them for a member's feet; and a live save holds them and loads them back, with no change of format
    /// (K1.2). The client's half, the dossier, is the client's tests' (<c>EquipmentSlotProofTests</c> there).
    /// </summary>
    public class EquipmentSlotProofTests
    {
        private static EconomyTuning WithBoots() =>
            EconomyTuning.Default.WithItemTerms(TestCatalogs.Boots, new ItemTerms(baseValue: 20, StallRole.Sells, stallPrice: 15, stallStock: 4));

        [Fact]
        public void ANewSlotIsWornSummedAndWornOutByItsRow()
        {
            using var extended = TestCatalogs.FeetAndBoots();
            var inventory = new AgentInventory(4, startingGold: 0);
            Assert.True(inventory.TryEquip(new ItemInstance(1, TestCatalogs.Boots, 1, 15, durability: 2), out var slot) && slot == TestCatalogs.Feet, "The boots go on the feet.");
            Assert.True(inventory.TryEquip(new ItemInstance(2, ItemType.HazardGear, 1, 30, durability: 1500)), "Hazard gear goes on beside them.");
            Assert.Equal(0.7f, CrawlerStats.Get(inventory, StatKind.HazardResistance), 4); // 0.5 and 0.2, stacked (K1.3)
            Assert.Equal(TestCatalogs.Feet, WornGear.TryReplacing(inventory, TestCatalogs.Boots, out var replacing) ? replacing : default);

            // A tick in danger wears them by their rule, with the gear (K1.3); the last unit breaks them
            System.Span<ItemType> broke = stackalloc ItemType[EquipmentSlots.Capacity];
            Assert.Equal(2, inventory.WearBy(WearRule.PerHazardTick, broke, out int count));
            Assert.Equal(0, count);
            inventory.WearBy(WearRule.PerHazardTick, broke, out count);
            Assert.True(count == 1 && broke[0] == TestCatalogs.Boots && inventory.GetEquipped(TestCatalogs.Feet).IsEmpty, "Worn to their end, the boots are gone.");
        }

        [Fact]
        public void ABoldResidentBuysBootsForItsBareFeet()
        {
            using var extended = TestCatalogs.FeetAndBoots();
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Host.Respawn(new OutpostSession(OutpostSession.DefaultCrawlerCount, WithBoots()));
            Assert.Equal(4, sim.Simulation.ActiveStalls[0].SellStockOf(TestCatalogs.Boots));

            AgentState shod = null;
            for (long t = 0; t < 2 * DayClock.TicksPerDay && shod == null; t++)
            {
                sim.Step(1);
                shod = sim.Host.CrawlerStates.FirstOrDefault(c => !c.IsDrifter && c.Inventory.GetEquipped(TestCatalogs.Feet).Type == TestCatalogs.Boots);
            }
            TestContext.Current.TestOutputHelper?.WriteLine(shod != null
                ? $"boots bought at t{sim.Clock.CurrentTick} by a resident of risk {shod.Personality.RiskTolerancePercent}%, wearing {WornGear.Protection(shod.Inventory).Type} too; gear bought {shod.Activity.GearPurchases}"
                : "no boots bought");
            Assert.NotNull(shod);
            Assert.True(shod.Personality.RiskTolerancePercent >= Vitality.VentureRiskTolerancePercent, "A venturer bought them.");
            Assert.Equal(ItemType.HazardGear, shod.Inventory.GetEquipped(EquipmentSlot.Body).Type); // its body already outfitted, the feet were the bare slot
            Assert.True(sim.Simulation.ActiveStalls[0].SellStockOf(TestCatalogs.Boots) < 4, "From the stall's stock.");
            Assert.Equal(0, sim.Simulation.IntentFaults);
        }

        [Fact]
        public void TheGuildOutfitsTheNewSlot()
        {
            using var extended = TestCatalogs.FeetAndBoots();
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Host.Respawn(new OutpostSession(OutpostSession.DefaultCrawlerCount, WithBoots()));
            var host = (IGuildHost)sim.Simulation;
            var member = sim.Host.CrawlerStates[0];
            member.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(60, 80, 0, 0, 1, 0));
            Assert.Equal(GuildRules.Check.Ok, sim.Simulation.Guild.Recruit(host, member.AgentId, sim.Simulation.Economy.Guild.MaxWage, 0));

            var flooding = GuildRules.HazardOf(host, Simpiens.Core.Session.Layouts.Floor1Layout.DrownedGallerySectorId);
            Assert.Equal(TestCatalogs.Boots, GuildRules.BestForBare(sim.Simulation.ActiveStalls[0], TestCatalogs.Feet, flooding));
            var stock = sim.Simulation.ActiveStalls[0];
            var gifts = new GearGift[8];
            int count = 0;
            GuildRules.PlanOutfit(sim.Simulation.Guild, host, new[] { member.AgentId }, flooding, OutfitRound.Both, ref stock, gifts, ref count);
            Assert.Contains(gifts.Take(count), g => g.Item == TestCatalogs.Boots && g.Slot == TestCatalogs.Feet);
            Assert.Contains(gifts.Take(count), g => g.Item == ItemType.HazardGear);
            var commands = (Simpiens.Intervention.IPlayerInterventionService)sim.Services.GetService(typeof(Simpiens.Intervention.IPlayerInterventionService));
            for (int i = 0; i < count; i++) commands.QueueInfluence(Simpiens.Intervention.InfluenceEvent.CreateGiveGear("Test", gifts[i].Crawler, gifts[i].Item, flooding));
            sim.Step(1);
            Assert.Equal(TestCatalogs.Boots, member.Inventory.GetEquipped(TestCatalogs.Feet).Type);
            Assert.Equal(ItemType.HazardGear, member.Inventory.GetEquipped(EquipmentSlot.Body).Type);
        }

        [Fact]
        public void ALiveSaveHoldsTheNewSlot()
        {
            using var extended = TestCatalogs.FeetAndBoots();
            using var played = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            played.Host.Respawn(new OutpostSession(OutpostSession.DefaultCrawlerCount, WithBoots()));
            var crawler = played.Host.CrawlerStates[2];
            Assert.True(crawler.Inventory.TryEquip(new ItemInstance(1, TestCatalogs.Boots, 1, 15, durability: 444)), "Boots, by hand.");
            played.Step(10);
            byte[] saved = SaveSerializer.Serialize(played.Host.Capture());

            using var loaded = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            loaded.Host.Respawn(new OutpostSession(OutpostSession.DefaultCrawlerCount, WithBoots()));
            Assert.True(loaded.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out string refusal), $"The save was refused: {refusal}");
            var back = loaded.Host.CrawlerStates.First(c => c.AgentId == crawler.AgentId);
            Assert.Equal((TestCatalogs.Boots, (ushort)444), (back.Inventory.GetEquipped(TestCatalogs.Feet).Type, back.Inventory.GetEquipped(TestCatalogs.Feet).Durability));
            Assert.Equal(saved, SaveSerializer.Serialize(loaded.Host.Capture()));
            Assert.Equal(SessionSaveHeader.CurrentFormatVersion, loaded.Host.Capture().Header.FormatVersion); // no change of format (K1.2)
        }
    }
}
