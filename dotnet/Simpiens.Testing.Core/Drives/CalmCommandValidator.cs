using System;
using Simpiens.Entities;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Testing.Environment;

namespace Simpiens.Testing.Drives
{
    /// <summary>
    /// Epic CLIENT M2.2: the dossier's Calm Frustration is a typed command applied on the tick, as every player action
    /// is (ROADMAP §1A.1). Unity's dashboard wrote the crawler's state directly; the effect is the same: frustration to
    /// zero, a tantrum or an exhaustion collapse ended, the crawler shown idle.
    /// </summary>
    public class CalmCommandValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[CalmCommandValidator] >>> Starting the calm command suite <<<");
            ValidateCalmAppliesOnTheTick();
            ValidateCalmEndsATantrumAndACollapse();
            ValidateAnUnknownCrawlerChangesNothing();
            ValidateTheLegacyPayloadParses();
            TestLog.Log("[CalmCommandValidator] >>> ALL 4 TESTS PASSED <<<");
        }

        private static (SimulationManager sim, PlayerInterventionService queue, TestCrawler crawler) Setup()
        {
            var registry = new WorldRegistry();
            var queue = new PlayerInterventionService();
            var sim = new SimulationManager(registry, null, interventionService: queue);
            var crawler = HeadlessEntities.Crawler(registry, sim, null, Vec2.Zero, new EntityId(7u, 7u, 7u, 7u));
            return (sim, queue, crawler);
        }

        public void ValidateCalmAppliesOnTheTick()
        {
            var (sim, queue, crawler) = Setup();
            crawler.Agent.Frustration = 80f;
            queue.QueueInfluence(InfluenceEvent.CreateCalmCrawler("Player", crawler.Id));
            if (crawler.Agent.Frustration != 80f) throw new Exception("Queuing the command changed the crawler before the tick.");

            sim.Tick();
            if (crawler.Agent.Frustration != 0f) throw new Exception($"Expected frustration 0 after the tick, got {crawler.Agent.Frustration}.");
            TestLog.Log("  ✔ ValidateCalmAppliesOnTheTick passed: frustration 80 -> 0 on the tick, not before.");
        }

        public void ValidateCalmEndsATantrumAndACollapse()
        {
            var (sim, queue, crawler) = Setup();
            crawler.Agent.Frustration = 100f;
            crawler.Agent.TantrumTimer = 5f;
            crawler.Agent.IsExhaustionCollapsed = true;
            queue.QueueInfluence(InfluenceEvent.CreateCalmCrawler("Player", crawler.Id));
            sim.Tick();

            if (crawler.Agent.IsInTantrum) throw new Exception("The tantrum went on after a calm.");
            if (crawler.Agent.IsExhaustionCollapsed) throw new Exception("The collapse went on after a calm.");
            if (crawler.Agent.VisualState != AgentVisualState.Idle) throw new Exception($"Expected the crawler shown Idle, got {crawler.Agent.VisualState}.");
            TestLog.Log("  ✔ ValidateCalmEndsATantrumAndACollapse passed: tantrum and collapse ended, shown Idle.");
        }

        public void ValidateAnUnknownCrawlerChangesNothing()
        {
            var (sim, queue, crawler) = Setup();
            crawler.Agent.Frustration = 60f;
            queue.QueueInfluence(InfluenceEvent.CreateCalmCrawler("Player", new EntityId(1u, 2u, 3u, 4u)));
            sim.Tick();
            if (crawler.Agent.Frustration != 60f) throw new Exception("A calm for another id changed this crawler.");

            var bare = new SimulationManager(null, null, interventionService: queue); // no registry: refused, no throw
            queue.QueueInfluence(InfluenceEvent.CreateCalmCrawler("Player", crawler.Id));
            bare.Tick();
            TestLog.Log("  ✔ ValidateAnUnknownCrawlerChangesNothing passed: unknown ids and a missing registry are refused safely.");
        }

        public void ValidateTheLegacyPayloadParses()
        {
            var id = new EntityId(7u, 7u, 7u, 7u);
            var parsed = new InfluenceEvent("Tool", "CalmCrawler", id.ToString());
            if (parsed.CommandType != InfluenceCommandType.CalmCrawler || parsed.CalmCrawler.CrawlerId != id || parsed.LegacyCommandName != "CalmCrawler")
            {
                throw new Exception($"The string form didn't parse: {parsed.CommandType}, {parsed.CalmCrawler.CrawlerId}.");
            }
            TestLog.Log("  ✔ ValidateTheLegacyPayloadParses passed: \"CalmCrawler\" with an id parses to the typed command.");
        }
    }
}
