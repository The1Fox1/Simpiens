using System;
using Simpiens.Simulation.Economy;

namespace Simpiens.Presentation
{
    /// <summary>
    /// One picture of something drawn (M3, design D4): a sprite, its size in art pixels at zoom 1, and where its anchor
    /// sits in it (0 to 1 from its top-left: 0.5, 0.5 its centre; 0.5, 1 its feet). No sprite: nothing is drawn.
    /// </summary>
    public readonly struct ArtPiece
    {
        public ArtPiece(int sprite, float width, float height, float pivotX = 0.5f, float pivotY = 0.5f)
        {
            Sprite = sprite;
            Width = width;
            Height = height;
            PivotX = pivotX;
            PivotY = pivotY;
        }

        public static ArtPiece None => new ArtPiece(-1, 0f, 0f);

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

        /// <summary>A resource node yielding <paramref name="kind"/>.</summary>
        public abstract ArtPiece Resource(ItemType kind);

        /// <summary>The hearth at <paramref name="viewSeconds"/>: it flickers and pulses.</summary>
        public abstract ArtPiece Hearth(float viewSeconds);

        public abstract ArtPiece Bunk { get; }

        /// <summary>A crawler in <paramref name="pose"/>; the pose's squash applies to its size, its tilt turns it.</summary>
        public abstract ArtPiece Crawler(in CrawlerPose pose);

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
    /// Top-down's art (Epic CLIENT M0–M1): the generated 16x16 sprites at Unity's sizes, centred on their points. Sizes
    /// are Unity's, in world units at <see cref="TopDownProjection.BasePixelsPerUnit"/> art pixels each: crawlers 0.7 of
    /// a cell, resource nodes 0.8, the hearth, bunks and the ring 0.9.
    /// </summary>
    public sealed class TopDownArt : WorldArt
    {
        public const float CrawlerSize = 0.7f;
        public const float ResourceSize = 0.8f;
        public const float HearthSize = 0.9f;
        public const float BunkSize = 0.9f;
        public const float SelectionRingSize = 0.9f;

        private const float Art = TopDownProjection.BasePixelsPerUnit;

        public static TopDownArt Instance { get; } = new TopDownArt();

        private TopDownArt() { }

        public override ArtPiece Tile(TileKind kind)
        {
            switch (kind)
            {
                case TileKind.Floor: return new ArtPiece(ProceduralSprites.FloorSprite.Index, Art, Art);
                case TileKind.Rock: return new ArtPiece(ProceduralSprites.RockSprite.Index, Art, Art);
                default: return new ArtPiece(ProceduralSprites.WallSprite.Index, Art, Art);
            }
        }

        public override ArtPiece Resource(ItemType kind)
            => new ArtPiece(ProceduralSprites.GetResourceSprite(kind).Index, ResourceSize * Art, ResourceSize * Art);

        /// <summary>Two-frame flame at 6 Hz and a subtle pulse (Unity's CampfireView).</summary>
        public override ArtPiece Hearth(float viewSeconds)
        {
            var sprite = ProceduralSprites.GetCampfireSprite((int)(viewSeconds * 6f) & 1);
            float size = HearthSize * (1f + MathF.Sin(viewSeconds * 4f) * 0.03f);
            return new ArtPiece(sprite.Index, size * Art, size * Art);
        }

        public override ArtPiece Bunk => new ArtPiece(ProceduralSprites.BunkSprite.Index, BunkSize * Art, BunkSize * Art);

        public override ArtPiece Crawler(in CrawlerPose pose)
            => new ArtPiece(pose.Sprite.Index, CrawlerSize * pose.ScaleX * Art, CrawlerSize * pose.ScaleY * Art);

        public override ArtPiece SelectionRing => new ArtPiece(ProceduralSprites.SelectionRing.Index, SelectionRingSize * Art, SelectionRingSize * Art);

        /// <summary>Above the crawler on the map (Unity's CameraController).</summary>
        public override Vec2 NameOffset => new Vec2(0f, 0.65f);

        public override float NameHeight => 0f;
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

        public string Name { get; }
        public IViewProjection Projection { get; }
        public WorldArt Art { get; }

        public override string ToString() => Name;
    }
}
