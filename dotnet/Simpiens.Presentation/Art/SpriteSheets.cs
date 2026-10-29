using System;
using System.Collections.Generic;
using System.Linq;
using Simpiens.Presentation.Cutscenes;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// Every sprite sheet and how it is sampled (graphics V0–V3): the one table the client builds its textures from,
    /// and the only place sprite indices come from (<see cref="SpriteSheet"/>). The generators make named pixels and
    /// never read a sheet, so no index exists before its sheet does.
    /// </summary>
    public static class SpriteSheets
    {
        /// <summary>
        /// The world's art, for both view modes (graphics V3.2): the generated 16x16 sprites, then the isometric
        /// placeholders, then the missing art (V3.3), then the items' map sprites and carried markers (V3.6), then
        /// crawlers wearing gear (V5), so either mode's world (walls, props, crawlers interleaved) is one batch. One sheet
        /// since the sampling rules made the picture independent of the atlas's layout (epic F12).
        /// </summary>
        public static SpriteSheet World { get; } = new SpriteSheet(DrawSheet.World, ProceduralSprites.All.Concat(IsometricSprites.All).Append(MissingArt.Sprite).Concat(ItemSprites.World).Concat(WornSprites.All));

        /// <summary>The fog's cloud, sampled smoothly.</summary>
        public static SpriteSheet Fog { get; } = new SpriteSheet(DrawSheet.Fog, new[] { FogCloud.Sprite });

        /// <summary>A hard cutscene's still backdrops.</summary>
        public static SpriteSheet Backdrop { get; } = new SpriteSheet(DrawSheet.Backdrop, CutsceneArt.Backdrops);

        /// <summary>The items' menu icons, then the missing art (graphics V3.6).</summary>
        public static SpriteSheet Icons { get; } = new SpriteSheet(DrawSheet.Icons, ItemSprites.Icons.Append(MissingArt.Sprite));

        /// <summary>The sheet <paramref name="sheet"/>.</summary>
        public static SpriteSheet Contents(DrawSheet sheet) => sheet switch
        {
            DrawSheet.World => World,
            DrawSheet.Fog => Fog,
            DrawSheet.Backdrop => Backdrop,
            DrawSheet.Icons => Icons,
            _ => throw new ArgumentOutOfRangeException(nameof(sheet), sheet, "No such sprite sheet."),
        };

        /// <summary>Sampled smoothly (the fog's cloud, stretched over a sector); pixel art is point-sampled.</summary>
        public static bool Smooth(DrawSheet sheet) => sheet == DrawSheet.Fog;

        /// <summary>Every sheet, by value.</summary>
        public static IReadOnlyList<DrawSheet> All { get; } = (DrawSheet[])Enum.GetValues(typeof(DrawSheet));
    }
}
