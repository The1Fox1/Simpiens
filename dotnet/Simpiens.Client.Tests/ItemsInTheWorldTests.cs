using System.Collections.Generic;
using System.Linq;
using Simpiens.Core.Session;
using Simpiens.Entities;
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
    /// Items in the world (graphics V5.3): the stall stands at its place with the goods it holds on its counter, a crawler
    /// wears its hazard gear as a helmet, and one carrying salvage shows it over its head, a click on it picking the carrier.
    /// </summary>
    public class ItemsInTheWorldTests
    {
        public static TheoryData<string> Modes => new TheoryData<string> { "top-down", "isometric" };

        private static ViewMode Mode(string name) => name == "top-down" ? ViewMode.TopDown : ViewMode.Isometric;

        private static List<DrawItem> Draw(ViewMode mode, WorldViewFrames frames, LocalSimulation sim)
        {
            var scene = new WorldScene { Mode = mode };
            scene.Build(sim.Host.Current.Map, frames, 0f);
            return Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).ToList();
        }

        private static int Sprite(ViewMode mode, ArtId id, int frame = 0) => mode.Catalog.Sprite(id, frame);

        [Theory]
        [MemberData(nameof(Modes))]
        public void TheStallShowsTheGoodsItHolds(string name)
        {
            var mode = Mode(name);
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var stall = sim.Simulation.ActiveStalls[0];
            var stocked = Enumerable.Range(0, TradeStallData.GoodsCapacity).Select(stall.GoodAt).Where(g => g.Item != ItemType.None && g.Stock > 0).Take(4).ToList();
            Assert.NotEmpty(stocked);

            var items = Draw(mode, TestFrames.Of(sim), sim);
            var catalog = mode.Catalog;
            int stallSprite = Sprite(mode, catalog.Id(ArtKeys.PropStall));
            var counter = items.Single(i => i.SpriteIndex == stallSprite && i.X == stall.Position.X && i.Y == stall.Position.Y);
            var shown = items.Where(i => i.SortKey > counter.SortKey && DrawSortKey.Layer(i.SortKey) == DrawSortKey.Layer(counter.SortKey) && Vec2.Distance(new Vec2(i.X, i.Y), stall.Position) < 1f && i.SpriteIndex != stallSprite).Take(stocked.Count).ToList();
            var expected = stocked.Select(g => Sprite(mode, catalog.Picture(ItemCatalog.Get(g.Item).ArtKey, catalog.DisplayPicture))).ToList();
            Assert.Equal(expected, shown.Select(i => i.SpriteIndex).ToList());
            Assert.All(shown, i => Assert.Equal(catalog.DisplaySlots[shown.IndexOf(i)].Height, i.Z));

            // With nothing in stock, the counter stands bare
            var bare = stall;
            for (int g = 0; g < TradeStallData.GoodsCapacity; g++) if (bare.GoodAt(g).Item != ItemType.None) bare = bare.WithStock(bare.GoodAt(g).Item, 0);
            var empty = Draw(mode, TestFrames.Of(sim.Registry.ActiveEntities, sim.Sectors, 0, new[] { bare }), sim);
            Assert.Single(empty, i => i.SpriteIndex == stallSprite);
            Assert.DoesNotContain(empty, i => expected.Contains(i.SpriteIndex) && Vec2.Distance(new Vec2(i.X, i.Y), stall.Position) < 1f);
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void ACrawlerWearsItsGearAndShowsWhatItCarries(string name)
        {
            var mode = Mode(name);
            using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed);
            sim.Respawn(OutpostSession.SessionId);
            var crawler = sim.Host.CrawlerStates.First(c => sim.Sectors.IsExplored(sim.Sectors.GetSectorAt(sim.Registry.GetEntity(c.AgentId).Position).Id));
            crawler.Inventory.TryEquip(new ItemInstance(1, ItemType.HazardGear, 1, 30, durability: 100));
            Assert.True(crawler.Inventory.TryAddItem(new ItemInstance(2, ItemType.IronOre, 1, 8), out _));
            var frames = TestFrames.Of(sim);
            var scene = new WorldScene { Mode = mode };
            scene.Build(sim.Host.Current.Map, frames, 0f);
            var items = Enumerable.Range(0, scene.Items.Count).Select(i => scene.Items[i]).ToList();

            var catalog = mode.Catalog;
            var pose = scene.Appearance.Pose(crawler.AgentId, crawler.VisualState, CrawlerFlags.None, 0f);
            int bundleSprite = Sprite(mode, catalog.Picture("iron-ore", ItemPicture.Carried));
            var bundle = items.Single(i => i.SpriteIndex == bundleSprite && i.Pick >= 0); // top-down's stall shows its ore with the same marker, not pickable
            var body = items.Single(i => i.Pick >= 0 && scene.Items.PickedEntity(i.Pick) == crawler.AgentId && DrawSortKey.Role(i.SortKey) == DrawRole.Crawler && i.SpriteIndex != bundleSprite);
            Assert.Equal(Sprite(mode, catalog.Id(ArtKeys.CrawlerWearing(pose.State, "hazard-gear")), pose.Frame), body.SpriteIndex);

            Assert.Equal(body.Pick, bundle.Pick); // a click on it picks the carrier
            Assert.True(bundle.SortKey > body.SortKey, "the bundle is drawn over its carrier");
            Assert.Equal((body.X + catalog.CarryOffset.X, body.Y + catalog.CarryOffset.Y, catalog.CarryHeight), (bundle.X, bundle.Y, bundle.Z));

            // Without them, the plain crawler and nothing over it
            crawler.Inventory.Unequip(EquipmentSlot.Protection);
            crawler.Inventory.TryRemoveItem(ItemType.IronOre, 1);
            var plain = Draw(mode, TestFrames.Of(sim), sim);
            Assert.DoesNotContain(plain, i => i.SpriteIndex == bundle.SpriteIndex && i.Pick >= 0);
            Assert.Contains(plain, i => i.SpriteIndex == Sprite(mode, catalog.Id(ArtKeys.Crawler(pose.State)), pose.Frame));
        }
    }
}
