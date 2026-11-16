using System.Linq;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Hunt bounties (Epic 10 Part 3, K6.1, Q7), on the real outpost: mechanism tests, the hunters' blows and the lair's
    /// emptying done by hand. A hunt is posted only on a charted lair with a creature in it, its escrow locked, its sector
    /// and heart the lair's; up to three join it; each one's blows on the lair's creatures add to its share; when the lair is
    /// empty the escrow is paid out by damage dealt and the bounty is done, the ledger balanced. A load keeps the party.
    /// </summary>
    public class HuntBountyTests
    {
        [Fact]
        public void AHuntIsPostedOnlyOnACharteredLairAndJoinedByThree()
        {
            using var sim = Outpost();
            int warren = WarrenIndex(sim);
            Post(sim, warren, 60);
            Assert.Empty(sim.Simulation.Hunts); // not charted yet: refused

            ChartTheWarren(sim);
            uint escrowBefore = sim.Simulation.EconomyLedger.TotalEscrowGold;
            Post(sim, warren, 60);
            var hunt = Assert.Single(sim.Simulation.Hunts);
            Assert.Equal(warren, hunt.LairIndex);
            Assert.Equal(0, hunt.Count);
            var bounty = sim.Simulation.ActiveBounties.Single(b => b.BountyId == hunt.BountyId);
            Assert.Equal(BountyType.Hunt, bounty.Type);
            Assert.Equal(BountyStatus.Open, bounty.Status);
            Assert.Equal(Floor1Layout.OvergrowthSectorId, bounty.TargetSectorId);
            Assert.Equal(escrowBefore + 60u, sim.Simulation.EconomyLedger.TotalEscrowGold);

            var residents = sim.Host.CrawlerStates.Where(s => !s.IsDrifter).Take(4).ToList();
            for (int i = 0; i < 3; i++) Assert.True(sim.Simulation.TryJoinHunt(hunt.BountyId, residents[i].AgentId));
            Assert.True(sim.Simulation.TryJoinHunt(hunt.BountyId, residents[0].AgentId), "Already in it.");
            Assert.False(sim.Simulation.TryJoinHunt(hunt.BountyId, residents[3].AgentId), "Three at most.");
            Assert.Equal(3, sim.Simulation.Hunts[0].Count);
            Assert.Equal(BountyStatus.Open, sim.Simulation.ActiveBounties.Single(b => b.BountyId == hunt.BountyId).Status);
        }

        [Fact]
        public void AnEmptiedLairPaysItsHuntersByDamageDealt()
        {
            using var sim = Outpost();
            int warren = WarrenIndex(sim);
            ChartTheWarren(sim);
            Post(sim, warren, 60);
            var hunt = sim.Simulation.Hunts[0];
            var residents = sim.Host.CrawlerStates.Where(s => !s.IsDrifter).Take(3).ToList();
            foreach (var r in residents) Assert.True(sim.Simulation.TryJoinHunt(hunt.BountyId, r.AgentId));
            var world = (ICreatureWorld)sim.Simulation;
            var rats = sim.Simulation.Creatures.Creatures.Where(c => c.LairIndex == warren).ToList();
            Assert.NotEmpty(rats);
            // By hand: the first dealt 30, the second 10, the third nothing; a stranger's blow counts for nobody
            world.CreatureHit(residents[0].AgentId, rats[0], 30f);
            world.CreatureHit(residents[1].AgentId, rats[0], 10f);
            world.CreatureHit(sim.Host.CrawlerStates.Last().AgentId, rats[0], 50f);
            Assert.Equal(30f, sim.Simulation.Hunts[0].Damage0);
            Assert.Equal(10f, sim.Simulation.Hunts[0].Damage1);
            int[] purses = residents.Select(r => r.Inventory.Gold).ToArray();
            Assert.True(Balanced(sim));

            uint tick = (uint)sim.Clock.CurrentTick;
            foreach (var rat in rats) sim.Simulation.Creatures.Slay(world, rat.Id, tick); // by hand: the lair emptied
            sim.Step(1);
            Assert.Empty(sim.Simulation.Hunts);
            Assert.Equal(45, residents[0].Inventory.Gold - purses[0]); // 60 x 30/40
            Assert.Equal(15, residents[1].Inventory.Gold - purses[1]); // 60 x 10/40
            Assert.Equal(0, residents[2].Inventory.Gold - purses[2]);
            Assert.True(Balanced(sim), "The escrow went to the hunters.");
        }

        [Fact]
        public void TheSplitGivesTheRestToTheMostDamageAndSharesEquallyWithNone()
        {
            var hunt = new HuntRecord(7, 0).WithClaimant(new EntityId(1, 0, 0, 0)).WithClaimant(new EntityId(2, 0, 0, 0)).WithClaimant(new EntityId(3, 0, 0, 0));
            System.Span<uint> shares = stackalloc uint[3];
            hunt.Split(100, shares);
            Assert.Equal(new uint[] { 34, 33, 33 }, shares.ToArray()); // nobody dealt any: equal, the rest to the first
            hunt = hunt.WithDamage(new EntityId(2, 0, 0, 0), 2f).WithDamage(new EntityId(3, 0, 0, 0), 1f);
            hunt.Split(100, shares);
            Assert.Equal(new uint[] { 0, 67, 33 }, shares.ToArray()); // 66.67 and 33.33 floored, the rest to the most
            Assert.Equal(64, System.Runtime.CompilerServices.Unsafe.SizeOf<HuntRecord>());
        }

        [Fact]
        public void ALoadKeepsTheHuntsParty()
        {
            using var sim = Outpost();
            int warren = WarrenIndex(sim);
            ChartTheWarren(sim);
            Post(sim, warren, 60);
            var hunt = sim.Simulation.Hunts[0];
            var hunter = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            Assert.True(sim.Simulation.TryJoinHunt(hunt.BountyId, hunter.AgentId));
            ((ICreatureWorld)sim.Simulation).CreatureHit(hunter.AgentId, sim.Simulation.Creatures.Creatures.First(c => c.LairIndex == warren), 12f);
            byte[] saved = Simpiens.Simulation.Persistence.SaveSerializer.Serialize(sim.Host.Capture());
            sim.Step(10);
            Assert.True(sim.Host.TryRestore(Simpiens.Simulation.Persistence.SaveSerializer.Deserialize<Simpiens.Simulation.Persistence.SessionSave>(saved), out string refusal), refusal);
            Assert.Equal(saved, Simpiens.Simulation.Persistence.SaveSerializer.Serialize(sim.Host.Capture()));
            var loaded = Assert.Single(sim.Simulation.Hunts);
            Assert.True(loaded.HasClaimant(hunter.AgentId));
            Assert.Equal(12f, loaded.Damage0);
        }

        [Fact]
        public void BoldArmedResidentsJoinAndHuntTheLairEmpty()
        {
            // By hand: the warren charted, its Matriarch slain (Epic 10 Part 3, K10.2a, D58: while she lives nobody braves it alone,
            // NobodyHuntsTheMatriarchsWarrenAlone) and its bold residents armed and armoured; the rest is theirs (K6.2)
            using var sim = Outpost();
            int warren = WarrenIndex(sim);
            ChartTheWarren(sim);
            SlayTheMatriarch(sim, warren);
            var bold = ArmTheBold(sim);
            Post(sim, warren, 60);
            var hunt = sim.Simulation.Hunts[0];
            int[] purses = bold.Select(b => b.Inventory.Gold).ToArray();
            bool joined = false, hunted = false;
            for (long t = 0; t < 2 * DayClock.TicksPerDay && sim.Simulation.Hunts.Count > 0; t++)
            {
                sim.Step(1);
                if (sim.Simulation.Hunts.Count > 0) joined |= sim.Simulation.Hunts[0].Count > 0;
                foreach (var b in bold) hunted |= b.CurrentIntent is Simpiens.Cognition.Contracts.HuntIntent;
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"t{sim.Clock.CurrentTick}: hunts left {sim.Simulation.Hunts.Count}; kills {sim.Simulation.Creatures.Kills}; paid {string.Join(", ", bold.Select((b, i) => b.Inventory.Gold - purses[i]))}");
            Assert.True(joined, "Someone joined the hunt.");
            Assert.True(hunted, "A bold resident went hunting.");
            Assert.Empty(sim.Simulation.Hunts); // the warren emptied and the hunt paid
            Assert.Equal(0, sim.Simulation.Creatures.Lairs[warren].Alive);
            var told = new System.Collections.Generic.List<Simpiens.Narrative.ChronicleEvent>();
            sim.Chronicle.GetAllRecentEvents(told, 512);
            Assert.Contains(told, e => e.EventType == Simpiens.Narrative.ChronicleEventType.BountyFulfilled && bold.Any(b => b.AgentId == e.ActorAId));
            Assert.True(Balanced(sim));
        }

        /// <summary>
        /// D58 (Epic 10 Part 3, K10.2a): while the Matriarch heads the warren, the bold judge it past what they would brave alone,
        /// so a hunt posted on it finds no hunter: the warren is the guild's to clear until she falls.
        /// </summary>
        [Fact]
        public void NobodyHuntsTheMatriarchsWarrenAlone()
        {
            using var sim = Outpost();
            int warren = WarrenIndex(sim);
            ChartTheWarren(sim);
            var bold = ArmTheBold(sim);
            var lair = sim.Simulation.Creatures.Lairs[warren].Definition;
            Assert.True(lair.HasBoss && sim.Simulation.Creatures.Lairs[warren].MemberIn(0) != default, "The Matriarch heads the warren.");
            foreach (var b in bold) Assert.False(Vitality.BravesLair(lair, true, b.Skills, b.Inventory, b.Health, b.Personality.RiskTolerancePercent, b.IsDowned), "Nobody braves her warren alone.");
            Post(sim, warren, 60);
            bool joined = false;
            for (long t = 0; t < DayClock.TicksPerDay && sim.Simulation.Hunts.Count > 0; t++)
            {
                sim.Step(1);
                if (sim.Simulation.Hunts.Count > 0) joined |= sim.Simulation.Hunts[0].Count > 0;
            }
            Assert.False(joined, "Nobody joined a hunt on the Matriarch's warren.");
            Assert.True(Balanced(sim));
        }

        // The bold residents (two at most), armed with the glowstone edge and the ring coat
        private static System.Collections.Generic.List<AgentState> ArmTheBold(LocalSimulation sim)
        {
            var bold = sim.Host.CrawlerStates.Where(s => !s.IsDrifter && s.Personality.RiskTolerancePercent >= Vitality.VentureRiskTolerancePercent).Take(2).ToList();
            Assert.NotEmpty(bold); // one on this seed
            foreach (var b in bold)
            {
                Assert.True(b.Inventory.TryEquip(Simpiens.Simulation.Items.Outfitting.NewPiece(ItemType.GlowstoneEdge, Simpiens.Simulation.Items.Grades.Standard)));
                Assert.True(b.Inventory.TryEquip(Simpiens.Simulation.Items.Outfitting.NewPiece(ItemType.RingCoat, Simpiens.Simulation.Items.Grades.Standard)));
            }
            return bold;
        }

        private static void SlayTheMatriarch(LocalSimulation sim, int warren)
        {
            var creatures = sim.Simulation.Creatures;
            Assert.True(creatures.Slay((Simpiens.Simulation.Creatures.ICreatureWorld)sim.Simulation, creatures.Lairs[warren].MemberIn(0), (uint)sim.Clock.CurrentTick), "The Matriarch is slain by hand.");
        }

        private static void Post(LocalSimulation sim, int lair, ushort reward)
        {
            var commands = (IPlayerInterventionService)sim.Services.GetService(typeof(IPlayerInterventionService));
            commands.QueueInfluence(InfluenceEvent.CreatePostBounty("Test", new PostBountyCommand(0, BountyType.Hunt, 0, ItemType.None, 1, reward,
                durationTicks: DayClock.Hours(48), lairIndex: (byte)lair)));
            sim.Step(1);
        }

        private static int WarrenIndex(LocalSimulation sim)
        {
            var lairs = sim.Simulation.Creatures.Lairs;
            for (int i = 0; i < lairs.Count; i++) if (lairs[i].Definition.Key == Floor1Layout.RatWarren) return i;
            return -1;
        }

        // By the crawlers' own rule: the Overgrowth charted, and a resident held within sight of a rat until the warren is charted
        private static void ChartTheWarren(LocalSimulation sim)
        {
            sim.Sectors.SetExplored(Floor1Layout.OvergrowthSectorId, true);
            int warren = WarrenIndex(sim);
            var watcher = sim.Host.CrawlerStates.Last(s => !s.IsDrifter);
            var home = watcher.Position;
            for (int t = 0; t < 200 && !sim.Simulation.Creatures.IsCharted(warren); t++)
            {
                watcher.Position = new Vec2(20f, 14f); // overgrowth.3, by the warren
                sim.Step(1);
            }
            watcher.Position = home;
            Assert.True(sim.Simulation.Creatures.IsCharted(warren), "The warren is charted.");
        }

        private static bool Balanced(LocalSimulation sim)
        {
            uint stalls = 0, agents = 0;
            foreach (var stall in sim.Simulation.ActiveStalls) stalls += stall.TreasuryGold;
            foreach (var crawler in sim.Host.CrawlerStates) agents += (uint)crawler.Inventory.Gold;
            return sim.Simulation.EconomyLedger.ValidateConservation(stalls, agents, out _);
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
