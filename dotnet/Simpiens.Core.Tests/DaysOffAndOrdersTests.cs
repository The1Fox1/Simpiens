using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Life;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Duty and a day off (Epic LIFE-1 plan 3, R.9, D36), live on the real outpost with the guild player sending expeditions and
    /// nothing injected: a member on its day off judges the muster's orders weighed 0.8, so it may stay behind; one who keeps to
    /// them is called in, its day a work day, and goes; and no resident on its day off goes into danger but under orders it kept
    /// (the day's checks, <see cref="OutpostWatch"/>).
    /// </summary>
    public sealed class DaysOffAndOrdersTests
    {
        /// <summary>A seed on which a member is called in from its day off within <see cref="Days"/> (picked by <see cref="MeasureDaysOffAndOrders"/>).</summary>
        private static readonly ulong Seed = ScenarioSeed.Or(5);

        private const int Days = 8;

        [Fact]
        public void AMemberCalledInFromItsDayOffGoes()
        {
            var run = Play(Seed, Days);
            TestContext.Current.TestOutputHelper?.WriteLine(run.ToString());
            Assert.True(run.DayFailures.Count == 0, "The days held: " + string.Join("; ", run.DayFailures));
            Assert.True(run.RestDays > 0, "Members drew days off.");
            Assert.True(run.CalledIn > 0, "A member on its day off kept to the guild's orders and was called in.");
            Assert.True(run.CalledInAndWent > 0, "One called in went out with its party.");
            Assert.Equal(0, run.RestDayInDanger);
        }

        /// <summary>The gate seeds for <see cref="Days"/> days: days off, members called in, members who stayed behind on one.</summary>
        [Fact(Explicit = true)]
        public void MeasureDaysOffAndOrders()
        {
            var seeds = OutpostWatch.GateSeeds;
            var runs = new Run[seeds.Length];
            System.Threading.Tasks.Parallel.For(0, seeds.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, i => runs[i] = Play(seeds[i], Days));
            for (int i = 0; i < seeds.Length; i++) TestContext.Current.TestOutputHelper?.WriteLine($"seed {seeds[i]}: {runs[i]}");
            TestContext.Current.TestOutputHelper?.WriteLine($"called in on {runs.Count(r => r.CalledInAndWent > 0)} of {seeds.Length} seeds; stayed behind on a day off {runs.Sum(r => r.StayedBehindOnADayOff)}; " +
                $"days held on {runs.Count(r => r.DayFailures.Count == 0)}");
        }

        private sealed class Run
        {
            public int RestDays, CalledIn, CalledInAndWent, StayedBehindOnADayOff, Trips, RestDayInDanger;
            public List<string> DayFailures = new List<string>();
            public override string ToString() =>
                $"{Trips} trips; member days off {RestDays}; called in {CalledIn} (went {CalledInAndWent}); stayed behind on a day off {StayedBehindOnADayOff}; on a day off in danger {RestDayInDanger}; " +
                (DayFailures.Count == 0 ? "days hold" : "days FAIL: " + string.Join("; ", DayFailures));
        }

        private static Run Play(ulong seed, int days)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            new GuildPlayer(sim, targetSize: 4).SendExpeditions(Floor1Layout.DrownedGallerySectorId, Floor1Layout.GallerySeamYield, RetreatRule.Steady);
            var watch = new OutpostWatch(sim);
            var run = new Run();
            var firstIntention = new Dictionary<(EntityId, long), DayIntention>();
            var calledIn = new HashSet<(EntityId, long)>();
            var went = new HashSet<(EntityId, long)>();
            var refused = new HashSet<(uint, EntityId)>();
            var trips = new HashSet<uint>();
            long horizon = days * (long)DayClock.TicksPerDay;
            while (sim.Clock.CurrentTick < horizon)
            {
                watch.Run(1);
                uint now = (uint)sim.Clock.CurrentTick;
                var guild = sim.Simulation.Guild;
                foreach (var crawler in sim.Host.CrawlerStates)
                {
                    if (crawler.IsDrifter || !guild.IsMember(crawler.AgentId)) continue;
                    long day = crawler.RoutineAt(now).Day;
                    var intention = crawler.IntentionAt(now);
                    if (crawler.Life.IntentionDay != day + 1) continue; // not drawn yet today
                    var key = (crawler.AgentId, day);
                    if (!firstIntention.ContainsKey(key))
                    {
                        firstIntention[key] = intention;
                        if (intention == DayIntention.Rest) run.RestDays++;
                    }
                    if (firstIntention[key] == DayIntention.Rest && intention == DayIntention.Work && calledIn.Add(key)) run.CalledIn++;
                    if (calledIn.Contains(key) && guild.IsOnExpedition(crawler.AgentId) && went.Add(key)) run.CalledInAndWent++;
                }
                foreach (var party in guild.Parties)
                {
                    var record = party.Expedition;
                    if (record.Stage == ExpeditionStage.None) continue;
                    trips.Add(record.DispatchTick);
                    for (int m = 0; m < party.Members.Count; m++)
                    {
                        var member = party.Members[m];
                        if (member.Status != ExpeditionMemberStatus.StayedBehind || !refused.Add((record.DispatchTick, member.CrawlerId))) continue;
                        var state = sim.Host.CrawlerStates.FirstOrDefault(c => c.AgentId == member.CrawlerId);
                        if (state != null && state.IntentionAt(now) == DayIntention.Rest) run.StayedBehindOnADayOff++;
                    }
                }
            }
            watch.Finish();
            run.Trips = trips.Count;
            run.RestDayInDanger = watch.RestDayInDanger;
            run.DayFailures = watch.DayFailures().Where(f => !f.Contains("Scout bounty") && !f.Contains("Gather bounty")).ToList();
            return run;
        }
    }
}
