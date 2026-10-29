using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// Items in the world happen in the live game (graphics V5.4, the Definition of Done's spirit for views): the real
    /// outpost, nothing injected, followed tick by tick through its frames and drawn through the game's view. The stall
    /// shows its goods from the start; within the day, a crawler buys hazard gear and is drawn wearing it, and a crawler
    /// carries salvage and is drawn carrying it. The first tick of each is reported.
    /// </summary>
    public class LiveItemsInTheWorldTests
    {
        private const int Day = 9000; // the outpost's day (Epic MAP's DoD); G3's gear is bought within it

        [Fact]
        public void TheStallsGoodsWornGearAndCarriedSalvageShowInTheLiveOutpost()
        {
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            using var frames = new WorldViewFrames();
            frames.Follow(sim.Host, sim.Registry, sim.Sectors, sim.Clock, null, sim.Simulation);
            var mode = ViewMode.Isometric;
            var catalog = mode.Catalog;
            var map = sim.Host.Current.Map;
            var scene = new WorldScene { Mode = mode };

            // From the start: the stall, and its goods on its counter
            scene.Build(map, frames, 0f);
            var stallPosition = frames.Current.Stall(0).Position;
            int stallSprite = catalog.Sprite(catalog.Id(ArtKeys.PropStall), 0);
            var goodsSprites = ItemCatalog.All.Select(i => catalog.Sprite(catalog.Picture(i.ArtKey, catalog.DisplayPicture), 0)).ToHashSet();
            var drawn = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).ToList();
            Assert.Contains(drawn, i => i.SpriteIndex == stallSprite);
            Assert.Contains(drawn, i => goodsSprites.Contains(i.SpriteIndex) && Vec2.Distance(new Vec2(i.X, i.Y), stallPosition) < 1f);

            long wornAt = -1, carriedAt = -1;
            for (int t = 1; t <= Day && (wornAt < 0 || carriedAt < 0); t++)
            {
                sim.Step(1);
                var frame = frames.Current;
                bool someoneWears = false, someoneCarries = false;
                for (int i = 0; i < frame.Count; i++)
                {
                    someoneWears |= frame[i].IsCrawler && frame[i].Worn != ItemType.None;
                    someoneCarries |= frame[i].IsCrawler && frame[i].Carried != ItemType.None;
                }
                if (!(someoneWears && wornAt < 0) && !(someoneCarries && carriedAt < 0)) continue;

                // Draw it as the game would, and look for what the frame says should be there (the fog may still hide it)
                scene.Build(map, frames, t * 0.1f);
                drawn = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).ToList();
                for (int i = 0; i < frame.Count; i++)
                {
                    var crawler = frame[i];
                    if (!crawler.IsCrawler) continue;
                    var body = drawn.FirstOrDefault(d => d.Pick >= 0 && d.Z == 0f && scene.Items.PickedEntity(d.Pick) == crawler.Id);
                    if (body.Pick < 0 || body.Width == 0f) continue; // not drawn: under the fog
                    if (wornAt < 0 && crawler.Worn != ItemType.None)
                    {
                        var pose = scene.Appearance.Pose(crawler.Id, crawler.VisualState, crawler.Flags, t * 0.1f);
                        Assert.Equal(catalog.Sprite(catalog.Id(ArtKeys.CrawlerWearing(pose.State, ItemCatalog.Get(crawler.Worn).ArtKey)), pose.Frame), body.SpriteIndex);
                        wornAt = t;
                    }
                    if (carriedAt < 0 && crawler.Carried != ItemType.None)
                    {
                        int bundle = catalog.Sprite(catalog.Picture(ItemCatalog.Get(crawler.Carried).ArtKey, ItemPicture.Carried), 0);
                        Assert.Contains(drawn, d => d.SpriteIndex == bundle && d.Pick == body.Pick);
                        carriedAt = t;
                    }
                }
            }
            TestContext.Current.TestOutputHelper?.WriteLine($"First drawn: carried salvage at tick {carriedAt}, worn hazard gear at tick {wornAt} (of {Day}).");
            Assert.True(carriedAt > 0, $"No crawler was drawn carrying salvage within {Day} ticks.");
            Assert.True(wornAt > 0, $"No crawler was drawn wearing hazard gear within {Day} ticks.");
        }
    }
}
