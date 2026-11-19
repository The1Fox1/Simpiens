using System;
using System.Collections.Generic;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session.Layouts;
using Simpiens.Intervention;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Skills;
using Simpiens.Simulation.World;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// Sponsorship (Epic 11, S4.6, A6, Q3, D16): the patron offers one of its people gold to take up a band's other technique,
    /// and trust and nature decide. Each refusal has its reason and words; a sponsorship is paid from the stall's treasury into
    /// the crawler's purse, the ledger balanced, the slot changed and the chronicle told; a band sponsored back costs again; the
    /// typed command applies it on the tick. Runs as a test (ValidatorTests).
    /// </summary>
    public class SponsorValidator
    {
        private SimulationManager _sim;
        private WorldRegistry _world;
        private EconomyLedger _ledger;
        private NarrativeChronicleService _chronicle;
        private PlayerInterventionService _commands;

        public void RunAllValidationTests()
        {
            TestLog.Log("[SponsorValidator] Beginning sponsorship validation...");
            ValidateEachRefusal();
            ValidateASponsorshipPaid();
            ValidateTheCommand();
            TestLog.Log("<color=green>[SponsorValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateEachRefusal()
        {
            NewOutpost(500);
            var tuning = EconomyTuning.Default.Guild;
            var member = Recruit(Skill.Melee, 4, Technique.ShieldWall);
            Assert(Check(member.AgentId, Technique.RecklessFury, out ushort price) == SponsorRules.Check.Ok && price == tuning.SponsorApprenticePrice && price == 20,
                $"A member at Apprentice may be offered the band's other for 20g ({price}g).");
            Assert(Check(EntityId.Generate(), Technique.RecklessFury, out _) == SponsorRules.Check.NoSuchCrawler, "Nobody by that id.");
            var resident = Crawler(70, 50);
            Assert(Check(resident.AgentId, Technique.RecklessFury, out _) == SponsorRules.Check.NotOurs, "A resident outside the guild and the workshops isn't the patron's.");
            Assert(Check(member.AgentId, Technique.None, out _) == SponsorRules.Check.NoSuchTechnique, "No technique.");
            Assert(Check(member.AgentId, Technique.HeavyBlows, out price) == SponsorRules.Check.BandNotReached && price == 40, "Not a Journeyman yet (and a Journeyman's costs 40g).");
            Assert(Check(member.AgentId, Technique.ShieldWall, out _) == SponsorRules.Check.AlreadyHeld, "It holds Shield Wall already.");
            Assert(Describe(SponsorRules.Check.AlreadyHeld, member, Technique.ShieldWall).Contains("already holds Shield Wall"), "The refusal names it.");

            var cautious = Recruit(Skill.Melee, 4, Technique.ShieldWall);
            cautious.Personality = new AgentPersonality(0f, -0.8f, 0f, 0f, 0f);
            Assert(Check(cautious.AgentId, Technique.RecklessFury, out _) == SponsorRules.Check.AgainstItsNature, "The cautious (-0.8) won't take up Reckless Fury.");
            Assert(Describe(SponsorRules.Check.AgainstItsNature, cautious, Technique.RecklessFury).Contains("against their nature"), "Its words say why.");

            var wary = Recruit(Skill.Melee, 4, Technique.ShieldWall);
            wary.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(10, 40, 0, 0, 1, 0));
            Assert(Check(wary.AgentId, Technique.RecklessFury, out _) == SponsorRules.Check.NotYetTrusted, "Trust 40 is under the line of 50.");
            Assert(Describe(SponsorRules.Check.NotYetTrusted, wary, Technique.RecklessFury).StartsWith("Not yet: trust 40 of 50"), "Its words give the numbers.");

            var stall = _sim.ActiveStalls[0];
            _sim.UpdateStall(stall.WithTreasury(15));
            _ledger.RecordSink(stall.TreasuryGold - 15, "Test");
            Assert(Check(member.AgentId, Technique.RecklessFury, out _) == SponsorRules.Check.CannotPay, "The stall can't pay 20g from 15g.");
            Assert(Describe(SponsorRules.Check.CannotPay, member, Technique.RecklessFury, 20) == "The stall can't pay the 20g.", "Its words name the price.");
            TestLog.Log("[PASS] Test 1: each refusal: nobody, not ours, no technique, a band not reached, held already, the stall short, trust under the line, against its nature; each in words.");
        }

        private void ValidateASponsorshipPaid()
        {
            NewOutpost(500);
            var member = Recruit(Skill.Melee, 4, Technique.ShieldWall);
            uint treasury = Treasury();
            int purse = member.Inventory.Gold;
            var check = SponsorRules.Sponsor(_sim.Guild, _sim.Workshops, _sim, member.AgentId, Technique.RecklessFury, 100);
            Assert(check == SponsorRules.Check.Ok, $"Sponsored ({check}).");
            Assert(member.Traits.TechniqueIn(Skill.Melee, SkillBand.Apprentice) == Technique.RecklessFury, "It holds Reckless Fury now, in Shield Wall's place.");
            Assert(Treasury() == treasury - 20 && member.Inventory.Gold == purse + 20, $"20g from the stall into its purse ({treasury} -> {Treasury()}, {purse} -> {member.Inventory.Gold}).");
            Assert(_ledger.ValidateConservation(Treasury(), CrawlerGold(), out int discrepancy), $"A transfer: the ledger balances ({discrepancy}g).");
            Assert(Told(ChronicleEventType.TechniqueSponsored) == 1, "The chronicle tells it.");
            var back = SponsorRules.Sponsor(_sim.Guild, _sim.Workshops, _sim, member.AgentId, Technique.ShieldWall, 200);
            Assert(back == SponsorRules.Check.Ok && member.Traits.TechniqueIn(Skill.Melee, SkillBand.Apprentice) == Technique.ShieldWall && Treasury() == treasury - 40,
                "Sponsored back, Shield Wall again, for another 20g.");
            var refused = SponsorRules.Sponsor(_sim.Guild, _sim.Workshops, _sim, member.AgentId, Technique.ShieldWall, 300);
            Assert(refused == SponsorRules.Check.AlreadyHeld && Treasury() == treasury - 40 && Told(ChronicleEventType.TechniqueSponsored) == 2, "Refused, it changes nothing.");
            TestLog.Log("[PASS] Test 2: a sponsorship paid: 20g from the stall into the purse, the ledger balanced, the slot changed and told; sponsored back for as much again.");
        }

        private void ValidateTheCommand()
        {
            NewOutpost(500);
            var member = Recruit(Skill.Mining, 3, Technique.SeamStudent);
            _commands.QueueInfluence(InfluenceEvent.CreateSponsorTechnique("test", member.AgentId, Technique.HardDriver));
            Assert(member.Traits.TechniqueIn(Skill.Mining, SkillBand.Apprentice) == Technique.SeamStudent, "Nothing changes until the tick.");
            _sim.Tick(1);
            Assert(member.Traits.TechniqueIn(Skill.Mining, SkillBand.Apprentice) == Technique.HardDriver, "On the tick the command takes it up.");
            var cmd = InfluenceEvent.CreateSponsorTechnique("test", member.AgentId, Technique.HardDriver);
            Assert(cmd.CommandType == InfluenceCommandType.SponsorTechnique && cmd.LegacyCommandName == "SponsorTechnique" && cmd.SponsorTechnique.Technique == Technique.HardDriver, "A typed command, named.");
            TestLog.Log("[PASS] Test 3: the typed command applies a sponsorship on the tick.");
        }

        private SponsorRules.Check Check(EntityId crawler, Technique technique, out ushort price) =>
            SponsorRules.CheckSponsor(_sim.Guild, _sim.Workshops, _sim, crawler, technique, 100, out price);

        private string Describe(SponsorRules.Check check, Simpiens.Cognition.AgentState crawler, Technique technique, ushort price = 20) =>
            SponsorRules.Describe(check, crawler, technique, price, EconomyTuning.Default.Guild, 100);

        private void NewOutpost(uint treasury)
        {
            _world = new WorldRegistry();
            _ledger = new EconomyLedger();
            _chronicle = new NarrativeChronicleService();
            _commands = new PlayerInterventionService();
            var sectors = new SectorRegistry();
            sectors.Load(Floor1Layout.Definition);
            _sim = new SimulationManager(_world, null, groupRegistry: new GroupRegistry(), chronicleService: _chronicle, sectors: sectors, economyLedger: _ledger, interventionService: _commands);
            _sim.SetEconomy(EconomyTuning.Default);
            _sim.RegisterTradeStall(TestStalls.Stall(EntityId.Generate(), Vec2.Zero, treasuryGold: treasury));
            _ledger.RecordFaucet(treasury, "InitialSeed");
        }

        private Simpiens.Cognition.AgentState Crawler(sbyte trust, sbyte warmth)
        {
            var crawler = HeadlessEntities.Crawler(_world, _sim, null, Vec2.Zero, id: EntityId.Generate()).Agent;
            crawler.Inventory.Gold = 0;
            crawler.Personality = new AgentPersonality(0f, 0.1f, 0f, 0f, 0f);
            crawler.Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(warmth, trust, 0, 0, 1, 0));
            return crawler;
        }

        private Simpiens.Cognition.AgentState Recruit(Skill skill, int level, Technique holding)
        {
            var crawler = Crawler(70, 50);
            var skills = default(SkillSet);
            skills.Set(skill, level);
            crawler.Skills = skills;
            var traits = default(CrawlerTraits);
            var row = TechniqueTable.Get(holding);
            traits.SetTechnique(row.Skill, row.Band, holding);
            crawler.Traits = traits;
            var check = _sim.Guild.Recruit(_sim, crawler.AgentId, 10, 10);
            Assert(check == GuildRules.Check.Ok, $"The recruit signs on ({check}).");
            return crawler;
        }

        private uint Treasury() => _sim.ActiveStalls[0].TreasuryGold;

        private uint CrawlerGold()
        {
            uint gold = 0;
            var all = _world.ActiveEntities;
            for (int i = 0; i < all.Count; i++) if (all[i].Agent?.Inventory != null) gold += (uint)all[i].Agent.Inventory.Gold;
            return gold;
        }

        private int Told(ChronicleEventType type)
        {
            var all = new List<ChronicleEvent>();
            _chronicle.GetAllRecentEvents(all, 256);
            return all.FindAll(e => e.EventType == type).Count;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[SponsorValidator] FAILED: " + message);
        }
    }
}
