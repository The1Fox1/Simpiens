namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// Isometric's art as data (graphics V1; M3 design D5): the placeholders on 32x16 tiles. Floor tiles are centred on
    /// their cells; walls, props and crawlers stand on their feet, at one art pixel per pixel of their 16x16 sprites
    /// (sizes here are art pixels). Everything is drawn from the world sheet, one batch.
    /// </summary>
    public static class IsometricArtTable
    {
        /// <summary>A 16x16 sprite's feet: the bottom of its last drawn row (row 14).</summary>
        public const float FeetPivot = 15f / 16f;

        /// <summary>A 16x16 sprite standing, in art pixels.</summary>
        private const float Standing = 16f;
        private const DrawSheet Sheet = DrawSheet.World;

        public static ArtCatalog Catalog { get; } = Build();

        private static ArtCatalog Build()
        {
            const int tileW = IsometricSprites.TileWidth, tileH = IsometricSprites.TileHeight;
            var art = new ArtCatalog.Builder("isometric")
            {
                NameOffset = Vec2.Zero, NameHeight = 1.125f, // over the head: a 16-pixel sprite is one unit tall
                CarryOffset = Vec2.Zero, CarryHeight = 1.25f, // centred over the head
                // Along the counter's front edge, 12 px apart on screen ((s, -s) on the floor is horizontal), on its top (8 px up)
                DisplaySlots = new[] { (new Vec2(-0.5625f, 0.5625f), 0.5f), (new Vec2(-0.1875f, 0.1875f), 0.5f), (new Vec2(0.1875f, -0.1875f), 0.5f), (new Vec2(0.5625f, -0.5625f), 0.5f) },
                DisplayPicture = ItemPicture.Map,
            };
            art.Add(ArtKeys.TileFloor, Sheet, new[] { "Iso_Floor" }, tileW, tileH)
               .Add(ArtKeys.TileRock, Sheet, new[] { "Iso_Rock" }, tileW, tileH)
               .Add(ArtKeys.TileWall, Sheet, new[] { "Iso_Wall" }, tileW, tileH + IsometricSprites.WallRise, pivotY: IsometricSprites.BlockPivotY(tileH, IsometricSprites.WallRise))
               .Add(ArtKeys.TileWallLow, Sheet, new[] { "Iso_Wall_Low" }, tileW, tileH + IsometricSprites.LowWallRise, pivotY: IsometricSprites.BlockPivotY(tileH, IsometricSprites.LowWallRise))
               .Add(ArtKeys.PropHearth, Sheet, new[] { "Anchor_Campfire_0", "Anchor_Campfire_1" }, Standing, Standing, pivotY: FeetPivot,
                   framesPerSecond: TopDownArtTable.HearthFramesPerSecond, pulseRate: TopDownArtTable.HearthPulseRate, pulseAmplitude: TopDownArtTable.HearthPulseAmplitude)
               .Add(ArtKeys.PropBunk, Sheet, new[] { "Iso_Bunk" }, IsometricSprites.BunkWidth, IsometricSprites.BunkHeight + IsometricSprites.BunkRise,
                   pivotY: IsometricSprites.BlockPivotY(IsometricSprites.BunkHeight, IsometricSprites.BunkRise))
               .Add(ArtKeys.PropStall, Sheet, new[] { "Iso_Stall" }, IsometricSprites.StallWidth, IsometricSprites.StallHeight + IsometricSprites.StallRise,
                   pivotY: IsometricSprites.BlockPivotY(IsometricSprites.StallHeight, IsometricSprites.StallRise))
               .Add(ArtKeys.CrawlerShadow, Sheet, new[] { "Iso_Shadow" }, IsometricSprites.Shadow.Width, IsometricSprites.Shadow.Height)
               .Add(ArtKeys.SelectionRing, Sheet, new[] { "Iso_Selection_Ring" }, IsometricSprites.SelectionRing.Width, IsometricSprites.SelectionRing.Height)
               .Add(ArtKeys.Missing, Sheet, new[] { MissingArt.Name }, Standing, Standing, pivotY: FeetPivot);
            foreach (var (key, sprite) in TopDownArtTable.Nodes) art.Add(key, Sheet, new[] { sprite }, Standing, Standing, pivotY: FeetPivot);
            foreach (var (state, sprites) in TopDownArtTable.Crawlers) art.Add(ArtKeys.Crawler(state), Sheet, sprites, Standing, Standing, pivotY: FeetPivot);
            foreach (var key in WornSprites.Keys)
                foreach (var (state, sprites) in TopDownArtTable.Crawlers)
                    art.Add(ArtKeys.CrawlerWearing(state, key), Sheet, new[] { WornSprites.Name(sprites[0], key), WornSprites.Name(sprites[1], key) }, Standing, Standing, pivotY: FeetPivot);
            // Items stand at the bottom of their canvas; the carried marker hangs centred
            foreach (var key in ItemSprites.Keys)
            {
                art.Add(ArtKeys.Item(key, ItemPicture.Map), Sheet, new[] { ItemSprites.MapName(key) }, ItemSprites.MapSize, ItemSprites.MapSize, pivotY: 1f);
                art.Add(ArtKeys.Item(key, ItemPicture.Carried), Sheet, new[] { ItemSprites.CarriedName(key) }, ItemSprites.CarriedSize, ItemSprites.CarriedSize);
            }
            return art.Build();
        }
    }
}
