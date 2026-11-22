using System;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The Clear order's mechanism on the real floor (Epic 10 Part 3, K7.1–K7.2, D38–D43), the preconditions set by hand:
    /// the warren charted, four signed on and put in place, the guild's stage machine driven tick by tick without the
    /// members' own decisions. A clear names a charted lair with a creature in it; the fight begins at the lair's reach,
    /// with the party's focus in every member's order, and ends when the lair is empty (the members thanked, haul or not)
    /// or by the retreat rule; the guild sends the healthiest mate for a fallen one; under orders a member fights on below
    /// its own line. The live outcome, with nothing injected, is <see cref="ClearScenarioTests"/>.
    /// </summary>
    public class ClearOrderTests
    {
        private sealed class Outpost : IDisposable
        {
            public LocalSimulation Sim;
            public IGuildHost Host;
            public GuildService Guild;
            public AgentState[] Members;
            public int Warren;
            public Vec2 Heart;
            public Vec2 Home;
            public uint Tick = 1;

            public void Dispose() => Sim.Dispose();

            public void Step(uint ticks = 1)
            {
                for (uint i = 0; i < ticks; i++) Guild.TickExpedition(Host, Tick++);
            }

            public void PutAll(Vec2 where, float health = 100f)
            {
                for (int i = 0; i < Members.Length; i++)
                {
                    Members[i].Position = where + new Vec2(i * 0.5f, 0f);
                    Members[i].Hunger = 0f;
                    Members[i].Health = health;
                    Members[i].IsDowned = false;
                    Members[i].BleedsOutAt = 0;
                }
            }

            public DispatchExpeditionCommand Clear(int lair, RetreatRule rule = RetreatRule.Steady)
            {
                Guild.FormParty(Host, new FormPartyCommand(1, Members.Select(m => m.AgentId).ToArray()), Tick);
                return new DispatchExpeditionCommand(1, 0, 1, rule, 0, ExpeditionKind.Clear, (byte)lair);
            }

            public GuildParty Party => Guild.Parties[0];

            /// <summary>Sent, mustered and marched into the warren's reach: the fight.</summary>
            public void ToTheFight(RetreatRule rule = RetreatRule.Steady)
            {
                Assert.Equal(GuildRules.Check.Ok, Guild.Dispatch(Host, Clear(Warren, rule), Tick));
                PutAll(Home);
                Step();
                Assert.Equal(ExpeditionStage.March, Party.Expedition.Stage);
                PutAll(Heart + new Vec2(-1.5f, -1f));
                Step();
                Assert.Equal(ExpeditionStage.Fight, Party.Expedition.Stage);
            }
        }

        private static Outpost NewOutpost()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(20);
            var o = new Outpost { Sim = sim, Host = (IGuildHost)sim.Simulation, Guild = sim.Simulation.Guild, Members = new AgentState[4] };
            var lairs = sim.Simulation.Creatures.Lairs;
            for (int i = 0; i < lairs.Count; i++) if (lairs[i].Definition.Key == Floor1Layout.RatWarren) o.Warren = i;
            o.Heart = lairs[o.Warren].Definition.Position;
            o.Home = sim.Simulation.ActiveStalls[0].Position;
            ChartTheWarren(sim, o.Warren);
            var tuning = sim.Simulation.Economy.Guild;
            var residents = sim.Host.CrawlerStates.Where(s => !s.IsDrifter).Take(4).ToList();
            for (int i = 0; i < 4; i++)
            {
                residents[i].Memory.SetAffinity(WorldRegistry.PlayerEntityId, new AffinityRecord(60, 80, 0, 0, 1, 0));
                Assert.Equal(GuildRules.Check.Ok, o.Guild.Recruit(o.Host, residents[i].AgentId, tuning.MaxWage, 0));
                ExpeditionTests.NoLeadership(residents[i]); // the rule's own line, unsteadied by a captain (Epic 11, S5.1)
                o.Members[i] = residents[i];
            }
            return o;
        }

        // By the crawlers' own rule: the Overgrowth charted, and a resident held within sight of a rat until the warren is charted
        private static void ChartTheWarren(LocalSimulation sim, int warren)
        {
            sim.Sectors.SetExplored(Floor1Layout.OvergrowthSectorId, true);
            var watcher = sim.Host.CrawlerStates.Last(s => !s.IsDrifter);
            var home = watcher.Position;
            for (int t = 0; t < 200 && !sim.Simulation.Creatures.IsCharted(warren); t++)
            {
                watcher.Position = new Vec2(20f, 14f);
                sim.Step(1);
            }
            watcher.Position = home;
            Assert.True(sim.Simulation.Creatures.IsCharted(warren), "The warren is charted.");
        }

        [Fact]
        public void AClearNamesAChartedLairWithACreatureInIt()
        {
            using var o = NewOutpost();
            int hollow = 1 - o.Warren; // the beetles' hollow, uncharted on this seed this early
            Assert.False(o.Sim.Simulation.Creatures.IsCharted(hollow));
            Assert.Equal(GuildRules.Check.BadTarget, o.Guild.CanDispatch(o.Host, o.Clear(hollow)));
            Assert.Equal(GuildRules.Check.BadTarget, o.Guild.CanDispatch(o.Host, o.Clear(9)));

            Assert.Equal(GuildRules.Check.Ok, o.Guild.Dispatch(o.Host, o.Clear(o.Warren), o.Tick));
            var record = o.Party.Expedition;
            Assert.Equal(ExpeditionKind.Clear, record.Kind);
            Assert.Equal(o.Warren, record.LairIndex);
            Assert.Equal(Floor1Layout.OvergrowthSectorId, record.TargetSectorId);
            Assert.Equal(ItemType.Hide, record.HaulItem); // the rats' table's first item
            Assert.Equal(ExpeditionStage.Muster, record.Stage);
            Assert.Equal(ExpeditionKind.Clear, o.Guild.OrderFor(o.Members[0].AgentId).Kind);
        }

        [Fact]
        public void TheFightBeginsAtTheLairAndEndsWhenItIsEmpty()
        {
            using var o = NewOutpost();
            int[] trust = o.Members.Select(m => (int)m.Memory.GetAffinity(WorldRegistry.PlayerEntityId, o.Tick).Trust).ToArray();
            o.ToTheFight();
            o.Step(); // the focus is chosen on the fight's first tick
            var order = o.Guild.OrderFor(o.Members[0].AgentId);
            Assert.True(order.IsFighting);
            Assert.Equal(o.Warren, order.LairIndex);
            Assert.True(o.Host.TryGetCreature(order.Focus, out var focus) && focus.IsAlive && focus.LairIndex == o.Warren, "The party's focus is one of the lair's living creatures.");
            Assert.All(o.Members, m => Assert.Equal(order.Focus, o.Guild.OrderFor(m.AgentId).Focus));
            o.Step(5);
            Assert.Equal(order.Focus, o.Guild.OrderFor(o.Members[0].AgentId).Focus); // kept while it lives

            var world = (ICreatureWorld)o.Sim.Simulation;
            foreach (var rat in o.Sim.Simulation.Creatures.Creatures.Where(c => c.LairIndex == o.Warren && c.IsAlive).ToList())
                o.Sim.Simulation.Creatures.Slay(world, rat.Id, o.Tick); // by hand: the lair emptied
            o.Step();
            Assert.Equal(ExpeditionStage.Return, o.Party.Expedition.Stage);
            Assert.True(o.Party.Expedition.Cleared);
            Assert.False(o.Party.Expedition.TurnedBack);

            o.PutAll(o.Home);
            o.Step();
            Assert.False(o.Party.IsOut);
            for (int i = 0; i < o.Members.Length; i++)
                Assert.True(o.Members[i].Memory.GetAffinity(WorldRegistry.PlayerEntityId, o.Tick).Trust > trust[i], "A cleared lair is thanked as a haul (D43).");
        }

        /// <summary>A named creature in the lair is the party's focus first (Epic 11, S7.4, D34), the farthest though it stands.</summary>
        [Fact]
        public void TheNamedIsTheFocusFirst()
        {
            using var o = NewOutpost();
            var at = o.Heart + new Vec2(-1.5f, -1f);
            var rats = o.Sim.Simulation.Creatures.Creatures.Where(c => c.LairIndex == o.Warren && c.IsAlive && !c.Definition.IsBoss).ToList();
            Assert.True(rats.Count >= 2, "The warren holds rats besides its Matriarch.");
            var far = rats.OrderByDescending(c => (c.Position - at).SqrMagnitude).First();
            var fallen = o.Sim.Host.CrawlerStates.Where(s => !s.IsDrifter && !o.Members.Contains(s)).First(); // downed by hand: the rat takes its name
            ((ICreatureWorld)o.Sim.Simulation).Strike(o.Sim.Registry.GetEntity(fallen.AgentId), far.Id, new Simpiens.Simulation.Combat.SwingOutcome(true, 1f, 1000f), o.Tick);
            Assert.True(far.IsNamed);
            o.ToTheFight();
            o.Step();
            Assert.Equal(far.Id, o.Guild.OrderFor(o.Members[0].AgentId).Focus);
        }

        [Fact]
        public void HalfThePartyDownTurnsTheFightBack()
        {
            using var o = NewOutpost();
            o.ToTheFight(RetreatRule.Bold);
            o.Members[0].IsDowned = true; // by hand: one down is not enough
            o.Step();
            Assert.Equal(ExpeditionStage.Fight, o.Party.Expedition.Stage);
            o.Members[1].IsDowned = true; // two of four
            o.Step();
            Assert.Equal(ExpeditionStage.Return, o.Party.Expedition.Stage);
            Assert.True(o.Party.Expedition.TurnedBack);
        }

        [Fact]
        public void APartyWornPastItsRuleTurnsTheFightBack()
        {
            using var o = NewOutpost();
            o.ToTheFight(RetreatRule.Steady);
            foreach (var m in o.Members) m.Health = o.Sim.Simulation.Economy.Guild.Expedition.RetreatHealth(RetreatRule.Steady) - 1f; // by hand
            o.Step();
            Assert.Equal(ExpeditionStage.Return, o.Party.Expedition.Stage);
            Assert.True(o.Party.Expedition.TurnedBack);
        }

        [Fact]
        public void TheGuildSendsTheHealthiestMateForTheFallen()
        {
            using var o = NewOutpost();
            o.ToTheFight(RetreatRule.Bold);
            var fallen = o.Members[1];
            fallen.Health = 0f;
            fallen.IsDowned = true;
            fallen.BleedsOutAt = o.Tick + 1000; // by hand: collapsed in danger
            o.Members[0].Health = 50f;
            o.Members[2].Health = 90f;
            o.Members[3].Health = 95f;
            o.Step();
            Assert.Equal(fallen.AgentId, o.Party.Fallen);
            Assert.Equal(o.Members[3].AgentId, o.Party.Carrier);
            Assert.Equal(fallen.AgentId, o.Guild.OrderFor(o.Members[3].AgentId).Carry);
            Assert.Equal(default, o.Guild.OrderFor(o.Members[0].AgentId).Carry); // the rest hold
            Assert.Equal(default, o.Guild.OrderFor(o.Members[2].AgentId).Carry);

            o.Members[3].IsDowned = true; // its carrier falls too: the next healthiest goes
            o.Step();
            Assert.Equal(o.Members[2].AgentId, o.Party.Carrier);

            fallen.BleedsOutAt = 0; // carried out of danger
            o.Step();
            Assert.Equal(default, o.Party.Fallen);
            Assert.Equal(default, o.Guild.OrderFor(o.Members[2].AgentId).Carry);
        }

        [Fact]
        public void UnderOrdersAMemberFightsOnBelowItsOwnLine()
        {
            using var o = NewOutpost();
            Assert.Equal(Vitality.RetreatBelow, Vitality.FightLine(o.Guild.OrderFor(o.Members[0].AgentId)));
            o.ToTheFight();
            Assert.Equal(0f, Vitality.FightLine(o.Guild.OrderFor(o.Members[0].AgentId)));
        }
    }
}
