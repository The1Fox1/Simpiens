using System;
using Simpiens.Entities;
using Simpiens.Presentation.Art;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Presentation
{
    /// <summary>
    /// One picture of something drawn (M3, design D4): a sprite, its size in art pixels at zoom 1, and where its anchor
    /// sits in it (0 to 1 from its top-left: 0.5, 0.5 its centre; 0.5, 1 its feet). No sprite: nothing is drawn.
    /// </summary>
    public readonly struct ArtPiece
    {
        public ArtPiece(int sprite, float width, float height, float pivotX = 0.5f, float pivotY = 0.5f, DrawSheet sheet = DrawSheet.Sprites)
        {
            Sheet = sheet;
            Sprite = sprite;
            Width = width;
            Height = height;
            PivotX = pivotX;
            PivotY = pivotY;
        }

        public static ArtPiece None => new ArtPiece(-1, 0f, 0f);

        /// <summary>The atlas the sprite is drawn from.</summary>
        public DrawSheet Sheet { get; }
        public int Sprite { get; }
        public float Width { get; }
        public float Height { get; }
        public float PivotX { get; }
        public float PivotY { get; }
        public bool IsNone => Sprite < 0;
    }

    /// <summary>
    /// How a view mode pictures the world (M3, design D4): the sprite, size and pivot of each thing the scene draws. The
    /// scene asks its art set and never which projection it has. Top-down's is Unity's art (<see cref="TopDownArt"/>);
    /// isometric's stands things on their feet. The seed of M7's sprite catalogue.
    /// </summary>
    public abstract class WorldArt
    {
        /// <summary>A map tile: floor (tinted per sector by the scene), rock or a wall.</summary>
        public abstract ArtPiece Tile(TileKind kind);

        /// <summary>A wall cut down so it hides nothing behind it (M3 design D7); top-down's walls are flat, so its is the wall tile.</summary>
        public virtual ArtPiece LowWall => Tile(TileKind.Wall);

        /// <summary>A resource node yielding <paramref name="kind"/>.</summary>
        public abstract ArtPiece Resource(ItemType kind);

        /// <summary>The hearth at <paramref name="viewSeconds"/>: it flickers and pulses.</summary>
        public abstract ArtPiece Hearth(float viewSeconds);

        public abstract ArtPiece Bunk { get; }

        /// <summary>A crawler in <paramref name="pose"/>; the pose's squash applies to its size, its tilt turns it.</summary>
        public abstract ArtPiece Crawler(in CrawlerPose pose);

        /// <summary>A soft shadow on the floor under a crawler, or none.</summary>
        public abstract ArtPiece CrawlerShadow { get; }

        /// <summary>The ring on the floor under the selected crawler.</summary>
        public abstract ArtPiece SelectionRing { get; }

        /// <summary>
        /// Where the selected crawler's name hangs: <see cref="NameOffset"/> on the floor from the crawler, then
        /// <see cref="NameHeight"/> world units above it. Its bottom sits there (<see cref="MapLabelLayout"/>).
        /// </summary>
        public abstract Vec2 NameOffset { get; }

        public abstract float NameHeight { get; }
    }

    /// <summary>
    /// An art set read from an <see cref="ArtCatalog"/> (graphics V1): every member is a row of the catalogue, resolved
    /// to its id once. Until the scene reads catalogues itself (graphics V2), the view modes' art sets are these.
    /// </summary>
    public abstract class CatalogueArt : WorldArt
    {
        private readonly ArtId[] _tiles;
        private readonly ArtId[] _crawlers;
        private readonly ArtId _lowWall, _hearth, _bunk, _ring, _shadow;

        protected CatalogueArt(ArtCatalog catalog)
        {
            Catalog = catalog;
            var kinds = (TileKind[])Enum.GetValues(typeof(TileKind));
            _tiles = new ArtId[kinds.Length];
            foreach (var kind in kinds) _tiles[(int)kind] = catalog.Id(ArtKeys.Tile(kind));
            var states = (AgentVisualState[])Enum.GetValues(typeof(AgentVisualState));
            _crawlers = new ArtId[states.Length];
            foreach (var state in states) _crawlers[(int)state] = catalog.Id(ArtKeys.Crawler(state));
            _lowWall = catalog.Id(ArtKeys.TileWallLow);
            _hearth = catalog.Id(ArtKeys.PropHearth);
            _bunk = catalog.Id(ArtKeys.PropBunk);
            _ring = catalog.Id(ArtKeys.SelectionRing);
            _shadow = catalog.TryId(ArtKeys.CrawlerShadow, out var shadow) ? shadow : ArtId.None;
        }

        public ArtCatalog Catalog { get; }

        public override ArtPiece Tile(TileKind kind) => Catalog.Piece(_tiles[(int)kind], 0f);

        public override ArtPiece LowWall => Catalog.Piece(_lowWall, 0f);

        public override ArtPiece Resource(ItemType kind) => Catalog.Piece(Catalog.Node(ItemCatalog.Get(kind)?.SpriteKey), 0f);

        public override ArtPiece Hearth(float viewSeconds) => Catalog.Piece(_hearth, viewSeconds);

        public override ArtPiece Bunk => Catalog.Piece(_bunk, 0f);

        public override ArtPiece Crawler(in CrawlerPose pose) => Catalog.Piece(_crawlers[(int)pose.State], pose.Frame, pose.ScaleX, pose.ScaleY);

        public override ArtPiece CrawlerShadow => _shadow.IsNone ? ArtPiece.None : Catalog.Piece(_shadow, 0f);

        public override ArtPiece SelectionRing => Catalog.Piece(_ring, 0f);

        public override Vec2 NameOffset => Catalog.NameOffset;

        public override float NameHeight => Catalog.NameHeight;
    }

    /// <summary>Top-down's art (Epic CLIENT M0–M1), from its table (<see cref="TopDownArtTable"/>).</summary>
    public sealed class TopDownArt : CatalogueArt
    {
        public const float CrawlerSize = TopDownArtTable.CrawlerSize;
        public const float ResourceSize = TopDownArtTable.ResourceSize;
        public const float HearthSize = TopDownArtTable.HearthSize;
        public const float BunkSize = TopDownArtTable.BunkSize;
        public const float SelectionRingSize = TopDownArtTable.SelectionRingSize;

        public static TopDownArt Instance { get; } = new TopDownArt();

        private TopDownArt() : base(TopDownArtTable.Catalog) { }
    }

    /// <summary>
    /// A way of looking at the world (M3, design D4): a projection and the art drawn through it. The world view sets both
    /// together, so they can't disagree.
    /// </summary>
    public sealed class ViewMode
    {
        public ViewMode(string name, IViewProjection projection, WorldArt art)
        {
            Name = name;
            Projection = projection ?? throw new ArgumentNullException(nameof(projection));
            Art = art ?? throw new ArgumentNullException(nameof(art));
        }

        /// <summary>The developers' view (the map designer, debugging): Unity's top-down picture.</summary>
        public static ViewMode TopDown { get; } = new ViewMode("top-down", TopDownProjection.Instance, TopDownArt.Instance);

        /// <summary>
        /// The main view (M3): 2:1 isometric on 32x16 tiles (producer P1), height at the tile's 16 art pixels a world
        /// unit, standing the placeholders on their feet.
        /// </summary>
        public static ViewMode Isometric { get; } = new ViewMode("isometric",
            new IsometricProjection(IsometricSprites.TileWidth, IsometricSprites.TileHeight, IsometricSprites.TileHeight), IsometricArt.Instance);

        public string Name { get; }
        public IViewProjection Projection { get; }
        public WorldArt Art { get; }
        /// <summary>The art as data, when the art set reads a catalogue (every view mode's does).</summary>
        public ArtCatalog Catalog => (Art as CatalogueArt)?.Catalog;

        public override string ToString() => Name;
    }
}
