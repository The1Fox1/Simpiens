using System.Collections.Generic;
using System.Linq;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The sheets' table (graphics V0): every sheet has sprites, a name means one sprite (a sprite in two sheets is the
    /// same sprite), and only the fog's cloud is sampled smoothly.
    /// </summary>
    public class SpriteSheetTests
    {
        [Fact]
        public void ANameMeansOneSprite()
        {
            var byName = new Dictionary<string, PixelSprite>();
            foreach (var sheet in SpriteSheets.All)
            {
                var sprites = SpriteSheets.Contents(sheet);
                Assert.NotEmpty(sprites);
                Assert.Equal(sprites.Count, sprites.Select(s => s.Name).Distinct().Count());
                foreach (var sprite in sprites)
                {
                    if (byName.TryGetValue(sprite.Name, out var seen)) Assert.Same(seen, sprite);
                    else byName[sprite.Name] = sprite;
                }
            }
        }

        [Fact]
        public void OnlyTheFogIsSampledSmoothly()
            => Assert.Equal(new[] { DrawSheet.Fog }, SpriteSheets.All.Where(SpriteSheets.Smooth));
    }
}
