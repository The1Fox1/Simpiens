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

        /// <summary>A shot's pivot: its eight pixels drawn six above the floor point it flies over, at a standing body's chest.</summary>
        public const float ShotPivot = 1.75f;

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
                BadgeOffset = new Vec2(-0.3f, 0.3f), BadgeHeight = 1f, // a role badge beside the head, to its left on screen (Epic 11, S5.8)
                RideOffset = Vec2.Zero, RideHeight = 0.55f, // a carried crawler across the shoulders (Epic 10 Part 3, K5.8a)
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
               .Add(ArtKeys.PropForge, Sheet, new[] { "Iso_Forge" }, IsometricSprites.ForgeWidth, IsometricSprites.ForgeHeight + IsometricSprites.ForgeRise,
                   pivotY: IsometricSprites.BlockPivotY(IsometricSprites.ForgeHeight, IsometricSprites.ForgeRise))
               .Add(ArtKeys.PropStill, Sheet, new[] { "Iso_Still" }, IsometricSprites.StillWidth, IsometricSprites.StillHeight + IsometricSprites.StillRise,
                   pivotY: IsometricSprites.BlockPivotY(IsometricSprites.StillHeight, IsometricSprites.StillRise))
               .Add(ArtKeys.PropStationPlot, Sheet, new[] { "Iso_StationPlot" }, IsometricSprites.PlotWidth, IsometricSprites.PlotHeight)
               .Add(ArtKeys.PropDrillYard, Sheet, new[] { "Iso_DrillYard" }, IsometricSprites.DrillYardWidth, IsometricSprites.DrillYardHeight + IsometricSprites.DrillYardRise,
                   pivotY: IsometricSprites.BlockPivotY(IsometricSprites.DrillYardHeight, IsometricSprites.DrillYardRise)) // Epic 11, S3.4
               .Add(ArtKeys.PropCairn, Sheet, new[] { "Prop_Cairn" }, Standing, Standing, pivotY: FeetPivot) // a body (Epic 10 Part 3, K5.8b), standing as the hearth does
               .Add(ArtKeys.CrawlerShadow, Sheet, new[] { "Iso_Shadow" }, IsometricSprites.Shadow.Width, IsometricSprites.Shadow.Height)
               .Add(ArtKeys.SelectionRing, Sheet, new[] { "Iso_Selection_Ring" }, IsometricSprites.SelectionRing.Width, IsometricSprites.SelectionRing.Height)
               .Add(ArtKeys.PropCaravan, Sheet, HazardSprites.Names(CaravanSprites.Name, CaravanSprites.Frames), Standing, Standing, pivotY: FeetPivot, framesPerSecond: TopDownArtTable.CaravanFramesPerSecond)
               // The hazards' animations (Epic 10 Part 2, the stretch, D69): puffs and pebbles stand, ripples lie on the floor
               .Add(ArtKeys.FxFoulAir, Sheet, HazardSprites.Names(HazardSprites.PuffName, HazardSprites.PuffFrames), HazardSprites.Size, HazardSprites.Size, pivotY: 1f, framesPerSecond: HazardEffects.PuffFramesPerSecond)
               .Add(ArtKeys.FxFallingRock, Sheet, HazardSprites.Names(HazardSprites.RockName, HazardSprites.RockFrames), Standing, Standing, pivotY: FeetPivot, framesPerSecond: HazardEffects.RockFramesPerSecond)
               .Add(ArtKeys.FxFlood, Sheet, HazardSprites.Names(HazardSprites.IsoRippleName, HazardSprites.RippleFrames), HazardSprites.IsoRippleWidth, HazardSprites.IsoRippleHeight, framesPerSecond: HazardEffects.RippleFramesPerSecond)
               // Shots in flight (Epic 10 Part 3, K8.4a): raised to a body's chest above the line they fly along
               .Add(ArtKeys.FxShot, Sheet, new[] { ShotSprites.ShotName }, ShotSprites.Size, ShotSprites.Size, pivotY: ShotPivot)
               .Add(ArtKeys.FxSpit, Sheet, new[] { ShotSprites.SpitName }, ShotSprites.Size, ShotSprites.Size, pivotY: ShotPivot)
               .Add(ArtKeys.FxBar, Sheet, new[] { BarSprites.Name }, 12f, 1.5f) // a boss's health bar (K10.5b), as top-down's
               .Add(ArtKeys.BadgeFront, Sheet, new[] { BadgeSprites.Front }, BadgeSprites.Size, BadgeSprites.Size) // a role badge (Epic 11, S5.8), as top-down's
               .Add(ArtKeys.BadgeBack, Sheet, new[] { BadgeSprites.Back }, BadgeSprites.Size, BadgeSprites.Size)
               .Add(ArtKeys.BadgeCarrier, Sheet, new[] { BadgeSprites.Carrier }, BadgeSprites.Size, BadgeSprites.Size)
               .Add(ArtKeys.BadgeLeader, Sheet, new[] { BadgeSprites.Leader }, BadgeSprites.Size, BadgeSprites.Size)
               .Add(ArtKeys.BadgeNamed, Sheet, new[] { BadgeSprites.Named }, BadgeSprites.Size, BadgeSprites.Size) // a named creature's mark (Epic 11, S7.7)
               .Add(ArtKeys.PropGate, Sheet, new[] { GateSprites.Sealed }, 2f * Standing, 2f * Standing, pivotY: FeetPivot) // the descent gate (K10.5c), standing
               .Add(ArtKeys.PropGateOpen, Sheet, new[] { GateSprites.Open }, 2f * Standing, 2f * Standing, pivotY: FeetPivot)
               .Add(ArtKeys.Missing, Sheet, new[] { MissingArt.Name }, Standing, Standing, pivotY: FeetPivot);
            foreach (var (key, sprite) in TopDownArtTable.Nodes) art.Add(key, Sheet, new[] { sprite }, Standing, Standing, pivotY: FeetPivot);
            foreach (var (state, sprites) in TopDownArtTable.Crawlers) art.Add(ArtKeys.Crawler(state), Sheet, sprites, Standing, Standing, pivotY: FeetPivot);
            foreach (var (key, framesPerSecond) in TopDownArtTable.Creatures)
            {
                float size = Standing * CreatureSprites.SizeOf(key) / CreatureSprites.Size; // a boss on its bigger canvas (K10.5a)
                art.Add(ArtKeys.Creature(key), Sheet, CreatureSprites.Names(key), size, size, pivotY: FeetPivot, framesPerSecond: framesPerSecond); // on their feet (K2.5)
            }
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
