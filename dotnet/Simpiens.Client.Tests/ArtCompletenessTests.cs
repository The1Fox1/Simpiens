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
            foreach (AgentVisualState state in System.Enum.GetValues(typeof(AgentVisualState))) used.Add(catalog.Id(ArtKeys.Crawler(state)));
            foreach (var item in ItemCatalog.All) used.Add(catalog.Node(item.ArtKey));
            var dead = catalog.Keys.Where(key => !used.Contains(catalog.Id(key))).ToList();
            Assert.True(dead.Count == 0, $"{mode}: rows nothing draws: {string.Join(", ", dead)}");
        }
    }
}
