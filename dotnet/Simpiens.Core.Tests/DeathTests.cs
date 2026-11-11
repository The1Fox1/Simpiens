using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Creatures;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Loot;
using Simpiens.Simulation.People;
using Simpiens.Simulation.Persistence;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// A death (Epic 10 Part 3, K5.4a–b, A2), on the real outpost: mechanism tests, the death called by hand. The dead leaves
    /// the world the drifter's way, at the end of the tick, into the departed list with its cause; its body is a pile of its
    /// pack, its gear and its purse, which the ledger holds as gold on the ground until someone takes it or it fades; a guild
    /// member's contract and kept bunk end; a load puts the body, the gold on the ground and the cause back.
    /// </summary>
    public class DeathTests
    {
        [Fact]
        public void TheDeadLeaveABodyAndTheWorld()
        {
            using var sim = Outpost();
            var dead = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            Assert.True(dead.Inventory.TryEquip(Outfitting.NewPiece(ItemType.IronBlade, Grades.Standard)));
            Assert.True(dead.Inventory.TryAddItem(new ItemInstance(1, ItemType.Hide, 2, 0), out _));
            int gold = dead.Inventory.Gold;
            Assert.True(gold > 0);
            var at = dead.Position;
            var node = sim.Registry.GetEntity(dead.AgentId);
            int before = sim.Host.CrawlerStates.Count;
            Assert.True(Balanced(sim));

            sim.Simulation.Die(node, DeathCause.Creature, (byte)CreatureKind.StoneBeetle);
            Assert.True(Balanced(sim), "Its purse lies on the ground, still counted.");
            Assert.Equal((uint)gold, sim.Simulation.EconomyLedger.TotalGroundGold);
            sim.Step(1);

            Assert.Equal(before - 1, sim.Host.CrawlerStates.Count);
            Assert.DoesNotContain(sim.Host.CrawlerStates, s => s.AgentId == dead.AgentId);
            Assert.Null(sim.Registry.GetEntity(dead.AgentId));
            var departed = sim.Host.Departed.Last();
            Assert.Equal(dead.AgentId, departed.Id);
            Assert.True(departed.Died);
            Assert.Equal(DeathCause.Creature, departed.Cause);
            Assert.Equal((byte)CreatureKind.StoneBeetle, departed.CauseDetail);

            var body = sim.Simulation.Piles.Piles.Single(p => p.BodyOf == dead.AgentId);
            Assert.Equal(at, body.Position);
            Assert.Equal(default, body.ClaimedFor); // anyone's
            Assert.Equal(gold, body.Gold);
            var stacks = Enumerable.Range(0, body.Count).Select(i => body.StackAt(i)).ToList();
            Assert.Contains(stacks, s => s.Type == ItemType.IronBlade && s.Durability > 0);
            Assert.Contains(stacks, s => s.Type == ItemType.Hide && s.Quantity == 2);

            var told = new List<ChronicleEvent>();
            sim.Chronicle.GetAllRecentEvents(told, 64);
            Assert.Contains(told, e => e.EventType == ChronicleEventType.CrawlerDied && e.ActorAId == dead.AgentId && (int)e.ExtraData == (int)DeathCause.Creature * 256 + (int)CreatureKind.StoneBeetle);
        }

        [Fact]
        public void GoldOnTheGroundIsCountedTakenAndFaded()
        {
            using var sim = Outpost();
            var dead = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            var taker = sim.Host.CrawlerStates.Last(s => !s.IsDrifter);
            int gold = dead.Inventory.Gold;
            sim.Simulation.Die(sim.Registry.GetEntity(dead.AgentId), DeathCause.Hazard, 1);
            var body = sim.Simulation.Piles.Piles.Single(p => p.BodyOf == dead.AgentId);
            var world = (IPileWorld)sim.Simulation;
            int purse = taker.Inventory.Gold;
            sim.Simulation.Piles.Take(world, body.Id, taker.Inventory, taker.AgentId, (uint)sim.Clock.CurrentTick, out int taken, out _);
            Assert.Equal(gold, taken);
            Assert.Equal(purse + gold, taker.Inventory.Gold);
            Assert.Equal(0u, sim.Simulation.EconomyLedger.TotalGroundGold);
            Assert.True(Balanced(sim));
            Assert.True(sim.Simulation.Piles.TryGet(body.Id, out _), "A body picked clean still lies there until it fades.");

            // A purse left to fade leaves the economy
            var second = sim.Host.CrawlerStates.First(s => !s.IsDrifter && s.AgentId != dead.AgentId && s.Inventory.Gold > 0);
            int left = second.Inventory.Gold;
            uint sunk = sim.Simulation.EconomyLedger.TotalSinkedGold;
            sim.Simulation.Die(sim.Registry.GetEntity(second.AgentId), DeathCause.Hazard, 1);
            var other = sim.Simulation.Piles.Piles.Single(p => p.BodyOf == second.AgentId);
            sim.Simulation.Piles.Tick(world, other.FadesAt);
            Assert.False(sim.Simulation.Piles.TryGet(other.Id, out _));
            Assert.Equal(sunk + (uint)left, sim.Simulation.EconomyLedger.TotalSinkedGold);
            Assert.Equal((uint)left, sim.Simulation.EconomyLedger.GetSinkTotal(EconomyLedger.GroundFadeSinkName));
            Assert.True(Balanced(sim));
        }

        [Fact]
        public void AMembersDeathEndsItsContractAndBunk()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var player = new GuildPlayer(sim, targetSize: 1);
            for (int t = 0; t < 2 * DayClock.TicksPerDay && sim.Simulation.Guild.Members.Count == 0; t++) sim.Step(1);
            Assert.NotEmpty(sim.Simulation.Guild.Members);
            player.Stop();
            var member = sim.Simulation.Guild.Members[0].CrawlerId;
            sim.Simulation.Die(sim.Registry.GetEntity(member), DeathCause.Hazard, 1);
            sim.Step(1);
            Assert.False(sim.Simulation.Guild.IsMember(member));
            Assert.True(sim.Simulation.Guild.KeptBunkFor(member).BunkId == default, "Its kept bunk is free.");
            Assert.True(sim.Simulation.Groups.GetMemberships(member).Count == 0, "On no roster.");
        }

        [Fact]
        public void ADeadLeaderIsSucceeded()
        {
            // By hand: a settlement led by one crawler with two more (none forms on its own in a day of the outpost)
            using var sim = Outpost();
            var residents = sim.Host.CrawlerStates.Where(s => !s.IsDrifter).Take(3).ToList();
            var groups = sim.Simulation.Groups;
            byte group = groups.CreateGroup(Simpiens.Cognition.Groups.GroupType.Settlement, residents[0].AgentId, residents[0].Position);
            Assert.True(group != 0);
            Assert.True(groups.AddMember(group, residents[1].AgentId) && groups.AddMember(group, residents[2].AgentId));
            var leader = residents[0].AgentId;
            sim.Simulation.Die(sim.Registry.GetEntity(leader), DeathCause.Hazard, 1);
            sim.Step(60); // succession is judged every 50 ticks
            Assert.True(groups.TryGetGroup(group, out var after), "The group outlives its leader.");
            Assert.DoesNotContain(leader, groups.GetMembers(group));
            Assert.True(after.LeaderId != default && after.LeaderId != leader, "Another leads it.");
        }

        [Fact]
        public void FriendsGrieve()
        {
            using var sim = Outpost();
            var dead = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            var friend = sim.Host.CrawlerStates.First(s => !s.IsDrifter && s != dead);
            var stranger = sim.Host.CrawlerStates.Last(s => !s.IsDrifter && s != dead && s != friend);
            uint tick = (uint)sim.Clock.CurrentTick;
            friend.Memory.RecordInteraction(dead.AgentId, deltaWarmth: 60, deltaTrust: 0, deltaFear: 0, deltaRespect: 0, tick);
            float friendBefore = friend.Frustration, strangerBefore = stranger.Frustration;
            bool strangerFriend = stranger.Memory.GetAffinity(dead.AgentId, tick).Warmth >= Simpiens.Cognition.Planning.Rescue.FriendWarmth;
            sim.Simulation.Die(sim.Registry.GetEntity(dead.AgentId), DeathCause.Hazard, 1);
            var memories = new List<Simpiens.Cognition.Memory.MemoryEvent>();
            friend.Memory.EventLedger.GetRecentEvents((uint)sim.Clock.CurrentTick, 100, memories);
            Assert.Contains(memories, e => e.EventType == Simpiens.Cognition.Memory.MemoryEventType.Grieved && e.SubjectId == dead.AgentId);
            Assert.Equal(SimMath.Min(100f, friendBefore + SimulationManager.GriefFrustration), friend.Frustration, 3);
            if (!strangerFriend) Assert.Equal(strangerBefore, stranger.Frustration, 3);
        }

        [Fact]
        public void ADeathUnderOrdersCostsThePatronTrust()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var player = new GuildPlayer(sim, targetSize: 2);
            for (int t = 0; t < 3 * DayClock.TicksPerDay && sim.Simulation.Guild.Members.Count < 2; t++) sim.Step(1);
            Assert.Equal(2, sim.Simulation.Guild.Members.Count);
            player.Stop();
            var dead = sim.Host.CrawlerStates.First(c => c.AgentId == sim.Simulation.Guild.Members[0].CrawlerId);
            var other = sim.Host.CrawlerStates.First(c => c.AgentId == sim.Simulation.Guild.Members[1].CrawlerId);
            uint tick = (uint)sim.Clock.CurrentTick;
            int trust = other.Memory.GetAffinity(WorldRegistry.PlayerEntityId, tick).Trust;
            // By hand: it dies on an expedition (the guild's orders)
            dead.Expedition = new ExpeditionOrder(Simpiens.Simulation.Guild.ExpeditionStage.Work, 1, default, dead.Position);
            sim.Simulation.Die(sim.Registry.GetEntity(dead.AgentId), DeathCause.Hazard, 1);
            Assert.Equal(System.Math.Max(-100, trust + SimulationManager.TrustLostUnderOrders), other.Memory.GetAffinity(WorldRegistry.PlayerEntityId, tick).Trust);
        }

        [Fact]
        public void ANewcomerSettlesTwoDaysAfterAResidentsDeath()
        {
            using var sim = Outpost();
            var dead = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            int residents = sim.Host.CrawlerStates.Count(s => !s.IsDrifter);
            sim.Simulation.Die(sim.Registry.GetEntity(dead.AgentId), DeathCause.Hazard, 1);
            sim.Step(1);
            Assert.Single(sim.Host.NewcomersDue);
            uint due = sim.Host.NewcomersDue[0];
            Assert.Equal((uint)sim.Clock.CurrentTick + DayClock.Hours(SessionHost.NewcomerAfterHours), due); // from the tick it was removed
            sim.RunTo(due - 1);
            Assert.Equal(0, sim.Host.NewcomersArrived);
            sim.RunTo(due + 50);
            Assert.Equal(1, sim.Host.NewcomersArrived);
            Assert.Empty(sim.Host.NewcomersDue);
            int index = sim.Host.CrawlerNames.ToList().FindIndex(n => n == "Newcomer_1");
            Assert.True(index >= 0, "The newcomer lives at the outpost.");
            var newcomer = sim.Host.CrawlerStates[index];
            Assert.False(newcomer.IsDrifter);
            Assert.False(string.IsNullOrEmpty(newcomer.GivenName));
            Assert.NotEqual(dead.GivenName, newcomer.GivenName);
            Assert.True(Balanced(sim), "Its purse is a faucet.");
            var told = new List<ChronicleEvent>();
            sim.Chronicle.GetAllRecentEvents(told, 256);
            Assert.Contains(told, e => e.EventType == ChronicleEventType.NewcomerArrived && e.ActorAId == newcomer.AgentId);
        }

        [Fact]
        public void ALoadPutsTheDeadAndTheirBodiesBack()
        {
            using var sim = Outpost();
            var dead = sim.Host.CrawlerStates.First(s => !s.IsDrifter);
            sim.Simulation.Die(sim.Registry.GetEntity(dead.AgentId), DeathCause.Creature, (byte)CreatureKind.BurrowRat);
            sim.Step(2);
            byte[] saved = SaveSerializer.Serialize(sim.Host.Capture());
            uint ground = sim.Simulation.EconomyLedger.TotalGroundGold;
            sim.Step(30);
            Assert.True(sim.Host.TryRestore(SaveSerializer.Deserialize<SessionSave>(saved), out string refusal), refusal);
            Assert.Equal(saved, SaveSerializer.Serialize(sim.Host.Capture()));
            var departed = sim.Host.Departed.Single(d => d.Id == dead.AgentId);
            Assert.Equal(DeathCause.Creature, departed.Cause);
            Assert.Equal((byte)CreatureKind.BurrowRat, departed.CauseDetail);
            Assert.True(departed.Tick > 0);
            Assert.Contains(sim.Simulation.Piles.Piles, p => p.BodyOf == dead.AgentId);
            Assert.Equal(ground, sim.Simulation.EconomyLedger.TotalGroundGold);
            Assert.Single(sim.Host.NewcomersDue); // a newcomer still due (K5.6)
            Assert.True(Balanced(sim));
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
