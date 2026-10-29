using System;
using Simpiens.Presentation.Art;

namespace Simpiens.Presentation
{
    /// <summary>
    /// A way of looking at the world (M3, design D4): a projection and the art drawn through it, as data (graphics V2:
    /// an <see cref="ArtCatalog"/>). The world view sets both together, so they can't disagree. The scene asks the
    /// catalogue what things look like, and never which projection it has.
    /// </summary>
    public sealed class ViewMode
    {
        public ViewMode(string name, IViewProjection projection, ArtCatalog catalog)
        {
            Name = name;
            Projection = projection ?? throw new ArgumentNullException(nameof(projection));
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>The developers' view (the map designer, debugging): Unity's top-down picture.</summary>
        public static ViewMode TopDown { get; } = new ViewMode("top-down", TopDownProjection.Instance, TopDownArtTable.Catalog);

        /// <summary>
        /// The main view (M3): 2:1 isometric on 32x16 tiles (producer P1), height at the tile's 16 art pixels a world
        /// unit, standing the placeholders on their feet.
        /// </summary>
        public static ViewMode Isometric { get; } = new ViewMode("isometric",
            new IsometricProjection(IsometricSprites.TileWidth, IsometricSprites.TileHeight, IsometricSprites.TileHeight), IsometricArtTable.Catalog);

        public string Name { get; }
        public IViewProjection Projection { get; }
        public ArtCatalog Catalog { get; }

        public override string ToString() => Name;
    }
}
