using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Simpiens.Core.Session;
using Simpiens.Core.Session.Layouts;
using Simpiens.Entities;
using Simpiens.Hosting;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Simpiens.Simulation;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Spatial;
using Simpiens.Simulation.World;
using Simpiens.Testing.Sessions;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The art is complete (graphics V2): everything the frames carry that the scene draws has a visual in both view
    /// modes, every node on a shipped map has art of its own (not the missing art), and no catalogue row is dead.
    /// </summary>
    public class ArtCompletenessTests
    {
        public static TheoryData<string> Modes => new TheoryData<string> { "top-down", "isometric" };

        private static ArtCatalog Catalog(string mode) => mode == "top-down" ? ViewMode.TopDown.Catalog : ViewMode.Isometric.Catalog;

        /// <summary>The kinds the scene draws as entities; walls are the map's tiles, and the stall isn't an entity.</summary>
        private static readonly EntityType[] Drawn = { EntityType.Pawn, EntityType.Resource, EntityType.Anchor, EntityType.Bunk };

        [Theory]
        [MemberData(nameof(Modes))]
        public void EverythingTheFramesCarryHasArt(string mode)
        {
            var visuals = new EntityVisuals(Catalog(mode));
            foreach (var session in new[] { OutpostSession.SessionId, CanaryPenSession.SessionId })
            {
                using var sim = LocalSimulation.Create(SimulationSettings.DefaultSeed, s => s.AddSingleton<ISessionDefinition>(new CanaryPenSession()));
                sim.Respawn(session);
                var frame = TestFrames.Of(sim).Current;
                var seen = new HashSet<EntityType>();
                for (int i = 0; i < frame.Count; i++)
                {
                    ref readonly var entity = ref frame[i];
                    seen.Add(entity.Type);
                    if (entity.IsCrawler) Assert.False(visuals.Crawler(entity.VisualState).IsNone, $"{session}: a crawler {entity.VisualState} has no art.");
                    else Assert.True(visuals.TryProp(entity, out _) == Drawn.Contains(entity.Type), $"{session}: {entity.Type} at {entity.Position}.");
                }
                if (session == OutpostSession.SessionId) Assert.Equal(Drawn.ToHashSet(), seen.Intersect(Drawn).ToHashSet()); // Floor 1 has every kind
            }
            foreach (AgentVisualState state in System.Enum.GetValues(typeof(AgentVisualState))) Assert.False(visuals.Crawler(state).IsNone, state.ToString());
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void EveryNodeOnAShippedMapHasItsOwnArt(string mode)
        {
            var catalog = Catalog(mode);
            var fallback = catalog.Missing;
            foreach (var map in new[] { Floor1Layout.Definition, DevMapLayout.Definition })
            {
                Assert.NotEmpty(map.ResourceNodes);
                foreach (var node in map.ResourceNodes)
                    Assert.True(catalog.Node(ItemCatalog.Get(node.Item)?.ArtKey) != fallback, $"{map.Name}: node '{node.Key}' yields {node.Item}, which has no node art (it draws the missing art).");
            }
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void EveryCatalogueRowIsUsed(string mode)
        {
            var catalog = Catalog(mode);
            var used = new HashSet<ArtId>
            {
                catalog.Id(ArtKeys.TileFloor), catalog.Id(ArtKeys.TileRock), catalog.Id(ArtKeys.TileWall), catalog.Id(ArtKeys.TileWallLow),
                catalog.Id(ArtKeys.PropHearth), catalog.Id(ArtKeys.PropBunk), catalog.Id(ArtKeys.SelectionRing), catalog.Missing,
            };
            if (catalog.TryId(ArtKeys.CrawlerShadow, out var shadow)) used.Add(shadow);
            used.Add(catalog.Id(ArtKeys.PropStall));
            foreach (var key in WornSprites.Keys)
                foreach (AgentVisualState state in System.Enum.GetValues(typeof(AgentVisualState))) used.Add(catalog.Id(ArtKeys.CrawlerWearing(state, key)));
            foreach (AgentVisualState state in System.Enum.GetValues(typeof(AgentVisualState))) used.Add(catalog.Id(ArtKeys.Crawler(state)));
            foreach (var item in ItemCatalog.All)
            {
                used.Add(catalog.Node(item.ArtKey));
                used.Add(catalog.Picture(item.ArtKey, ItemPicture.Map));
                used.Add(catalog.Picture(item.ArtKey, ItemPicture.Carried));
            }
            var dead = catalog.Keys.Where(key => !used.Contains(catalog.Id(key))).ToList();
            Assert.True(dead.Count == 0, $"{mode}: rows nothing draws: {string.Join(", ", dead)}");
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void EveryItemCanStandInTheWorldAndBeCarried(string mode)
        {
            var catalog = Catalog(mode);
            foreach (var item in ItemCatalog.All)
            {
                foreach (var picture in new[] { ItemPicture.Map, ItemPicture.Carried })
                    Assert.True(catalog.Picture(item.ArtKey, picture) != catalog.Missing, $"{mode}: {item.Name} ('{item.ArtKey}') has no {picture} art.");
            }
        }

        [Fact]
        public void EveryItemHasAnIconAndTheIconsHaveNothingElse()
        {
            var icons = IconArtTable.Catalog;
            var used = new HashSet<ArtId> { icons.Missing };
            foreach (var item in ItemCatalog.All)
            {
                var icon = icons.Picture(item.ArtKey, ItemPicture.Icon);
                Assert.True(icon != icons.Missing, $"{item.Name} ('{item.ArtKey}') has no icon.");
                used.Add(icon);
            }
            Assert.Equal(icons.Count, used.Count);
        }

        /// <summary>Items in the world (graphics V5): each view mode has the stall, a worn variant of every crawler state for each item worn on the head, and four display slots.</summary>
        [Theory]
        [MemberData(nameof(Modes))]
        public void TheStallAndWornGearHaveArt(string mode)
        {
            var catalog = Catalog(mode);
            var visuals = new EntityVisuals(catalog);
            Assert.False(visuals.Stall.IsNone);
            Assert.Equal(4, catalog.DisplaySlots.Count);
            foreach (var (key, look) in ItemArt.All)
            {
                if (look.Worn != Worn.Head) continue;
                var item = ItemCatalog.All.Single(i => i.ArtKey == key).Id;
                foreach (AgentVisualState state in System.Enum.GetValues(typeof(AgentVisualState)))
                    Assert.NotEqual(visuals.Crawler(state), visuals.Crawler(state, item));
            }
            Assert.Equal(visuals.Crawler(AgentVisualState.Idle), visuals.Crawler(AgentVisualState.Idle, Simpiens.Simulation.Economy.ItemType.None));
        }
    }
}
