namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// The menus' art as data (graphics V3.6): every item's icon (<c>item/&lt;art key&gt;/icon</c>), 16x16 art pixels on
    /// the icons sheet, and the missing art. The dashboard draws them magnified to a whole multiple (V4).
    /// </summary>
    public static class IconArtTable
    {
        private const DrawSheet Sheet = DrawSheet.Icons;

        public static ArtCatalog Catalog { get; } = Build();

        private static ArtCatalog Build()
        {
            var art = new ArtCatalog.Builder("icons");
            art.Add(ArtKeys.Missing, Sheet, new[] { MissingArt.Name }, MissingArt.Size, MissingArt.Size);
            foreach (var key in ItemSprites.Keys) art.Add(ArtKeys.Item(key, ItemPicture.Icon), Sheet, new[] { ItemSprites.IconName(key) }, SpriteKit.IconSize, SpriteKit.IconSize);
            return art.Build();
        }
    }
}
