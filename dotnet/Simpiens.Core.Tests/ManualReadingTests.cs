using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Skills;
using Xunit;

namespace Simpiens.Core.Tests
{
    /// <summary>
    /// Manuals bought and read (Epic 11, S3.3, Q10, D11), on the real outpost with nothing injected: the caravan shelves the
    /// primers, residents who love a skill below a primer's ceiling buy one from a spare purse and read it at the hearth, and
    /// nobody without a passion for the skill reads one; the ledger stays balanced and the days hold.
    /// </summary>
    public class ManualReadingTests
    {
        [Fact]
        public void ResidentsBuyAPrimerForASkillTheyLoveAndReadIt()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var watch = new OutpostWatch(sim);
            var readings = new List<(AgentState Who, Skill Skill, int Experience, int Ceiling)>();
            sim.Simulation.Practised += (who, skill, experience, source) =>
            {
                if (source.Kind == PracticeKind.Manual) readings.Add((who, skill, experience, source.Ceiling));
            };
            for (long t = 0; t < 2 * DayClock.TicksPerDay && readings.Count == 0; t += 100) watch.Run(100);
            watch.Finish();
            TestContext.Current.TestOutputHelper?.WriteLine(string.Join("\n", readings.Select(r => $"{r.Who.GivenName} read {r.Skill} for {r.Experience} (to {r.Ceiling}), {r.Who.Traits.PassionFor(r.Skill)}")) + $"\nat t{sim.Clock.CurrentTick}\n{watch.Report()}");
            Assert.NotEmpty(readings);
            Assert.All(readings, r => Assert.NotEqual(Passion.None, r.Who.Traits.PassionFor(r.Skill))); // passions only
            Assert.All(readings, r => Assert.Equal(ManualBlock.PrimerCeiling, r.Ceiling));
            Assert.Equal(0, watch.ConservationViolations);
            Assert.Equal(0, watch.IntentFaults);
        }
    }
}
