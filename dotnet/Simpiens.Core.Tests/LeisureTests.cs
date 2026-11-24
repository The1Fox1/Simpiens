using System;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Planning;
using Simpiens.Cognition.Social;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Life;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// The leisure score on the outpost (Epic LIFE-1 plan 2, L2.4, D15): what the venues offer a crawler at the start; each
    /// factor moving the score its way (its nature, the walk, a friend there, company for a game of two); a closed, full or
    /// forbidden venue scoring nothing; the same inputs scoring the same; and nothing allocated by a choice.
    /// </summary>
    public class LeisureTests
    {
        [Fact]
        public void TheOutpostOffersItsVenues()
        {
            using var sim = Outpost();
            var crawler = Resident(sim);
            var context = Context(sim, crawler);
            Assert.True(Leisure.TryChoose(context, out int venue, out var activity, out int seat, out float score), "Something to do on the outpost.");
            Assert.True(score > 0f && seat >= 0);
            Assert.Contains(sim.Host.Current.Map.Venues[venue].Key, new[] { Floor1Layout.StallVenue, Floor1Layout.GardenVenue });
            Assert.Contains(activity, new[] { Activity.Browse, Activity.Walk });
            Assert.Equal(0f, Leisure.Score(context, Venue(sim, Floor1Layout.YardVenue), Activity.Spar, out _)); // no drill yard yet
            Assert.Equal(0f, Leisure.Score(context, Venue(sim, Floor1Layout.HearthVenue), Activity.Fireside, out _)); // company's, not Recreate's
            Assert.Equal(0f, Leisure.Score(context, Venue(sim, Floor1Layout.GardenVenue), Activity.Browse, out _)); // not offered there
        }

        [Fact]
        public void ANatureTheWalkAndAFriendMoveTheScore()
        {
            using var sim = Outpost();
            var crawler = Resident(sim);
            int garden = Venue(sim, Floor1Layout.GardenVenue);

            // Its nature: the quiet lean toward a walk, the sociable away from it
            crawler.Personality = new AgentPersonality(0f, 0f, -1f, 0f, 0f);
            float quiet = Leisure.Score(Context(sim, crawler), garden, Activity.Walk, out _);
            crawler.Personality = new AgentPersonality(0f, 0f, 1f, 0f, 0f);
            float sociable = Leisure.Score(Context(sim, crawler), garden, Activity.Walk, out _);
            Assert.True(quiet > 0f && Math.Abs(quiet / sociable - 3f) < 0.01f, $"Solitary 1.5 against sociable 0.5 ({quiet:F3}, {sociable:F3}).");
            crawler.Personality = new AgentPersonality(0f, 0f, 0f, 0f, 0f);

            // The walk there: from the bunkhouse, and from the Garden's door (in the tavern, since T.2)
            crawler.Position = new Vec2(-4f, -9f);
            sim.Step(1);
            crawler.Position = new Vec2(-4f, -9f);
            float far = Leisure.Score(Context(sim, crawler), garden, Activity.Walk, out _);
            crawler.Position = new Vec2(4f, 17f);
            float near = Leisure.Score(Context(sim, crawler), garden, Activity.Walk, out _);
            Assert.True(near > far, $"Nearer scores higher ({near:F3} against {far:F3}).");

            // A warm friend in the Garden
            var friend = sim.Host.CrawlerStates.First(c => !c.IsDrifter && c != crawler);
            crawler.Memory.SetAffinity(friend.AgentId, new AffinityRecord(40, 40, 0, 0, 1, (uint)sim.Clock.CurrentTick));
            float alone = Leisure.Score(Context(sim, crawler), garden, Activity.Walk, out _);
            friend.Position = new Vec2(-3f, 25f);
            sim.Step(1);
            crawler.Position = new Vec2(4f, 17f);
            float withFriend = Leisure.Score(Context(sim, crawler), garden, Activity.Walk, out _);
            Assert.True(Math.Abs(withFriend / alone - Leisure.FriendsPresent) < 0.01f, $"A friend there: x{Leisure.FriendsPresent} ({withFriend:F3} against {alone:F3}).");
        }

        [Fact]
        public void AFullClosedOrForbiddenVenueScoresNothing()
        {
            using var sim = Outpost();
            var crawlers = sim.Host.CrawlerStates.Where(c => !c.IsDrifter).Take(6).ToArray();
            var map = sim.Host.Current.Map;
            int stall = Venue(sim, Floor1Layout.StallVenue), yard = Venue(sim, Floor1Layout.YardVenue);

            // The stall's four spots taken
            for (int i = 0; i < 4; i++) crawlers[i].Position = map.Venues[stall].Seats[i];
            sim.Step(1);
            for (int i = 0; i < 4; i++) crawlers[i].Position = map.Venues[stall].Seats[i];
            Assert.Equal(0f, Leisure.Score(Context(sim, crawlers[4]), stall, Activity.Browse, out int none));
            Assert.Equal(-1, none);

            // The yard: the drill yard built, but only for the guild's members, and sparring needs another there
            var workshops = sim.Simulation.Workshops;
            var host = (IGuildHost)sim.Simulation;
            host.UpdateStall(sim.Simulation.ActiveStalls[0].WithTreasury(1000));
            Assert.Equal(Simpiens.Simulation.Workshops.WorkshopRules.Check.Ok, workshops.Build((Simpiens.Simulation.Workshops.IWorkshopHost)sim.Simulation, workshops.IndexOf(Floor1Layout.DrillYard), (uint)sim.Clock.CurrentTick));
            var member = crawlers[5];
            member.Position = map.Venues[yard].Position;
            sim.Step(1);
            member.Position = map.Venues[yard].Position;
            Assert.True(Context(sim, member).Snapshot.IsVenueOpen(yard), "The yard opens with the drill yard.");
            Assert.Equal(0f, Leisure.Score(Context(sim, member), yard, Activity.Spar, out _)); // not a member
            uint tick = (uint)sim.Clock.CurrentTick;
            var guild = sim.Simulation.Guild;
            foreach (var c in new[] { member, crawlers[4] })
            {
                c.Memory.SetAffinity(Simpiens.Simulation.WorldRegistry.PlayerEntityId, new AffinityRecord(60, 80, 0, 0, 1, tick));
                Assert.Equal(GuildRules.Check.Ok, guild.Recruit(host, c.AgentId, GuildRules.ReservationWage(c, sim.Simulation.Economy.Guild), tick));
            }
            sim.Step(1);
            member.Position = map.Venues[yard].Position;
            Assert.Equal(0f, Leisure.Score(Context(sim, member), yard, Activity.Spar, out _)); // a member, but alone
            crawlers[4].Position = map.Venues[yard].Seats[1];
            sim.Step(1);
            member.Position = map.Venues[yard].Position;
            crawlers[4].Position = map.Venues[yard].Seats[1];
            Assert.True(Leisure.Score(Context(sim, member), yard, Activity.Spar, out int seat) > 0f, "Two members at the yard: a bout.");
            Assert.Equal(0, seat); // the other spot is taken
        }

        [Fact]
        public void TheScoreIsPureAndAllocatesNothing()
        {
            using var sim = Outpost();
            var context = Context(sim, Resident(sim));
            int garden = Venue(sim, Floor1Layout.GardenVenue);
            float first = Leisure.Score(context, garden, Activity.Walk, out int seat);
            Leisure.TryChoose(context, out _, out _, out _, out _);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) Leisure.TryChoose(context, out _, out _, out _, out _);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(first, Leisure.Score(context, garden, Activity.Walk, out int again));
            Assert.Equal(seat, again);
            Assert.Equal(0L, allocated);
        }

        private static LocalSimulation Outpost()
        {
            var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            sim.Step(1);
            return sim;
        }

        private static AgentState Resident(LocalSimulation sim) => sim.Host.CrawlerStates.First(c => !c.IsDrifter);

        private static AgentContext Context(LocalSimulation sim, AgentState crawler)
            => crawler.CreateContext(((ISpatialPartition)sim.Services.GetService(typeof(ISpatialPartition))).GetActiveSnapshot(), (uint)sim.Clock.CurrentTick);

        private static int Venue(LocalSimulation sim, string key) => sim.Host.Current.Map.Venues.ToList().FindIndex(v => v.Key == key);
    }
}
