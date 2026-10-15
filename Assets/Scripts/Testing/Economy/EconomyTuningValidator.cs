using System;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Planning;
using Simpiens.Core.Session;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// Review §3.3 step 1: one economy tuning per session. The defaults must equal the constants they replaced (the
    /// refactor changes no behaviour), and a session's tuning must reach every reader: the supply caravan, the trade
    /// rules (through the execution context) and cognition (through the world snapshot).
    /// </summary>
    public class EconomyTuningValidator
    {
        public void RunAllValidationTests()
        {
            Debug.Log("[EconomyTuningValidator] Beginning economy tuning validation...");

            ValidateDefaultsMatchLegacyConstants();
            ValidateSessionsExposeTheirTuning();
            ValidateSupplyFollowsTheSessionTuning();
            ValidateCognitionReadsTheSnapshotTuning();
            ValidateCopiesLeaveTheDefaultUntouched();

            Debug.Log("<color=green>[EconomyTuningValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateDefaultsMatchLegacyConstants()
        {
            var e = EconomyTuning.Default;

            // Item base values (were ItemTypeExtensions.BaseValue)
            Assert(e.BaseValue(ItemType.Ration) == 5 && e.BaseValue(ItemType.IronOre) == 8 && e.BaseValue(ItemType.MedicinalHerb) == 12,
                "Base values: ration 5, ore 8, herb 12.");
            Assert(e.BaseValue(ItemType.ToolPickaxe) == 30 && e.BaseValue(ItemType.BasicWeapon) == 40 && e.BaseValue(ItemType.None) == 0,
                "Base values: pickaxe 30, weapon 40, none 0.");

            // Crawlers (were AgentState / AgentInventory literals and Considerations.RationReferencePrice)
            Assert(e.StartingGold == 10 && EconomyTuning.DefaultStartingGold == 10, "Starting gold 10.");
            Assert(new AgentInventory().Gold == 10, "AgentInventory's default starting gold is the tuning's.");
            Assert(Mathf.Approximately(e.RationReferencePrice, 20f), "Reference ration price 20.");

            // Stall and seeded bounties (were OutpostSession literals)
            Assert(e.StallRationPrice == 15 && e.StallRationStock == 50, "Stall rations: 15g, stock 50.");
            Assert(e.StallOreBuyPrice == 20 && e.StallOreStock == 10, "Stall ore: buys at 20g, stock 10.");
            Assert(e.StallHerbBuyPrice == 25 && e.StallHerbStock == 10, "Stall herbs: buys at 25g, stock 10.");
            Assert(e.StallTreasury == 250, "Stall treasury 250.");
            Assert(e.SeedGatherBountyReward == 25 && e.SeedScoutBountyReward == 15, "Seeded rewards: gather 25, scout 15.");

            // Supply caravan (was SupplyTuning.Default via DI)
            var s = e.Supply;
            Assert(s.RestockIntervalTicks == 600 && s.RationStockCap == 50 && s.WholesaleRationCost == 2, "Caravan: every 600 ticks, cap 50, 2g wholesale.");
            Assert(s.ProvisionalOreExportPrice == 24 && s.ProvisionalHerbExportPrice == 30 && s.ProvisionalExportReserve == 10, "Provisional export: ore 24, herb 30, reserve 10.");
            var off = s.WithoutProvisionalExport();
            Assert(off.ProvisionalOreExportPrice == 0 && off.ProvisionalHerbExportPrice == 0 && off.RationStockCap == 50 && off.RestockIntervalTicks == 600,
                "Without the export only the export prices change.");

            // Trade-to-trust (were TradeIntentHandler locals)
            var t = e.TradeTrust;
            Assert(t.FairRationPrice == 5 && Mathf.Approximately(t.GougingMultiplier, 1.5f), "Gouging above 5g × 1.5.");
            AssertDelta(t.GenerousPurchase, 5, 10, 5, "generous purchase");
            AssertDelta(t.ModeratePurchase, 2, 3, 2, "moderate purchase");
            AssertDelta(t.GougingPurchase, -10, -15, 0, "gouging purchase");
            AssertDelta(t.SalvageSale, 5, 10, 5, "salvage sale");

            Debug.Log("[PASS] Test 1: The default tuning equals every constant it replaced.");
        }

        private void ValidateSessionsExposeTheirTuning()
        {
            Assert(ReferenceEquals(new OutpostSession().Economy, EconomyTuning.Default), "The live outpost runs on the default tuning.");
            var custom = EconomyTuning.Default.WithStallTerms(rationPrice: 30, oreBuyPrice: 10, herbBuyPrice: 12, treasury: 100);
            Assert(ReferenceEquals(new OutpostSession(OutpostSession.DefaultCrawlerCount, custom).Economy, custom), "A measurement session keeps its tuning.");
            Assert(ReferenceEquals(new Simpiens.Testing.Sessions.CanaryPenSession().Economy, EconomyTuning.Default), "The Dev Map runs on the default tuning.");

            var sim = new SimulationManager(new WorldRegistry(), null);
            Assert(ReferenceEquals(sim.Economy, EconomyTuning.Default), "A fresh simulation reports the default tuning.");
            IIntentExecutionContext context = sim;
            sim.SetEconomy(custom);
            Assert(ReferenceEquals(sim.Economy, custom) && ReferenceEquals(context.Economy, custom), "SetEconomy reaches the intent handlers' context.");
            sim.SetEconomy(null);
            Assert(ReferenceEquals(sim.Economy, EconomyTuning.Default), "A null tuning falls back to the default.");

            Debug.Log("[PASS] Test 2: Sessions expose their tuning and the simulation applies it.");
        }

        private void ValidateSupplyFollowsTheSessionTuning()
        {
            var ledger = new EconomyLedger();
            var supply = new SupplyService(); // default tuning: cap 50 every 600 ticks
            var sim = new SimulationManager(new WorldRegistry(), null, economyLedger: ledger, supplyService: supply);
            sim.SetEconomy(EconomyTuning.Default.WithSupply(new SupplyTuning(restockIntervalTicks: 20, rationStockCap: 30, wholesaleRationCost: 3)));
            Assert(supply.Tuning.RationStockCap == 30 && supply.Tuning.RestockIntervalTicks == 20, "SetEconomy reconfigures the supply service.");

            var stallId = new GUID(5u, 5u, 5u, 5u);
            sim.RegisterTradeStall(new TradeStallData(stallId, Vector2.zero, rationStock: 10, treasuryGold: 500));
            ledger.RecordFaucet(500, "InitialSeed");
            while (sim.CurrentTick < 20) sim.Tick();

            Assert(sim.TryGetTradeStall(stallId, out var stall), "Stall retrieved.");
            Assert(stall.RationStock == 30 && stall.TreasuryGold == 440, $"Caravan used the session's cap and price: stock {stall.RationStock} (30), treasury {stall.TreasuryGold} (440).");

            Debug.Log("[PASS] Test 3: The supply caravan follows the session's tuning.");
        }

        private void ValidateCognitionReadsTheSnapshotTuning()
        {
            var without = CreateContext(hunger: 50f, snapshot: null);
            Assert(ReferenceEquals(Considerations.Economy(without), EconomyTuning.Default), "No snapshot: the default tuning.");
            Assert(Considerations.RationReservationPrice(without) == 20, "Default reservation price at hunger 50 is 20g.");
            Assert(Considerations.MinimumSalePrice(without, ItemType.IronOre) == 8, "Default minimum ore price is 8g.");

            var snapshot = new SharedWorldSnapshot(10, 10, 1f, 4, _ => { });
            snapshot.SetEconomy(EconomyTuning.Default.WithRationReferencePrice(40f).WithBaseValues(ration: 5, ore: 16, herb: 12));
            var with = CreateContext(hunger: 50f, snapshot: snapshot);
            Assert(Considerations.RationReservationPrice(with) == 40, $"Reservation follows the snapshot's reference price ({Considerations.RationReservationPrice(with)}, expected 40).");
            Assert(Considerations.MinimumSalePrice(with, ItemType.IronOre) == 16, "Minimum sale price follows the snapshot's base values.");

            var partition = new SpatialHashGrid();
            partition.Initialize(100f, 100f, 10f);
            var economy = EconomyTuning.Default.WithStartingGold(42);
            partition.SetEconomy(economy);
            partition.UpdateFromRegistry(null);
            var published = partition.GetActiveSnapshot();
            Assert(published != null && ReferenceEquals(published.Economy, economy), "The partition publishes the tuning in every snapshot.");
            partition.UpdateFromRegistry(null);
            Assert(ReferenceEquals(partition.GetActiveSnapshot()?.Economy, economy), "The next snapshot carries it too.");

            Debug.Log("[PASS] Test 4: Cognition reads the session's tuning from the world snapshot.");
        }

        private void ValidateCopiesLeaveTheDefaultUntouched()
        {
            var copy = EconomyTuning.Default.WithStartingGold(99).WithStallTerms(1, 2, 3, 4).WithBaseValues(7, 7, 7)
                .WithTradeTrust(new TradeTrustTuning(1, 1f, default, default, default, default))
                .WithSupply(new SupplyTuning(1, 1, 1));
            Assert(copy.StartingGold == 99 && copy.StallTreasury == 4 && copy.BaseValue(ItemType.IronOre) == 7 && copy.Supply.RationStockCap == 1,
                "The copy carries its changes.");
            Assert(copy.StallRationStock == 50 && Mathf.Approximately(copy.RationReferencePrice, 20f), "Fields not changed are kept.");
            var d = EconomyTuning.Default;
            Assert(d.StartingGold == 10 && d.StallTreasury == 250 && d.BaseValue(ItemType.IronOre) == 8 && d.Supply.RationStockCap == 50 && d.TradeTrust.FairRationPrice == 5,
                "The default is never mutated by a copy.");

            Debug.Log("[PASS] Test 5: With… copies leave the default untouched.");
        }

        private static AgentContext CreateContext(float hunger, SharedWorldSnapshot snapshot)
        {
            var inventory = new AgentInventory(AgentInventory.DefaultCapacity, startingGold: 100);
            return new AgentContext(new GUID(77u, 1u, 1u, 1u), Vector2.zero, new AgentNeeds(hunger, 90f, 80f), 0f, snapshot,
                new AgentMemory(), 1, inventory: inventory.GetSnapshot(), personality: AgentPersonality.Neutral);
        }

        private static void AssertDelta(in TrustDelta delta, int warmth, int trust, int respect, string what)
        {
            Assert(delta.Warmth == warmth && delta.Trust == trust && delta.Respect == respect,
                $"Trust delta for a {what}: W{delta.Warmth}/T{delta.Trust}/R{delta.Respect} (expected W{warmth}/T{trust}/R{respect}).");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[ASSERTION FAILED] {message}");
            }
        }
    }
}
