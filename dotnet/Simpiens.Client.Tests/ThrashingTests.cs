using Microsoft.Extensions.DependencyInjection;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Simulation;
using Simpiens.Testing.Sessions;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// No crawler flickers on screen (Epic CLIENT M2.8): Unity's supply-loop and resource-contention scenarios asserted
    /// no visual-state thrashing, judged on the state its views displayed. Here the anomaly detector judges the state the
    /// client shows, through its own <see cref="CrawlerAppearance"/>, on the outpost and on the crowded Dev Map pens.
    /// </summary>
    public class ThrashingTests
    {
        [Theory]
        [InlineData("outpost", 3000)]
        [InlineData("devmap", 1000)]
        public void NoCrawlerFlickersOnScreen(string sessionId, long ticks)
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed, services => services.AddSingleton<ISessionDefinition>(new CanaryPenSession()));
            sim.Respawn(sessionId == "devmap" ? CanaryPenSession.SessionId : OutpostSession.SessionId);
            var appearance = new CrawlerAppearance();
            var shown = AnomalyDetector.For(sim, (entity, tick) => appearance.Advance(entity.Id, entity.Agent.VisualState, WorldViewFrame.FlagsOf(entity.Agent), tick));
            var raw = AnomalyDetector.For(sim);
            for (long t = 0; t < ticks; t++)
            {
                sim.Step(1);
                shown.Observe(sim.Clock.CurrentTick);
                raw.Observe(sim.Clock.CurrentTick);
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"shown: {shown.Summary()}; the simulation's own states: {raw.ThrashingCount} flickers");
            Assert.True(shown.ThrashingCount == 0, $"A crawler flickered on screen: {shown.First}");
        }
    }
}
