using System;
using System.Collections.Generic;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Social;
using Simpiens.Cognition.Contracts;
using Simpiens.Core.Session.Layouts;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;
using Simpiens.Simulation.Workshops;
using Simpiens.Simulation.World;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Skills
{
    /// <summary>
    /// The cost of mastery (Epic 11, S2.2, Q9, D8): a crawler's asking wage is its reservation wage raised by its best band
    /// (×1.25 a Journeyman, ×1.5 a Master), for members and artisans alike, from the guild's rows; a recruit or a hire signs
    /// for no less; at a visit a raise paid keeps trust, and one unpaid costs the unpaid wage's trust and warmth with no unpaid
    /// visit counted, the ledger balanced; the chronicle tells the ask once, when the band is reached. Runs as a test
    /// (ValidatorTests).
    /// </summary>
    public class UpkeepValidator
    {
        private SimulationManager _sim;
        private WorldRegistry _world;
        private GroupRegistry _groups;
        private EconomyLedger _ledger;
        private NarrativeChronicleService _chronicle;

        public void RunAllValidationTests()
        {
            TestLog.Log("[UpkeepValidator] Beginning upkeep validation...");
            ValidateTheAskingWage();
            ValidateARecruitSignsForWhatItAsks();
            ValidateARaisePaidAndUnpaid();
            ValidateTheAskIsToldOnce();
            ValidateArtisansAskToo();
            TestLog.Log("<color=green>[UpkeepValidator] ALL 5 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateTheAskingWage()
        {
            NewOutpost(500);
            var tuning = _sim.Economy.Guild;
            var crawler = Crawler(70, 50);
            Assert(tuning.JourneymanRaisePercent == 125 && tuning.MasterRaisePercent == 150, "The raises: a Journeyman ×1.25, a Master ×1.5 (D8).");
            ushort reservation = GuildRules.ReservationWage(crawler, tuning);
            Assert(GuildRules.AskingWage(crawler, tuning) == reservation, "A Novice asks its reservation wage.");
            SetLevel(crawler, Skill.Mining, 5);
            Assert(GuildRules.AskingWage(crawler, tuning) == reservation, "So does an Apprentice.");
            SetLevel(crawler, Skill.Mining, 6);
            Assert(GuildRules.AskingWage(crawler, tuning) == 6, $"A Journeyman asks 5g × 1.25, rounded: 6g ({GuildRules.AskingWage(crawler, tuning)}g).");
            SetLevel(crawler, Skill.Mining, 9);
            Assert(GuildRules.AskingWage(crawler, tuning) == 8, $"A Master asks 5g × 1.5, rounded: 8g ({GuildRules.AskingWage(crawler, tuning)}g).");
            crawler.Personality = new AgentPersonality(1f, 0f, 0f, 0f, 0f);
            ushort greedy = GuildRules.ReservationWage(crawler, tuning);
            Assert(GuildRules.AskingWage(crawler, tuning) == (greedy * 150 + 50) / 100, "The raise multiplies a greedy crawler's own reservation wage.");
            Assert(GuildRules.Raised(40, SkillBand.Master, tuning, tuning.MaxWage) == tuning.MaxWage, "Never above the most a wage may be.");
            Assert(CrawlerSkills.BestBand(crawler) == SkillBand.Master && CrawlerSkills.BestBand(null) == SkillBand.Novice, "The best band is read through the seam.");
            TestLog.Log("[PASS] Test 1: the asking wage: the reservation wage to Apprentice, ×1.25 for a Journeyman, ×1.5 for a Master, within the most a wage may be.");
        }

        private void ValidateARecruitSignsForWhatItAsks()
        {
            NewOutpost(500);
            var tuning = _sim.Economy.Guild;
            var journeyman = Crawler(70, 50);
            SetLevel(journeyman, Skill.Smithing, 7);
            Assert(_sim.Guild.Recruit(_sim, journeyman.AgentId, 5, 10) == GuildRules.Check.WageTooLow, "A Journeyman won't sign for its reservation wage.");
            Assert(GuildRules.Describe(GuildRules.Check.WageTooLow, "Bram", journeyman, _sim.Guild, 10, tuning) == "Bram asks at least 6g a visit.", "The refusal names what it asks.");
            Assert(_sim.Guild.Recruit(_sim, journeyman.AgentId, 6, 10) == GuildRules.Check.Ok, "It signs for what it asks.");
            TestLog.Log("[PASS] Test 2: a recruit signs for no less than its asking wage, and the refusal names it.");
        }

        private void ValidateARaisePaidAndUnpaid()
        {
            NewOutpost(500);
            var tuning = _sim.Economy.Guild;
            var paid = Recruit(5);
            var unpaid = Recruit(5);
            SetLevel(paid, Skill.Melee, 6);
            SetLevel(unpaid, Skill.Melee, 6);
            Assert(_sim.Guild.SetWage(_sim, paid.AgentId, GuildRules.AskingWage(paid, tuning), 20) == GuildRules.Check.Ok, "The player pays one its raise.");
            int paidTrust = Trust(paid), unpaidTrust = Trust(unpaid), paidWarmth = Warmth(paid), unpaidWarmth = Warmth(unpaid);
            Assert(_groups.TryGetMembership(unpaid.AgentId, _sim.Guild.GroupId, out var before), "Precondition: a member.");
            int paidHad = paid.Inventory.Gold, unpaidHad = unpaid.Inventory.Gold; // the signing fee
            uint gold = Treasury() + (uint)paid.Inventory.Gold + (uint)unpaid.Inventory.Gold;
            _sim.Guild.PayWages(_sim, _sim.ActiveStalls[0], 600);
            Assert(paid.Inventory.Gold - paidHad == 6 && unpaid.Inventory.Gold - unpaidHad == 5, "Each is paid its wage in full.");
            Assert(Treasury() + (uint)paid.Inventory.Gold + (uint)unpaid.Inventory.Gold == gold, "Wages are transfers: no gold made or lost.");
            Assert(Trust(paid) == paidTrust + tuning.Paid.Trust && Warmth(paid) == paidWarmth + tuning.Paid.Warmth, "A raise paid keeps trust: it rises as any paid member's.");
            Assert(Trust(unpaid) == unpaidTrust + tuning.Unpaid.Trust && Warmth(unpaid) == unpaidWarmth + tuning.Unpaid.Warmth,
                $"A raise unpaid costs the unpaid wage's trust and warmth (trust {unpaidTrust} to {Trust(unpaid)}).");
            Assert(_sim.Guild.TryGetContract(unpaid.AgentId, out var contract) && contract.UnpaidVisits == 0, "No unpaid visit is counted: it was paid.");
            Assert(_groups.TryGetMembership(unpaid.AgentId, _sim.Guild.GroupId, out var after) && after.Allegiance == before.Allegiance, "Its allegiance stands.");
            Assert(_ledger.ValidateConservation(Treasury(), (uint)(paid.Inventory.Gold + unpaid.Inventory.Gold), out int discrepancy), $"The ledger balances ({discrepancy}g).");
            // Visit after visit unpaid, its trust falls under the line and it quits, never on unpaid visits
            for (uint visit = 2; visit <= 6 && _sim.Guild.IsMember(unpaid.AgentId); visit++) _sim.Guild.PayWages(_sim, _sim.ActiveStalls[0], 600 * visit);
            Assert(!_sim.Guild.IsMember(unpaid.AgentId) && _sim.Guild.IsMember(paid.AgentId), "Left unpaid, the raise's member quits once its trust falls under the line; the paid one stays.");
            TestLog.Log("[PASS] Test 3: a raise paid keeps trust; one unpaid costs the unpaid wage's trust and warmth, no unpaid visit counted, the ledger balanced, until it quits.");
        }

        private void ValidateTheAskIsToldOnce()
        {
            NewOutpost(500);
            var member = Recruit(5);
            SetLevel(member, Skill.Mining, 5);
            _sim.Practise(member, SkillWork.Harvest(ItemType.IronOre), SkillRules.ExperienceFor(6) - SkillRules.ExperienceFor(5), PracticeSource.Work);
            var asks = Asks();
            Assert(asks.Count == 1 && asks[0].ActorAId == member.AgentId && (int)asks[0].ExtraData == 6, $"Reaching Journeyman, the member asks for 6g, told once ({asks.Count}).");
            Assert(new TemplateNarrativeFormatter().FormatEvent(asks[0]).EndsWith("asks for a raise, to 6g a visit."), "The chronicle tells the ask.");
            _sim.Practise(member, SkillWork.Harvest(ItemType.IronOre), SkillRules.ExperienceFor(7) - SkillRules.ExperienceFor(6), PracticeSource.Work);
            Assert(Asks().Count == 1, "A level within the band asks nothing more.");
            _sim.Guild.SetWage(_sim, member.AgentId, 8, 20);
            _sim.Practise(member, SkillWork.Harvest(ItemType.IronOre), SkillRules.ExperienceFor(9) - SkillRules.ExperienceFor(7), PracticeSource.Work);
            Assert(Asks().Count == 1, "A Master already paid what it would ask asks nothing.");
            var loner = Crawler(70, 50);
            SetLevel(loner, Skill.Mining, 5);
            _sim.Practise(loner, SkillWork.Harvest(ItemType.IronOre), 600, PracticeSource.Work);
            Assert(Asks().Count == 1, "A crawler under no contract asks nobody.");
            TestLog.Log("[PASS] Test 4: the chronicle tells the ask once, when a band reached raises the asking wage above the contract's.");
        }

        private void ValidateArtisansAskToo()
        {
            NewOutpost(1000);
            int forge = _sim.Workshops.IndexOf(StationKind.Forge);
            Assert(_sim.Workshops.Build(_sim, forge, 0) == WorkshopRules.Check.Ok, "Precondition: the forge.");
            var smith = Crawler(70, 50);
            SetLevel(smith, Skill.Smithing, 6);
            Assert(_sim.Workshops.Hire(_sim, smith.AgentId, forge, 5, 10) == WorkshopRules.Check.WageTooLow, "A Journeyman smith won't take its reservation wage.");
            Assert(WorkshopService.AskingWage(_sim, smith) == 6 && WorkshopRules.AskingWage(smith, _sim.Economy.Workshops, _sim.Economy.Guild) == GuildRules.AskingWage(smith, _sim.Economy.Guild),
                "An artisan asks by the guild's rows, as a member does.");
            Assert(_sim.Workshops.Hire(_sim, smith.AgentId, forge, 6, 10) == WorkshopRules.Check.Ok, "It takes what it asks.");
            SetLevel(smith, Skill.Smithing, 8);
            _sim.Practise(smith, SkillWork.Make(StationKind.Forge), SkillRules.ExperienceFor(9) - SkillRules.ExperienceFor(8), PracticeSource.Work);
            Assert(Asks().Count == 1 && (int)Asks()[0].ExtraData == 8, "Reaching Master at the forge, it asks for 8g.");
            var tuning = _sim.Economy.Workshops;
            int trust = Trust(smith);
            _sim.Workshops.PayWages(_sim, _sim.ActiveStalls[0], 600);
            Assert(smith.Inventory.Gold == 6 && Trust(smith) == trust + tuning.Unpaid.Trust, "Paid 6g of the 8g it asks, it takes the unpaid wage's trust.");
            Assert(_sim.Workshops.TryGetContract(smith.AgentId, out var contract) && contract.UnpaidVisits == 0, "No unpaid visit is counted.");
            _sim.Workshops.SetWage(_sim, smith.AgentId, 8, 700);
            trust = Trust(smith);
            _sim.Workshops.PayWages(_sim, _sim.ActiveStalls[0], 1200);
            Assert(Trust(smith) == trust + tuning.Paid.Trust, "Paid its raise, its trust rises.");
            TestLog.Log("[PASS] Test 5: artisans ask by the same rows: hired for what they ask, told once, a raise paid or unpaid as a member's.");
        }

        private void NewOutpost(uint treasury)
        {
            _world = new WorldRegistry();
            _groups = new GroupRegistry();
            _ledger = new EconomyLedger();
            _chronicle = new NarrativeChronicleService();
            var sectors = new SectorRegistry();
            sectors.Load(Floor1Layout.Definition);
            _sim = new SimulationManager(_world, null, groupRegistry: _groups, chronicleService: _chronicle, sectors: sectors, economyLedger: _ledger);
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

        private Simpiens.Cognition.AgentState Recruit(ushort wage)
        {
            var crawler = Crawler(70, 50);
            var check = _sim.Guild.Recruit(_sim, crawler.AgentId, wage, 10);
            Assert(check == GuildRules.Check.Ok, $"The recruit signs on ({check}).");
            return crawler;
        }

        private static void SetLevel(Simpiens.Cognition.AgentState crawler, Skill skill, int level)
        {
            var skills = crawler.Skills;
            skills.Set(skill, level);
            crawler.Skills = skills;
        }

        private List<ChronicleEvent> Asks()
        {
            var all = new List<ChronicleEvent>();
            _chronicle.GetAllRecentEvents(all, 256);
            return all.FindAll(e => e.EventType == ChronicleEventType.RaiseAsked);
        }

        private uint Treasury() => _sim.ActiveStalls[0].TreasuryGold;
        private static int Trust(Simpiens.Cognition.AgentState crawler) => crawler.Memory.GetAffinity(WorldRegistry.PlayerEntityId, 600).Trust;
        private static int Warmth(Simpiens.Cognition.AgentState crawler) => crawler.Memory.GetAffinity(WorldRegistry.PlayerEntityId, 600).Warmth;

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[UpkeepValidator] FAILED: " + message);
        }
    }
}
