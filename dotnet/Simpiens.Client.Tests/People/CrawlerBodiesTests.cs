using System;
using System.Collections.Generic;
using System.Linq;
using Simpiens.Cognition;
using Simpiens.Entities;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Presentation.Art.People;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.People;
using Simpiens.Simulation.Spatial;
using Xunit;

namespace Simpiens.Client.Tests.People
{
    /// <summary>
    /// The body region's table (Epic UI-Beta B2, D2): a slot per body, shared by crawlers that look alike, freed when no
    /// crawler shows it, queued for baking once; past the region's slots, today's figure; no allocation once baked.
    /// </summary>
    public class CrawlerBodiesTests
    {
        [Fact]
        public void EveryCrawlerWithALookGetsASlotAndOneBake()
        {
            var crawlers = Crawlers(12);
            crawlers.Add(Crawler(99, default)); // given no look: today's figure
            var bodies = new CrawlerBodies();
            bodies.Update(Frame(crawlers));

            Assert.Equal(12, bodies.InUse);
            Assert.Equal(12, bodies.PendingCount);
            Assert.Equal(0, bodies.Overflow);
            var frame = Frame(crawlers);
            var slots = new HashSet<int>();
            for (int i = 0; i < frame.Count; i++)
            {
                bool drawn = bodies.TryCell(frame[i], AgentVisualState.Walking, 1, out int sprite);
                Assert.Equal(frame[i].Look.IsComplete, drawn);
                if (!drawn) continue;
                Assert.True(BodySlots.IsSlot(SpriteSheets.World[sprite]), $"{SpriteSheets.World[sprite].Name} isn't in the body region.");
                slots.Add(sprite);
            }
            Assert.Equal(12, slots.Count);

            // Each seated slot is baked once, in seating order; a second update queues nothing
            var taken = new List<int>();
            while (bodies.TryTakePending(out int slot)) taken.Add(slot);
            Assert.Equal(Enumerable.Range(0, 12), taken);
            bodies.Update(frame);
            Assert.False(bodies.TryTakePending(out _));
        }

        [Fact]
        public void CrawlersThatLookAlikeShareASlot()
        {
            var crawlers = Crawlers(4);
            crawlers[3].Agent.Look = crawlers[0].Agent.Look;
            var bodies = new CrawlerBodies();
            var frame = Frame(crawlers);
            bodies.Update(frame);
            Assert.Equal(3, bodies.InUse);
            Assert.True(bodies.TryCell(frame[0], AgentVisualState.Idle, 0, out int a));
            Assert.True(bodies.TryCell(frame[3], AgentVisualState.Idle, 0, out int b));
            Assert.Equal(a, b);
        }

        [Fact]
        public void ASlotIsFreedWhenNoCrawlerShowsItAndAGearChangeTakesANewOne()
        {
            var crawlers = Crawlers(3);
            var bodies = new CrawlerBodies();
            bodies.Update(Frame(crawlers));
            Drain(bodies);

            // The second leaves; a newcomer takes its slot and is baked into it
            var leaving = crawlers[1];
            crawlers.RemoveAt(1);
            crawlers.Add(Crawler(50, CrawlerLook.Generate(7, new EntityId(50u, 0u, 0u, 0u))));
            bodies.Update(Frame(crawlers));
            Assert.Equal(3, bodies.InUse);
            Assert.True(bodies.TryTakePending(out int slot));
            Assert.Equal(1, slot);
            Assert.True(bodies.TryKeyIn(1, out var key) && key.Look == crawlers[2].Agent.Look);

            // Putting on a helm is another body: its old slot is freed first, so the helmed body is baked into it
            crawlers[0].Agent.Inventory.TryEquip(new ItemInstance(1, ItemType.HazardGear, 1, 30, durability: 100));
            bodies.Update(Frame(crawlers));
            Assert.Equal(3, bodies.InUse);
            Assert.True(bodies.TryTakePending(out slot));
            Assert.Equal(0, slot);
            Assert.True(bodies.TryKeyIn(slot, out key) && key.HeadGear == ItemType.HazardGear && key.Look == crawlers[0].Agent.Look);
            Assert.False(bodies.TryTakePending(out _));
        }

        [Fact]
        public void PastTheRegionCrawlersAreDrawnAsTodaysFigure()
        {
            var crawlers = Crawlers(BodySlots.Count + 5);
            var bodies = new CrawlerBodies();
            var frame = Frame(crawlers);
            bodies.Update(frame);
            Assert.Equal(BodySlots.Count, bodies.InUse);
            Assert.Equal(5, bodies.Overflow);
            Assert.False(bodies.TryCell(frame[frame.Count - 1], AgentVisualState.Idle, 0, out _));
        }

        [Fact]
        public void AnUpdateOnceBakedAllocatesNothing()
        {
            var frame = Frame(Crawlers(20));
            var bodies = new CrawlerBodies();
            bodies.Update(frame);
            Drain(bodies);
            bodies.Update(frame);
            bodies.TryCell(frame[0], AgentVisualState.Idle, 0, out _);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                bodies.Update(frame);
                for (int c = 0; c < frame.Count; c++) bodies.TryCell(frame[c], AgentVisualState.Walking, i & 1, out _);
            }
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        private static void Drain(CrawlerBodies bodies)
        {
            while (bodies.TryTakePending(out _)) { }
        }

        private static List<WorldEntity> Crawlers(int count)
        {
            var list = new List<WorldEntity>(count);
            for (uint i = 0; i < count; i++) list.Add(Crawler(i + 1, CrawlerLook.Generate(PeopleSheetTests.CrowdSeed, new EntityId(i + 1u, 0u, 0u, 0u))));
            return list;
        }

        private static WorldEntity Crawler(uint n, CrawlerLook look)
        {
            var id = new EntityId(n, 1u, 0u, 0u);
            return new WorldEntity(id, EntityType.Pawn, new Vec2(n, 0f), 0.3f, 1f, new AgentState(id) { Look = look });
        }

        private static WorldViewFrame Frame(List<WorldEntity> crawlers) => TestFrames.Of(crawlers, null, 1).Current;
    }
}
