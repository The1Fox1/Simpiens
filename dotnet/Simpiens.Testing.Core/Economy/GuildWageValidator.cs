using System;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Social;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Economy
{
    /// <summary>
    /// Epic 10 G4.7, wages and quitting (D3, D4): each visit pays every member in join order, in full or not at all, from
    /// the stall; a paid member's trust, warmth and allegiance rise and an unpaid one's fall; a member quits after two
    /// unpaid visits in a row or below the trust floor, keeping its settlement; the ledger stays conserved; and the caravan's
    /// visit pays wages after the rations.
    /// </summary>
    public class GuildWageValidator
    {
        private SimulationManager _sim;
        private GroupRegistry _groups;
        private EconomyLedger _ledger;
        private WorldRegistry _world;

        public void RunAllValidationTests()
        {
            TestLog.Log("[GuildWageValidator] Beginning wage validation...");

            ValidatePaidAndUnpaid();
            ValidateQuittingAfterTwoUnpaidVisits();
            ValidateTheTrustFloor();
            ValidateTheVisitPaysWages();

            TestLog.Log("<color=green>[GuildWageValidator] ALL 4 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidatePaidAndUnpaid()
        {
            NewOutpost(treasury: 500, supply: false);
            var tuning = _sim.Economy.Guild;
            var first = Recruit(trust: 70, warmth: 50, wage: 5);
            var second = Recruit(trust: 70, warmth: 50, wage: 6);
            SetTreasury(8); // enough for the first's wage, not then for the second's
            uint before = Treasury() + (uint)first.Inventory.Gold + (uint)second.Inventory.Gold;
            int firstTrust = Trust(first), secondTrust = Trust(second);

            var stall = _sim.Guild.PayWages(_sim, _sim.ActiveStalls[0], 600);
            Assert(stall.TreasuryGold == 3 && Treasury() == 3, $"The first member was paid 5g from the stall (treasury {Treasury()}).");
            Assert(Treasury() + (uint)first.Inventory.Gold + (uint)second.Inventory.Gold == before, "Wages are transfers: no gold made or lost.");
            Assert(_ledger.Flows.Wages == 1 && _ledger.Flows.WageGold == 5, "The ledger counts the wage paid.");
            Assert(Trust(first) == firstTrust + tuning.Paid.Trust, "A paid member's trust rises.");
            Assert(_groups.TryGetMembership(first.AgentId, _sim.Guild.GroupId, out var paidMember) && paidMember.Allegiance == tuning.InitialAllegiance + tuning.PaidAllegiance, "And its allegiance.");
            Assert(Trust(second) == secondTrust + tuning.Unpaid.Trust, "The second, unpaid in full or not at all, loses trust.");
            Assert(_groups.TryGetMembership(second.AgentId, _sim.Guild.GroupId, out var unpaidMember) && unpaidMember.Allegiance == tuning.InitialAllegiance + tuning.UnpaidAllegiance, "And allegiance.");
            Assert(_sim.Guild.TryGetContract(second.AgentId, out var contract) && contract.UnpaidVisits == 1, "Its unpaid visits count up.");
            Assert(_sim.Guild.IsMember(second.AgentId), "One unpaid visit isn't enough to quit.");

            TestLog.Log("[PASS] Test 1: Wages in join order, each in full or not at all; paid members gain trust and allegiance, unpaid ones lose them.");
        }

        private void ValidateQuittingAfterTwoUnpaidVisits()
        {
            NewOutpost(treasury: 500, supply: false);
            var member = Recruit(trust: 90, warmth: 60, wage: 5);
            byte settlement = _groups.CreateGroup(GroupType.Settlement, EntityId.Generate(), Vec2.Zero);
            _groups.AddMember(settlement, member.AgentId);
            SetTreasury(0);

            _sim.Guild.PayWages(_sim, _sim.ActiveStalls[0], 600);
            Assert(_sim.Guild.IsMember(member.AgentId), "Still a member after one unpaid visit.");
            _sim.Guild.PayWages(_sim, _sim.ActiveStalls[0], 1200);
            Assert(!_sim.Guild.IsMember(member.AgentId) && !_groups.TryGetMembership(member.AgentId, _sim.Guild.GroupId, out _), "After two unpaid visits in a row it quits the guild.");
            Assert(_groups.TryGetMembership(member.AgentId, settlement, out _), "It keeps its settlement (D1).");
            Assert(_sim.Guild.CooldownUntil(member.AgentId) == 1200 + _sim.Economy.Guild.RejoinCooldownTicks, "And can't sign again for a while.");

            TestLog.Log("[PASS] Test 2: A member unpaid two visits in a row quits, keeping its settlement.");
        }

        private void ValidateTheTrustFloor()
        {
            NewOutpost(treasury: 500, supply: false);
            var tuning = _sim.Economy.Guild;
            var steady = Recruit(trust: 70, warmth: 50, wage: 5);
            var sour = Recruit(trust: 70, warmth: 50, wage: 5);
            sour.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(10, (sbyte)(tuning.QuitTrust - tuning.Paid.Trust - 1), 0, 0, 5, 590));
            _sim.Guild.PayWages(_sim, _sim.ActiveStalls[0], 600);
            Assert(_sim.Guild.IsMember(steady.AgentId), "A paid member above the floor stays.");
            Assert(!_sim.Guild.IsMember(sour.AgentId), $"A member whose trust is below {tuning.QuitTrust} quits, even paid.");

            TestLog.Log("[PASS] Test 3: A member below the trust floor quits at the visit.");
        }

        private void ValidateTheVisitPaysWages()
        {
            NewOutpost(treasury: 500, supply: true);
            var member = Recruit(trust: 70, warmth: 50, wage: 5);
            int gold = member.Inventory.Gold;
            uint interval = _sim.Economy.Supply.RestockIntervalTicks;
            _sim.Tick(interval);
            Assert(member.Inventory.Gold == gold + 5 && _ledger.Flows.Wages == 1, "The caravan's visit pays the wage.");
            Assert(_ledger.ValidateConservation(Treasury(), (uint)member.Inventory.Gold, out int discrepancy), $"The ledger balances after the visit (discrepancy {discrepancy}g).");

            TestLog.Log("[PASS] Test 4: The caravan's visit pays the guild's wages, conserved.");
        }

        private void NewOutpost(uint treasury, bool supply)
        {
            _world = new WorldRegistry();
            _groups = new GroupRegistry();
            _ledger = new EconomyLedger();
            _sim = new SimulationManager(_world, null, groupRegistry: _groups, economyLedger: _ledger, supplyService: supply ? new SupplyService() : null);
            _sim.SetEconomy(EconomyTuning.Default);
            _sim.RegisterTradeStall(TestStalls.Stall(EntityId.Generate(), Vec2.Zero, rationSellPrice: 5, rationStock: 50, treasuryGold: treasury));
            _ledger.RecordFaucet(treasury, "InitialSeed");
        }

        private Simpiens.Cognition.AgentState Recruit(sbyte trust, sbyte warmth, ushort wage)
        {
            var crawler = HeadlessEntities.Crawler(_world, _sim, null, Vec2.Zero, id: EntityId.Generate()).Agent;
            crawler.Inventory.Gold = 0;
            crawler.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(warmth, trust, 0, 0, 1, 0));
            var check = _sim.Guild.Recruit(_sim, crawler.AgentId, wage, 10);
            Assert(check == Simpiens.Simulation.Guild.GuildRules.Check.Ok, $"The recruit signs on ({check}).");
            return crawler;
        }

        private uint Treasury() => _sim.ActiveStalls[0].TreasuryGold;
        private void SetTreasury(uint gold) => _sim.UpdateStall(_sim.ActiveStalls[0].WithTreasury(gold));
        private static int Trust(Simpiens.Cognition.AgentState crawler) => crawler.Memory.GetAffinity(WorldRegistry.PlayerEntityId, 600).Trust;

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[ASSERTION FAILED] {message}");
            }
        }
    }
}
