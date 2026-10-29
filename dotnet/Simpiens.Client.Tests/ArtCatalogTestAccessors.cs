// Test conveniences (graphics V2): a catalogue's pieces by what they picture, in the shape the art sets had (Tile, Bunk,
// Crawler, ...), so tests read as they did. Production code resolves ids once (WorldScene, EntityVisuals) and never
// looks a key up per frame; these do, which is fine in a test. They live in the production namespace so tests see
// them without a using, and in the test assembly so production code can't.
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;

namespace Simpiens.Presentation.Art
{
    public static class ArtCatalogTestAccessors
    {
        extension(ArtCatalog catalog)
        {
            public ArtPiece Tile(TileKind kind) => catalog.Piece(catalog.Id(ArtKeys.Tile(kind)), 0f);
            public ArtPiece LowWall => catalog.Piece(catalog.Id(ArtKeys.TileWallLow), 0f);
            public ArtPiece Resource(ItemType kind) => catalog.Piece(catalog.Node(ItemCatalog.Get(kind)?.SpriteKey), 0f);
            public ArtPiece Hearth(float viewSeconds) => catalog.Piece(catalog.Id(ArtKeys.PropHearth), viewSeconds);
            public ArtPiece Bunk => catalog.Piece(catalog.Id(ArtKeys.PropBunk), 0f);
            public ArtPiece Crawler(in CrawlerPose pose) => catalog.Piece(catalog.Id(ArtKeys.Crawler(pose.State)), pose.Frame, pose.ScaleX, pose.ScaleY);
            public ArtPiece CrawlerShadow => catalog.TryId(ArtKeys.CrawlerShadow, out var id) ? catalog.Piece(id, 0f) : ArtPiece.None;
            public ArtPiece SelectionRing => catalog.Piece(catalog.Id(ArtKeys.SelectionRing), 0f);
        }
    }
}
