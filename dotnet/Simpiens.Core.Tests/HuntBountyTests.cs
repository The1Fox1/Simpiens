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

        /// <summary>
        /// A hunt naming its quarry (Epic 11, S7.4, D34): refused for a creature not named, or not of the lair, or in a lair not
        /// charted; posted for a named rat of the charted warren, its record keeping the quarry.
        /// </summary>
        [Fact]
        public void ANamedHuntNamesANamedCreatureOfTheChartedLair()
        {
            using var sim = Outpost();
            int warren = WarrenIndex(sim), hollow = HollowIndex(sim);
            var rat = sim.Simulation.Creatures.Creatures.First(c => c.LairIndex == warren && !c.Definition.IsBoss);
            var beetle = sim.Simulation.Creatures.Creatures.First(c => c.LairIndex == hollow);
            Name(sim, beetle);
            Post(sim, hollow, 60, beetle.Id);
            Assert.Empty(sim.Simulation.Hunts); // the hollow isn't charted

            ChartTheWarren(sim);
            Post(sim, warren, 60, rat.Id);
            Assert.Empty(sim.Simulation.Hunts); // the rat has no name
            Name(sim, rat);
            Post(sim, hollow, 60, rat.Id);
            Assert.Empty(sim.Simulation.Hunts); // not the hollow's
            Post(sim, warren, 60, rat.Id);
            var hunt = Assert.Single(sim.Simulation.Hunts);
            Assert.Equal(rat.Id, hunt.Quarry);
            Assert.Equal(warren, hunt.LairIndex);
        }

        /// <summary>
        /// A hunt naming its quarry is paid when the quarry dies, by the damage each hunter dealt it (D34): a blow on another of the
        /// lair's creatures counts for nothing, and the lair's others slain leave it open.
        /// </summary>
        [Fact]
        public void ANamedHuntPaysOnItsQuarrysDeathByDamageToIt()
        {
            using var sim = Outpost();
            int warren = WarrenIndex(sim);
            ChartTheWarren(sim);
            var rats = sim.Simulation.Creatures.Creatures.Where(c => c.LairIndex == warren && !c.Definition.IsBoss).ToList();
            Assert.True(rats.Count >= 2, "The warren holds two rats besides its Matriarch.");
            var (quarry, other) = (rats[0], rats[1]);
            Name(sim, quarry);
            Post(sim, warren, 60, quarry.Id);
            var hunt = sim.Simulation.Hunts[0];
            var residents = sim.Host.CrawlerStates.Where(s => !s.IsDrifter && s.AgentId != quarry.Victim).Take(2).ToList();
            foreach (var r in residents) Assert.True(sim.Simulation.TryJoinHunt(hunt.BountyId, r.AgentId));
            var world = (ICreatureWorld)sim.Simulation;
            world.CreatureHit(residents[0].AgentId, quarry, 20f);
            world.CreatureHit(residents[1].AgentId, quarry, 20f);
            world.CreatureHit(residents[1].AgentId, other, 40f); // not its quarry: nothing
            Assert.Equal(20f, sim.Simulation.Hunts[0].Damage0);
            Assert.Equal(20f, sim.Simulation.Hunts[0].Damage1);
            int[] purses = residents.Select(r => r.Inventory.Gold).ToArray();

            uint tick = (uint)sim.Clock.CurrentTick;
            sim.Simulation.Creatures.Slay(world, other.Id, tick);
            sim.Step(1);
            Assert.Single(sim.Simulation.Hunts); // another slain: still open
            sim.Simulation.Creatures.Slay(world, quarry.Id, (uint)sim.Clock.CurrentTick);
            sim.Step(1);
            Assert.Empty(sim.Simulation.Hunts);
            Assert.True(sim.Simulation.Creatures.Lairs[warren].Alive > 0, "Paid with the warren not empty.");
            Assert.Equal(30, residents[0].Inventory.Gold - purses[0]);
            Assert.Equal(30, residents[1].Inventory.Gold - purses[1]);
            Assert.True(Balanced(sim), "The escrow went to the hunters.");
        }

        /// <summary>
        /// A hunter on a hunt naming its quarry goes for the quarry, though another of the lair's creatures is nearer, and is done
        /// once the quarry is dead, the lair not empty (Epic 11, S7.4, D34; <c>HuntIntentHandler</c>).
        /// </summary>
        [Fact]
        public void AHunterGoesForTheQuarry()
        {
            using var sim = Outpost();
            int warren = WarrenIndex(sim);
            ChartTheWarren(sim);
            var rats = sim.Simulation.Creatures.Creatures.Where(c => c.LairIndex == warren && !c.Definition.IsBoss).ToList();
            var (quarry, other) = (rats[0], rats[1]);
            Name(sim, quarry);
            Post(sim, warren, 60, quarry.Id);
            var hunt = sim.Simulation.Hunts[0];
            var hunter = sim.Host.CrawlerStates.First(s => !s.IsDrifter && !s.IsDowned);
            Assert.True(sim.Simulation.TryJoinHunt(hunt.BountyId, hunter.AgentId));
            var node = sim.Registry.GetEntity(hunter.AgentId);
            // By hand, on either side of the hunter at the warren's heart: the other rat nearer to the west, the quarry to the east,
            // both out of reach, so the way it steps says which it goes for
            var heart = sim.Simulation.Creatures.Lairs[warren].Definition.Position;
            node.SetPosition(heart);
            other.Position = heart + new Vec2(-1.5f, 0f);
            quarry.Position = heart + new Vec2(2.5f, 0f);
            hunter.Health = 100f;
            var handler = new Simpiens.Cognition.Planning.Handlers.HuntIntentHandler();
            var state = new ActiveIntentState { Intent = new Simpiens.Cognition.Contracts.HuntIntent(hunter.AgentId, hunt.BountyId, (byte)warren, null) };
            var sm = (SimulationManager)sim.Simulation;
            Assert.Equal(Simpiens.Cognition.Contracts.IntentResult.InProgress, handler.Execute(node, state, sm));
            Assert.True(node.Position.X > heart.X, $"It stepped east, toward its quarry, not west to the nearer rat: {heart} to {node.Position}.");

            sim.Simulation.Creatures.Slay((ICreatureWorld)sim.Simulation, quarry.Id, (uint)sim.Clock.CurrentTick);
            Assert.True(sim.Simulation.Creatures.Lairs[warren].Alive > 0);
            Assert.Equal(Simpiens.Cognition.Contracts.IntentResult.Success, handler.Execute(node, state, sm));
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
            Assert.Equal(80, System.Runtime.CompilerServices.Unsafe.SizeOf<HuntRecord>()); // its quarry since Epic 11's format 13 (S7.1); 64 before
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

        /// <summary>A load keeps a hunt's quarry (Epic 11, S7.4), byte for byte.</summary>
        [Fact]
        public void ALoadKeepsTheHuntsQuarry()
        {
            using var sim = Outpost();
            int warren = WarrenIndex(sim);
            ChartTheWarren(sim);
            var rat = sim.Simulation.Creatures.Creatures.First(c => c.LairIndex == warren && !c.Definition.IsBoss);
            Name(sim, rat);
            Post(sim, warren, 60, rat.Id);
            byte[] saved = Simpiens.Simulation.Persistence.SaveSerializer.Serialize(sim.Host.Capture());
            sim.Step(10);
            Assert.True(sim.Host.TryRestore(Simpiens.Simulation.Persistence.SaveSerializer.Deserialize<Simpiens.Simulation.Persistence.SessionSave>(saved), out string refusal), refusal);
            Assert.Equal(saved, Simpiens.Simulation.Persistence.SaveSerializer.Serialize(sim.Host.Capture()));
            Assert.Equal(rat.Id, Assert.Single(sim.Simulation.Hunts).Quarry);
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
        /// <summary>
        /// Gate seed 1 since Epic 11's S5.6 (F11): a bold resident at Apprentice in melee or better, given the glowstone edge
        /// and the ring coat, expects to clear her warren alone with health to spare (<see cref="MeasureWhoBravesTheWarrenOnTheGateSeeds"/>:
        /// on 11 of 23 gate seeds, 19 of 59 bold residents; melee 3 expected 57 harm of 60 before plan 2's techniques too), and
        /// on two more one grows into it within the day. The scenario holds on 12 of 23; the default seed's bold hold a melee
        /// talent since Leadership moved every draw; on this one, the first that holds, none do.
        /// </summary>
        private const ulong WarrenAloneSeed = 1;

        [Fact]
        public void NobodyHuntsTheMatriarchsWarrenAlone()
        {
            var (braves, joined, balanced, note) = WarrenAlone(WarrenAloneSeed);
            TestContext.Current.TestOutputHelper?.WriteLine(note);
            Assert.True(braves.Count == 0, "Nobody braves her warren alone: " + string.Join("; ", braves));
            Assert.False(joined, "Nobody joined a hunt on the Matriarch's warren.");
            Assert.True(balanced);
        }

        // The scenario on a seed: the warren charted, its bold residents armed with the best, a hunt posted on it, a day
        private static (System.Collections.Generic.List<string> Braves, bool Joined, bool Balanced, string Note) WarrenAlone(ulong seed)
        {
            using var sim = Outpost(seed);
            int warren = WarrenIndex(sim);
            ChartTheWarren(sim);
            var bold = ArmTheBold(sim);
            var lair = sim.Simulation.Creatures.Lairs[warren].Definition;
            Assert.True(lair.HasBoss && sim.Simulation.Creatures.Lairs[warren].MemberIn(0) != default, "The Matriarch heads the warren.");
            var braves = new System.Collections.Generic.List<string>();
            foreach (var b in bold)
                if (Vitality.BravesLair(lair, true, b.Skills, b.Inventory, b.Traits, b.Health, b.Personality.RiskTolerancePercent, b.IsDowned))
                    braves.Add($"{b.GivenName}: expects {Vitality.HarmToClear(lair, true, b.Skills, b.Inventory, b.Traits):0} harm against {b.Health - Vitality.RetreatBelow:0} to spare, melee {b.Skills.Level(Simpiens.Simulation.Skills.Skill.Melee)}");
            Post(sim, warren, 60);
            bool joined = false;
            EntityId hunter = default;
            for (long t = 0; t < DayClock.TicksPerDay && sim.Simulation.Hunts.Count > 0; t++)
            {
                sim.Step(1);
                if (sim.Simulation.Hunts.Count > 0 && sim.Simulation.Hunts[0].Count > 0 && !joined)
                {
                    joined = true;
                    hunter = sim.Simulation.Hunts[0].ClaimantAt(0);
                }
            }
            var who = hunter != default ? sim.Host.CrawlerStates.FirstOrDefault(c => c.AgentId == hunter) : null;
            string note = $"seed {seed}: {bold.Count} bold armed; braves {braves.Count}; joined {(joined ? $"yes ({who?.GivenName}, melee {who?.Skills.Level(Simpiens.Simulation.Skills.Skill.Melee)}, bold {who?.Personality.RiskTolerancePercent})" : "no")}";
            return (braves, joined, Balanced(sim), note);
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

        private static void Post(LocalSimulation sim, int lair, ushort reward, EntityId quarry = default)
        {
            var commands = (IPlayerInterventionService)sim.Services.GetService(typeof(IPlayerInterventionService));
            commands.QueueInfluence(InfluenceEvent.CreatePostBounty("Test", new PostBountyCommand(0, BountyType.Hunt, 0, ItemType.None, 1, reward,
                durationTicks: DayClock.Hours(48), lairIndex: (byte)lair, quarry: quarry)));
            sim.Step(1);
        }

        // A creature named by hand (Epic 11, S7.1): its blow downs the first resident still standing, through the simulation's
        // landing (the last is the warren's watcher)
        private static void Name(LocalSimulation sim, CreatureState creature)
        {
            var fallen = sim.Host.CrawlerStates.First(s => !s.IsDrifter && !s.IsDowned);
            ((ICreatureWorld)sim.Simulation).Strike(sim.Registry.GetEntity(fallen.AgentId), creature.Id, new Simpiens.Simulation.Combat.SwingOutcome(true, 1f, 1000f), (uint)sim.Clock.CurrentTick);
            Assert.True(creature.IsNamed);
        }

        private static int HollowIndex(LocalSimulation sim)
        {
            var lairs = sim.Simulation.Creatures.Lairs;
            for (int i = 0; i < lairs.Count; i++) if (lairs[i].Definition.Key == Floor1Layout.BeetleHollow) return i;
            return -1;
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

        /// <summary>
        /// The measurement behind <see cref="NobodyHuntsTheMatriarchsWarrenAlone"/> (Epic 11, S5.6): on each seed, every bold
        /// resident armed with the glowstone edge and the ring coat, and the harm it expects clearing her warren alone against
        /// what it has to spare. Explicit.
        /// </summary>
        [Fact(Explicit = true)]
        public void MeasureWhoBravesTheWarrenOnTheGateSeeds()
        {
            int seedsWithOne = 0, braves = 0, bold = 0;
            foreach (ulong seed in OutpostWatch.GateSeeds)
            {
                using var sim = Outpost(seed);
                int warren = WarrenIndex(sim);
                var lair = sim.Simulation.Creatures.Lairs[warren].Definition;
                var line = new System.Text.StringBuilder($"seed {seed}:");
                bool any = false;
                foreach (var b in sim.Host.CrawlerStates.Where(s => !s.IsDrifter && s.Personality.RiskTolerancePercent >= Vitality.VentureRiskTolerancePercent))
                {
                    b.Inventory.TryEquip(Simpiens.Simulation.Items.Outfitting.NewPiece(ItemType.GlowstoneEdge, Simpiens.Simulation.Items.Grades.Standard));
                    b.Inventory.TryEquip(Simpiens.Simulation.Items.Outfitting.NewPiece(ItemType.RingCoat, Simpiens.Simulation.Items.Grades.Standard));
                    bool brave = Vitality.BravesLair(lair, true, b.Skills, b.Inventory, b.Traits, b.Health, b.Personality.RiskTolerancePercent, b.IsDowned);
                    bold++;
                    if (brave) { braves++; any = true; }
                    line.Append($" {b.GivenName} melee {b.Skills.Level(Simpiens.Simulation.Skills.Skill.Melee)} harm {Vitality.HarmToClear(lair, true, b.Skills, b.Inventory, b.Traits):0}/{b.Health - Vitality.RetreatBelow:0}{(brave ? " BRAVES" : "")};");
                }
                if (any) seedsWithOne++;
                TestContext.Current.TestOutputHelper?.WriteLine(line.ToString());
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"of {OutpostWatch.GateSeeds.Length} gate seeds: a bold resident so armed braves her warren alone on {seedsWithOne} ({braves} of {bold} bold residents)");
            int held = 0;
            foreach (ulong seed in OutpostWatch.GateSeeds)
            {
                try
                {
                    var (brave, joined, balanced, note) = WarrenAlone(seed);
                    if (brave.Count == 0 && !joined && balanced) held++;
                    TestContext.Current.TestOutputHelper?.WriteLine(note + (brave.Count == 0 && !joined && balanced ? " HOLDS" : ""));
                }
                catch (Xunit.Sdk.XunitException e) { TestContext.Current.TestOutputHelper?.WriteLine($"seed {seed}: {e.Message.Split('\n')[0]}"); }
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"the scenario holds on {held} of {OutpostWatch.GateSeeds.Length} gate seeds");
        }

        private static LocalSimulation Outpost(ulong seed = SimulationSettings.DefaultSeed)
        {
            var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(20);
            return sim;
        }
    }
}
