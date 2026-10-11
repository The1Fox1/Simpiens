using System;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Inventory;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Planning.Goals;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Spatial;
using UnityEngine;
using VContainer.Unity;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Planning
{
    /// <summary>
    /// Milestone 1 closure, Phase 4 (review §3.2): agent personality and utility considerations.
    /// Mechanism tests with hand-built contexts; live price response is asserted by PriceResponseScenario.
    /// </summary>
    public class PersonalityValidator : IStartable
    {
        public void Start()
        {
            RunAllValidationTests();
        }

        public void RunAllValidationTests()
        {
            Debug.Log("[PersonalityValidator] Beginning personality & considerations validation...");

            ValidateGenerationIsDeterministic();
            ValidateNeutralIsUnchanged();
            ValidateReservationPrice();
            ValidateMinimumSalePrice();
            ValidateBountyChoiceFollowsPersonality();

            Debug.Log("<color=green>[PersonalityValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateGenerationIsDeterministic()
        {
            var id = new GUID(10u, 20u, 30u, 40u);
            var a = AgentPersonality.Generate(42, id);
            var b = AgentPersonality.Generate(42, new GUID(10u, 20u, 30u, 40u));
            Assert(a.Greed == b.Greed && a.Diligence == b.Diligence && a.Sociability == b.Sociability && a.Loyalty == b.Loyalty && a.RiskTolerance == b.RiskTolerance,
                "Same seed and id must generate the same personality.");
            Assert(default(AgentPersonality).Greed == 0f && AgentPersonality.Neutral.Diligence == 0f, "default(AgentPersonality) must be neutral.");

            float sum = 0f, min = 1f, max = -1f;
            bool anyDifferent = false;
            const int count = 500;
            for (uint i = 0; i < count; i++)
            {
                var p = AgentPersonality.Generate(42, new GUID(i + 1u, 7u, 7u, 7u));
                Assert(p.Greed >= -1f && p.Greed <= 1f, $"Trait out of range: {p.Greed}");
                sum += p.Greed;
                min = Mathf.Min(min, p.Greed);
                max = Mathf.Max(max, p.Greed);
                if (p.Greed != a.Greed) anyDifferent = true;
            }
            Assert(anyDifferent && max - min > 1f, $"Personalities must vary across agents (range {min:F2}..{max:F2}).");
            Assert(Mathf.Abs(sum / count) < 0.1f, $"Trait mean {sum / count:F3} should be ~0 (centred distribution).");

            Debug.Log("[PASS] Test 1: Personalities are deterministic per (seed, id), centred, varied, and default to neutral.");
        }

        private void ValidateNeutralIsUnchanged()
        {
            Assert(Considerations.TraitFactor(0f, 0.9f) == 1f, "A neutral trait must yield a factor of exactly 1.");

            // EarnIncome's pre-personality formula: max(5, 25 + need - hunger*0.2), need = clamp(Comfortable - gold) * 0.5
            var context = CreateContext(AgentPersonality.Neutral, hunger: 20f, gold: 10, snapshot: null);
            float expected = Mathf.Max(5f, 25f + Mathf.Clamp(EarnIncomeGoal.ComfortableGold - 10, 0, EarnIncomeGoal.ComfortableGold) * 0.5f - 20f * 0.2f);
            Assert(Mathf.Approximately(new EarnIncomeGoal().CalculatePriority(context), expected), "Neutral EarnIncome priority must equal the pre-personality formula.");

            var social = CreateContext(AgentPersonality.Neutral, hunger: 20f, gold: 10, snapshot: null, social: 30f);
            Assert(Mathf.Approximately(new SocializeGoal().CalculatePriority(social), 70f), "Neutral Socialize priority must equal 100 - social.");

            var sociable = CreateContext(new AgentPersonality(0f, 0f, 1f, 0f, 0f), hunger: 20f, gold: 10, snapshot: null, social: 30f);
            Assert(new SocializeGoal().CalculatePriority(sociable) > 70f, "A sociable agent must want company more than a neutral one.");

            Debug.Log("[PASS] Test 2: Neutral personalities reproduce the pre-personality priorities; traits move them.");
        }

        private void ValidateReservationPrice()
        {
            var neutral = CreateContext(AgentPersonality.Neutral, hunger: 50f, gold: 100, snapshot: null);
            var greedy = CreateContext(new AgentPersonality(1f, 0f, 0f, 0f, 0f), hunger: 50f, gold: 100, snapshot: null);
            var generous = CreateContext(new AgentPersonality(-1f, 0f, 0f, 0f, 0f), hunger: 50f, gold: 100, snapshot: null);
            var starving = CreateContext(AgentPersonality.Neutral, hunger: 90f, gold: 100, snapshot: null);

            Assert(Considerations.RationReservationPrice(neutral) == 20, $"Neutral reservation at hunger 50 is {Considerations.RationReservationPrice(neutral)} (expected 20).");
            Assert(Considerations.RationReservationPrice(greedy) < 20 && Considerations.RationReservationPrice(generous) > 20, "Greed lowers, generosity raises the reservation price.");
            Assert(Considerations.RationReservationPrice(starving) > 20, "Hunger raises the reservation price.");

            // The purchase goal respects it: a reachable stall priced at 25g is refused at hunger 50, bought when starving
            var snapshot = new SharedWorldSnapshot(20, 20, 1f, 8, _ => { });
            snapshot.SetStalls(new[] { new TradeStallData(new GUID(3u, 3u, 3u, 3u), new Vector2(1f, 0f), rationSellPrice: 25, rationStock: 10) }, 1);
            var atHalf = CreateContext(AgentPersonality.Neutral, hunger: 50f, gold: 100, snapshot: snapshot);
            var atStarving = CreateContext(AgentPersonality.Neutral, hunger: 90f, gold: 100, snapshot: snapshot);
            Assert(!new PurchaseProvisionsGoal().IsValid(atHalf), "25g ration must be refused above the neutral reservation at hunger 50.");
            Assert(new PurchaseProvisionsGoal().IsValid(atStarving), "25g ration must be acceptable when starving (reservation 28g).");

            Debug.Log("[PASS] Test 3: Reservation price follows hunger and greed, and gates the purchase goal.");
        }

        private void ValidateMinimumSalePrice()
        {
            var neutral = CreateContext(AgentPersonality.Neutral, hunger: 20f, gold: 10, snapshot: null);
            var greedy = CreateContext(new AgentPersonality(1f, 0f, 0f, 0f, 0f), hunger: 20f, gold: 10, snapshot: null);
            Assert(Considerations.MinimumSalePrice(neutral, ItemType.IronOre) == ItemType.IronOre.BaseValue(), "Neutral minimum sale price is the item's base value.");
            Assert(Considerations.MinimumSalePrice(greedy, ItemType.IronOre) > ItemType.IronOre.BaseValue(), "Greedy agents ask more.");

            var inventory = new AgentInventory();
            inventory.TryAddItem(new ItemInstance((ushort)ItemType.IronOre, ItemType.IronOre, 1, ItemType.IronOre.BaseValue()), out _);
            var holder = CreateContext(new AgentPersonality(1f, 0f, 0f, 0f, 0f), hunger: 20f, gold: 10, snapshot: null, inventory: inventory);
            var cheapStall = new TradeStallData(new GUID(4u, 4u, 4u, 4u), Vector2.zero, oreBuyPrice: 9, treasuryGold: 500);
            var fairStall = new TradeStallData(new GUID(5u, 5u, 5u, 5u), Vector2.zero, oreBuyPrice: 20, treasuryGold: 500);
            Assert(ResourceTargeting.AffordableHeldSalvage(holder, cheapStall) == ItemType.None, "A greedy seller refuses 9g ore (minimum 12g).");
            Assert(ResourceTargeting.AffordableHeldSalvage(holder, fairStall) == ItemType.IronOre, "A greedy seller accepts 20g ore.");

            Debug.Log("[PASS] Test 4: Minimum sale price follows greed and gates salvage sales.");
        }

        private void ValidateBountyChoiceFollowsPersonality()
        {
            var stallId = new GUID(6u, 6u, 6u, 6u);
            var snapshot = new SharedWorldSnapshot(40, 40, 1f, 8, _ => { });
            snapshot.SetStalls(new[] { new TradeStallData(stallId, Vector2.zero) }, 1);
            snapshot.SetBounties(new[]
            {
                new BountyContract(1, BountyType.GatherResource, 0, ItemType.IronOre, 1, 20, 0, 5000, new Vector2(2f, 0f), creatorStallId: stallId),
                new BountyContract(2, BountyType.GatherResource, 0, ItemType.IronOre, 1, 40, 0, 5000, new Vector2(12f, 0f), creatorStallId: stallId)
            }, 2);

            ushort Pick(AgentPersonality p)
            {
                var c = CreateContext(p, hunger: 20f, gold: 10, snapshot: snapshot);
                Assert(BountyQueries.TryGetOpenBounty(c, BountyType.None, out var b), "An open bounty is found.");
                return b.BountyId;
            }

            Assert(Pick(AgentPersonality.Neutral) == 1, "Neutral agent prefers the near, modest bounty.");
            Assert(Pick(new AgentPersonality(1f, 0f, 0f, 0f, 1f)) == 2, "Greedy, diligent agent prefers the far, richer bounty.");
            Assert(Pick(new AgentPersonality(-1f, 0f, 0f, 0f, -1f)) == 1, "Unmotivated agent prefers the near bounty.");

            Debug.Log("[PASS] Test 5: Bounty choice weighs reward against distance by personality.");
        }

        private static AgentContext CreateContext(AgentPersonality personality, float hunger, int gold, SharedWorldSnapshot snapshot,
            float social = 80f, AgentInventory inventory = null)
        {
            inventory ??= new AgentInventory(AgentInventory.DefaultCapacity, startingGold: gold);
            inventory.Gold = gold;
            return new AgentContext(new GUID(99u, 1u, 1u, 1u), Vector2.zero, new AgentNeeds(hunger, 90f, social), 0f, snapshot,
                new AgentMemory(), 1, inventory: inventory.GetSnapshot(), personality: personality);
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
