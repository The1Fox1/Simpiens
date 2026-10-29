using System;
using System.Collections.Generic;
using Simpiens.Presentation.Cutscenes;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// What each sprite sheet holds and how it is sampled (graphics V0): the one table the client builds its textures
    /// from, and the one the tests read a draw item's sprite back through.
    /// </summary>
    public static class SpriteSheets
    {
        private static readonly IReadOnlyList<PixelSprite> Fog = new[] { FogCloud.Sprite };

        /// <summary>The sprites of <paramref name="sheet"/>, by index.</summary>
        public static IReadOnlyList<PixelSprite> Contents(DrawSheet sheet) => sheet switch
        {
            DrawSheet.Sprites => ProceduralSprites.All,
            DrawSheet.Fog => Fog,
            DrawSheet.Isometric => WorldSprites.All,
            DrawSheet.Backdrop => CutsceneArt.Backdrops,
            _ => throw new ArgumentOutOfRangeException(nameof(sheet), sheet, "No such sprite sheet."),
        };

        /// <summary>Sampled smoothly (the fog's cloud, stretched over a sector); pixel art is point-sampled.</summary>
        public static bool Smooth(DrawSheet sheet) => sheet == DrawSheet.Fog;

        /// <summary>Every sheet, by value.</summary>
        public static IReadOnlyList<DrawSheet> All { get; } = (DrawSheet[])Enum.GetValues(typeof(DrawSheet));
    }
}
