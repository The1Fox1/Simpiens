using System;
using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Spatial;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// Which art each thing a frame carries is drawn with, in one catalogue (graphics V2): the one place that maps an
    /// entity to its visual. A prop by its kind (the hearth, a bunk), a resource node by its item's art key,
    /// a crawler by its shown state, a creature by its kind's art key (Epic 10 Part 3, K2.5). Kinds without a picture
    /// (walls, which the map's tiles draw; the stall, which isn't an entity) have none. Every lookup is an array read,
    /// or one dictionary probe for a node: no allocation.
    /// </summary>
    public sealed class EntityVisuals
    {
        private readonly ArtCatalog _catalog;
        private readonly ArtId[] _byType;
        private readonly ArtId[] _crawlers;
        private readonly ArtId[] _stations; // by kind, then built
        private readonly ArtId[] _creatures = new ArtId[Simpiens.Simulation.Creatures.CreatureCatalog.Capacity]; // by kind
        private readonly Dictionary<string, ArtId[]> _wearing = new Dictionary<string, ArtId[]>(StringComparer.Ordinal); // by the worn item's art key, then state

        public EntityVisuals(ArtCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            var types = (EntityType[])Enum.GetValues(typeof(EntityType));
            _byType = new ArtId[types.Length];
            for (int i = 0; i < _byType.Length; i++) _byType[i] = ArtId.None;
            _byType[(int)EntityType.Anchor] = catalog.Id(ArtKeys.PropHearth);
            _byType[(int)EntityType.Bunk] = catalog.Id(ArtKeys.PropBunk); // under its sleeper: the role orders a prop before a crawler
            _byType[(int)EntityType.GuildBunk] = catalog.Id(ArtKeys.PropBunk); // the barracks' bunks are bunks to look at (Epic 10 Part 2, W5.2)
            var states = (AgentVisualState[])Enum.GetValues(typeof(AgentVisualState));
            _crawlers = new ArtId[states.Length];
            foreach (var state in states) _crawlers[(int)state] = catalog.Id(ArtKeys.Crawler(state));
            foreach (var key in WornSprites.Keys)
            {
                var byState = new ArtId[states.Length];
                bool all = true;
                foreach (var state in states) all &= catalog.TryId(ArtKeys.CrawlerWearing(state, key), out byState[(int)state]);
                if (all) _wearing[key] = byState;
            }
            Stall = catalog.TryId(ArtKeys.PropStall, out var stall) ? stall : ArtId.None;
            for (int i = 0; i < _creatures.Length; i++) _creatures[i] = ArtId.None;
            foreach (var kind in Simpiens.Simulation.Creatures.CreatureCatalog.All)
                _creatures[(byte)kind.Id] = catalog.TryId(ArtKeys.Creature(kind.ArtKey), out var creature) ? creature : catalog.Missing;
            var kinds = (StationKind[])Enum.GetValues(typeof(StationKind));
            _stations = new ArtId[kinds.Length * 2];
            foreach (var kind in kinds)
            {
                _stations[(int)kind * 2] = catalog.TryId(ArtKeys.Station(kind, built: false), out var plot) ? plot : ArtId.None;
                _stations[(int)kind * 2 + 1] = catalog.TryId(ArtKeys.Station(kind, built: true), out var built) ? built : ArtId.None;
            }
        }

        /// <summary>A station's art (Epic 10 Part 2, W1.6a): its own once built, the plot where it may be built before.</summary>
        public ArtId Station(StationKind kind, bool built)
        {
            int i = (int)kind * 2 + (built ? 1 : 0);
            return (uint)i < (uint)_stations.Length ? _stations[i] : ArtId.None;
        }

        /// <summary>
        /// A prop's art: a resource node, the hearth, a bunk, or a pile on the ground (Epic 10 Part 3, K4.5a: its top item as
        /// the stall displays it); false for anything drawn otherwise, or not at all.
        /// </summary>
        public bool TryProp(in EntityView entity, out ArtId id)
        {
            if (entity.Type == EntityType.Resource) id = _catalog.Node(ItemCatalog.Get(entity.ResourceKind)?.ArtKey);
            else if (entity.Type == EntityType.Pile) id = entity.PileItem != Simpiens.Simulation.Economy.ItemType.None ? _catalog.Picture(ItemCatalog.Get(entity.PileItem)?.ArtKey, _catalog.DisplayPicture) : ArtId.None;
            else id = (uint)entity.Type < (uint)_byType.Length ? _byType[(int)entity.Type] : ArtId.None;
            return !id.IsNone;
        }

        /// <summary>A crawler in <paramref name="state"/>.</summary>
        public ArtId Crawler(AgentVisualState state) => _crawlers[(int)state];

        /// <summary>
        /// A crawler in <paramref name="state"/> wearing <paramref name="worn"/> (graphics V5): its variant when the catalogue
        /// has one (a helmet), the plain crawler otherwise. One dictionary probe; no string built.
        /// </summary>
        public ArtId Crawler(AgentVisualState state, Simpiens.Simulation.Economy.ItemType worn)
        {
            if (worn != Simpiens.Simulation.Economy.ItemType.None)
            {
                var key = ItemCatalog.Get(worn)?.ArtKey;
                if (key != null && _wearing.TryGetValue(key, out var byState)) return byState[(int)state];
            }
            return _crawlers[(int)state];
        }

        /// <summary>
        /// A creature of <paramref name="kind"/> (Epic 10 Part 3, K2.5): its kind's art, the loud missing art for a kind the
        /// catalogue has no row for, none for no kind. An array read.
        /// </summary>
        public ArtId Creature(Simpiens.Simulation.Creatures.CreatureKind kind)
        {
            var id = _creatures[(byte)kind];
            return id.IsNone && Simpiens.Simulation.Creatures.CreatureCatalog.IsDefined(kind) ? _catalog.Missing : id;
        }

        /// <summary>The trade stall's art; none when the catalogue has no stall.</summary>
        public ArtId Stall { get; }
    }
}
