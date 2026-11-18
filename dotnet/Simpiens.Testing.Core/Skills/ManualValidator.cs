using System;
using System.Collections.Generic;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// Manuals as items (Epic 11, S3.2, Q10, D11): a primer for every skill in the table, each a row with a manual block (the
    /// work it teaches, 300 experience, to 3) and nothing else; the stall sells them at 25g, none to start; the caravan brings
    /// one of each at 8g into its own sink, the ledger conserved; the stall's goods table holds every good the session trades.
    /// Runs as a test (ValidatorTests).
    /// </summary>
    public class ManualValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[ManualValidator] Beginning manual validation...");
            ValidateAPrimerForEverySkill();
            ValidateTheirTerms();
            ValidateTheGoodsTableHoldsThem();
            ValidateTheCaravanBringsThem();
            TestLog.Log("<color=green>[ManualValidator] ALL 4 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private static void ValidateAPrimerForEverySkill()
        {
            var taught = new HashSet<Skill>();
            foreach (var item in ItemCatalog.InCategory(ItemCategory.Manual))
            {
                var definition = ItemCatalog.Get(item);
                var manual = definition.Manual;
                Assert(manual != null, $"{definition.Name} is a manual with its block.");
                var skill = SkillTable.SkillFor(manual.Work);
                Assert(skill != Skill.None && taught.Add(skill), $"{definition.Name} teaches a skill no other primer does ({skill}).");
                Assert(manual.Experience == ManualBlock.PrimerExperience && manual.Ceiling == ManualBlock.PrimerCeiling, $"{definition.Name}: 300 experience, to 3.");
                Assert(definition.Equipment == null && definition.ConsumeEffectCount == 0 && !definition.Is(ItemCategory.Consumable), $"{definition.Name} is read, not worn or eaten.");
            }
            foreach (var row in SkillTable.All) Assert(taught.Contains(row.Id), $"{row.DisplayName} has its primer.");
            foreach (var definition in ItemCatalog.All)
                Assert((definition.Manual != null) == definition.Is(ItemCategory.Manual), $"{definition.Name}: a manual block only on a manual.");
            TestLog.Log($"[PASS] Test 1: a primer for each of the {taught.Count} skills, each naming the work it teaches, 300 experience to 3.");
        }

        private static void ValidateTheirTerms()
        {
            var economy = EconomyTuning.Default;
            foreach (var item in ItemCatalog.InCategory(ItemCategory.Manual))
            {
                var terms = economy.Terms(item);
                Assert(terms.StallRole == StallRole.Sells && terms.StallPrice == 25 && terms.StallStock == 0, $"{item}: the stall sells it at 25g, none to start.");
                var term = economy.Supply.Terms(item);
                bool supplied = term.Item == item && term.Role == CaravanRole.Supplies && term.Limit == 1 && term.Price == 8 && term.FlowName == SupplyService.ManualSinkName;
                Assert(supplied, $"{item}: the caravan brings one at 8g into {SupplyService.ManualSinkName}.");
            }
            TestLog.Log("[PASS] Test 2: the stall sells each primer at 25g; the caravan brings one of each at 8g.");
        }

        private static void ValidateTheGoodsTableHoldsThem()
        {
            var goods = new List<StallGood>(TradeStallData.GoodsCapacity);
            EconomyTuning.Default.StallGoods(goods);
            Assert(goods.Count <= TradeStallData.GoodsCapacity, $"The session trades {goods.Count} goods, within the table's {TradeStallData.GoodsCapacity}.");
            var stall = new TradeStallData(EntityId.Generate(), Vec2.Zero, 100, 2f);
            foreach (var good in goods) stall = stall.WithGood(good);
            foreach (var item in ItemCatalog.InCategory(ItemCategory.Manual))
                Assert(stall.TryGetGood(item, out var held) && held.Price == 25, $"The stall opens with {item} on its table.");
            TestLog.Log($"[PASS] Test 3: the stall's table holds all {goods.Count} goods the session trades, the primers among them.");
        }

        private static void ValidateTheCaravanBringsThem()
        {
            var world = new WorldRegistry();
            var ledger = new EconomyLedger();
            var sim = new SimulationManager(world, null, economyLedger: ledger, supplyService: new SupplyService());
            sim.SetEconomy(EconomyTuning.Default);
            var goods = new List<StallGood>(TradeStallData.GoodsCapacity);
            EconomyTuning.Default.StallGoods(goods);
            var stall = new TradeStallData(EntityId.Generate(), Vec2.Zero, 500, 2f);
            foreach (var good in goods) stall = stall.WithGood(good);
            sim.RegisterTradeStall(stall);
            ledger.RecordFaucet(500, "InitialSeed");
            sim.Tick(EconomyTuning.Default.Supply.RestockIntervalTicks);
            var after = sim.ActiveStalls[0];
            int primers = 0;
            foreach (var item in ItemCatalog.InCategory(ItemCategory.Manual)) primers += after.SellStockOf(item);
            Assert(primers == ItemCatalog.InCategory(ItemCategory.Manual).Count, $"The caravan's visit shelves one of each primer ({primers}).");
            Assert(ledger.GetSinkTotal(SupplyService.ManualSinkName) == 8u * (uint)primers, $"It pays 8g each into {SupplyService.ManualSinkName} ({ledger.GetSinkTotal(SupplyService.ManualSinkName)}g).");
            Assert(ledger.ValidateConservation(after.TreasuryGold, 0, out int discrepancy), $"The ledger balances ({discrepancy}g).");
            TestLog.Log($"[PASS] Test 4: the caravan's visit shelves one of each primer, paying {8 * primers}g into its sink, the ledger balanced.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[ManualValidator] FAILED: " + message);
        }
    }
}
