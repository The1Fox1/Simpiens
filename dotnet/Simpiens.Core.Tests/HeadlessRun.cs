using System.Collections.Generic;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Simulation;
using Simpiens.Testing.Environment;

namespace Simpiens.Core.Tests
{
    /// <summary>Headless runs of the outpost, sampled the way Unity's GoldenWorldHashScenario samples them.</summary>
    internal static class HeadlessRun
    {
        /// <summary>
        /// The outpost respawned with <paramref name="seed"/>, hashed at t0 and every 100 ticks to t3,000. Each sample is
        /// taken on its tick, after the simulation's and the crawlers' ticks (the sampler subscribes last).
        /// </summary>
        public static List<WorldStateSample> GoldenSamples(ulong seed)
        {
            using var sim = LocalSimulation.Create(seed);
            sim.Respawn(OutpostSession.SessionId);
            var samples = new List<WorldStateSample>(32) { WorldStateHash.Capture(sim.Clock.CurrentTick, sim.Host, sim.Simulation, sim.Sectors) };
            sim.Clock.OnSimulationTick += tick =>
            {
                if (tick % GoldenWorldHash.SampleEveryTicks == 0 && tick <= GoldenWorldHash.HorizonTicks)
                {
                    samples.Add(WorldStateHash.Capture(tick, sim.Host, sim.Simulation, sim.Sectors));
                }
            };
            sim.RunTo(GoldenWorldHash.HorizonTicks);
            return samples;
        }
    }
}
