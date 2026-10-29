using System.Collections.Generic;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// Crawlers wearing their gear (graphics V5): for each item worn on the head (<see cref="ItemArt"/>), every crawler
    /// state and frame again with its hair (<see cref="ProceduralSprites.Hair"/>, a colour no other part uses) in the
    /// item's primary colour: a helmet, aligned in every pose, asleep included. In the world sheet.
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
                if (look.Worn != Worn.Head) continue;
                foreach (var (_, frames) in TopDownArtTable.Crawlers)
                    foreach (var frame in frames)
                        _all.Add(SpriteKit.Recolour(sheet[frame], ProceduralSprites.Hair, look.Primary, Name(frame, key)));
                _keys.Add(key);
            }
        }

        public static IReadOnlyList<PixelSprite> All => _all;

        /// <summary>The art keys of the items worn on the head, in ordinal order.</summary>
        public static IReadOnlyList<string> Keys => _keys;

        /// <summary>A crawler frame's sprite wearing <paramref name="itemArtKey"/>: "Agent_Walk_0_hazard-gear".</summary>
        public static string Name(string crawlerFrame, string itemArtKey) => crawlerFrame + "_" + itemArtKey;
    }
}
