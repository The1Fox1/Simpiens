using System;
using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Testing.Environment;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Epic CLIENT M0's gate: the client doesn't change the simulation. Run through the client's own tick path (the
    /// game's update and its <see cref="SimulationDriver"/>), every baseline seed reproduces the golden world hashes
    /// sample for sample, at any frame pacing.
    /// </summary>
    public class GoldenInsideClientTests
    {
        public static IEnumerable<object[]> BaselineSeeds() =>
            GoldenWorldHash.Read(Repo.GoldenBaseline).Keys.OrderBy(s => s).Select(s => new object[] { s });

        [Theory]
        [MemberData(nameof(BaselineSeeds))]
        public void TheGameReproducesTheGoldenHashesFrameByFrame(ulong seed)
        {
            var baseline = GoldenWorldHash.Read(Repo.GoldenBaseline)[seed];
            // 100 ticks a frame: 30 frames to t3000, each one a full update and draw of the real game
            var game = Offscreen.Start(new ClientOptions { Seed = seed, TicksPerUpdate = 100 }, frames: 0);
            var samples = Sample(game.Simulation);

            int frames = 0;
            while (game.Simulation.Clock.CurrentTick < GoldenWorldHash.HorizonTicks)
            {
                game.RunOneFrame();
                Assert.True(++frames <= 100, "the game stopped advancing the clock");
            }

            Assert.Equal(GoldenWorldHash.HorizonTicks, game.Simulation.Clock.CurrentTick);
            Assert.Equal(30, frames);
            AssertMatches(baseline, samples, seed);
        }

        [Fact]
        public void RealTimePacingGivesTheSameWorldAsFixedTicks()
        {
            // Uneven frames, a long hitch and a zero-length frame: only the number of whole ticks matters
            ulong seed = SimulationSettings.DefaultSeed;
            var baseline = GoldenWorldHash.Read(Repo.GoldenBaseline)[seed];
            using var sim = HeadlessSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            var samples = Sample(sim);
            var driver = new SimulationDriver(sim.Clock, ticksPerUpdate: 0);

            var frameMs = new[] { 16, 33, 7, 250, 0, 16, 1000, 17 };
            for (int i = 0; sim.Clock.CurrentTick < GoldenWorldHash.HorizonTicks; i++)
            {
                driver.Advance(TimeSpan.FromMilliseconds(frameMs[i % frameMs.Length]));
            }

            AssertMatches(baseline, samples, seed);
        }

        /// <summary>The outpost hashed at its current tick and every 100 ticks to t3000, sampled after every other subscriber.</summary>
        private static List<WorldStateSample> Sample(HeadlessSimulation sim)
        {
            var samples = new List<WorldStateSample>(32) { WorldStateHash.Capture(sim.Clock.CurrentTick, sim.Host, sim.Simulation, sim.Sectors) };
            sim.Clock.OnSimulationTick += tick =>
            {
                if (tick % GoldenWorldHash.SampleEveryTicks == 0 && tick <= GoldenWorldHash.HorizonTicks)
                {
                    samples.Add(WorldStateHash.Capture(tick, sim.Host, sim.Simulation, sim.Sectors));
                }
            };
            return samples;
        }

        private static void AssertMatches(GoldenWorldHash.Baseline baseline, List<WorldStateSample> samples, ulong seed)
        {
            Assert.Equal(GoldenWorldHash.HorizonTicks / GoldenWorldHash.SampleEveryTicks + 1, samples.Count);
            string difference = GoldenWorldHash.Compare(baseline, OutpostSession.SessionId, samples, out long tick);
            Assert.True(difference == null, $"Seed {seed}: first difference from the golden hashes at t{tick}: {difference}");
        }
    }
}
