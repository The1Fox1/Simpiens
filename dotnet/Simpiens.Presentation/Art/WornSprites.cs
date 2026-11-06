using System.Collections.Generic;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// Crawlers wearing their gear (graphics V5): for each item worn on the head (<see cref="ItemArt"/>), every crawler
    /// state and frame again with its hair (<see cref="ProceduralSprites.Hair"/>, a colour no other part uses) in the
    /// item's primary colour: a helmet, aligned in every pose, asleep included. For each item worn on the face (Epic 10
    /// Part 2, D52), the skin of the mouth's row at each frame's head anchor in the item's colours: a mask, its filters at
    /// either side. In the world sheet.
    /// </summary>
    public static class WornSprites
    {
        private static readonly List<PixelSprite> _all = new List<PixelSprite>(24);
        private static readonly List<string> _keys = new List<string>(2);

        static WornSprites()
        {
            var sheet = new Dictionary<string, PixelSprite>(ProceduralSprites.All.Count);
            foreach (var sprite in ProceduralSprites.All) sheet[sprite.Name] = sprite;
            foreach (var (key, look) in ItemArt.All)
            {
                if (look.Worn == Worn.None) continue;
                foreach (var (state, frames) in CrawlerFrames.All)
                    for (int f = 0; f < frames.Length; f++)
                        _all.Add(look.Worn == Worn.Head
                            ? SpriteKit.Recolour(sheet[frames[f]], ProceduralSprites.Hair, look.Primary, Name(frames[f], key))
                            : Masked(sheet[frames[f]], state, f, look, Name(frames[f], key)));
                _keys.Add(key);
            }
        }

        public static IReadOnlyList<PixelSprite> All => _all;

        // The mouth's row at the frame's head anchor: its skin becomes the mask, the head's two outer columns its filters
        private static PixelSprite Masked(PixelSprite sprite, Simpiens.Entities.AgentVisualState state, int frame, in ItemLook look, string name)
        {
            var pixels = (Rgba32[])sprite.Pixels.Clone();
            var head = People.HeadAnchors.Of(state, frame);
            var keys = ProceduralSprites.CrawlerKeys(state, frame);
            int y = head.Chin;
            for (int x = People.HeadAnchor.Left; x <= People.HeadAnchor.Right; x++)
            {
                if (keys[y][x] != 'K') continue;
                bool filter = x == People.HeadAnchor.Left || x == People.HeadAnchor.Right;
                pixels[y * sprite.Width + x] = filter ? look.Secondary : look.Primary;
            }
            return new PixelSprite(name, sprite.Width, sprite.Height, pixels);
        }

        /// <summary>The art keys of the items worn on the head, in ordinal order.</summary>
        public static IReadOnlyList<string> Keys => _keys;

        /// <summary>A crawler frame's sprite wearing <paramref name="itemArtKey"/>: "Agent_Walk_0_hazard-gear".</summary>
        public static string Name(string crawlerFrame, string itemArtKey) => crawlerFrame + "_" + itemArtKey;
    }
}
