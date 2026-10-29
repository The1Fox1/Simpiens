using System;
using System.Collections.Generic;
using System.Linq;
using Simpiens.Presentation.Cutscenes;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// Every sprite sheet and how it is sampled (graphics V0–V1): the one table the client builds its textures from,
    /// and the only place sprite indices come from (<see cref="SpriteSheet"/>). The generators make named pixels and
    /// never read a sheet, so no index exists before its sheet does.
    /// </summary>
    public static class SpriteSheets
    {
        /// <summary>Top-down's art: the generated 16x16 sprites (<see cref="ProceduralSprites.All"/>).</summary>
        public static SpriteSheet Sprites { get; } = new SpriteSheet(DrawSheet.Sprites, ProceduralSprites.All);

        /// <summary>The fog's cloud, sampled smoothly.</summary>
        public static SpriteSheet Fog { get; } = new SpriteSheet(DrawSheet.Fog, new[] { FogCloud.Sprite });

        /// <summary>
        /// Isometric's: top-down's art at the same indices, then the isometric placeholders, so the isometric World
        /// layer (walls, props, crawlers interleaved) is one batch. Top-down keeps its own sheet (finding F-T1: its point
        /// sampling hits texel ties, so its picture depends on its texture's width; the sheets merge in graphics V3).
        /// </summary>
        public static SpriteSheet Isometric { get; } = new SpriteSheet(DrawSheet.Isometric, ProceduralSprites.All.Concat(IsometricSprites.All));

        /// <summary>A hard cutscene's still backdrops.</summary>
        public static SpriteSheet Backdrop { get; } = new SpriteSheet(DrawSheet.Backdrop, CutsceneArt.Backdrops);

        /// <summary>The sheet <paramref name="sheet"/>.</summary>
        public static SpriteSheet Contents(DrawSheet sheet) => sheet switch
        {
            DrawSheet.Sprites => Sprites,
            DrawSheet.Fog => Fog,
            DrawSheet.Isometric => Isometric,
            DrawSheet.Backdrop => Backdrop,
            _ => throw new ArgumentOutOfRangeException(nameof(sheet), sheet, "No such sprite sheet."),
        };

        /// <summary>Sampled smoothly (the fog's cloud, stretched over a sector); pixel art is point-sampled.</summary>
        public static bool Smooth(DrawSheet sheet) => sheet == DrawSheet.Fog;

        /// <summary>Every sheet, by value.</summary>
        public static IReadOnlyList<DrawSheet> All { get; } = (DrawSheet[])Enum.GetValues(typeof(DrawSheet));
    }
}
