using System.Collections.Generic;
using System.Linq;
using Simpiens.Presentation;
using Simpiens.Presentation.Art;
using Xunit;

namespace Simpiens.Client.Tests
{
    /// <summary>
    /// The sheets (graphics V0–V1): every sheet has sprites, a name means one sprite (a sprite in two sheets is the same
    /// sprite), a sheet gives out its indices, and only the fog's cloud is sampled smoothly.
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
        public void ASheetGivesOutItsIndicesByNameAndBySprite()
        {
            foreach (var sheet in SpriteSheets.All)
            {
                var sprites = SpriteSheets.Contents(sheet);
                Assert.Equal(sheet, sprites.Id);
                for (int i = 0; i < sprites.Count; i++)
                {
                    Assert.Equal(i, sprites.IndexOf(sprites[i].Name));
                    Assert.Equal(i, sprites.IndexOf(sprites[i]));
                }
            }
            Assert.Throws<KeyNotFoundException>(() => SpriteSheets.Fog.IndexOf("Agent_Idle_0"));
            Assert.Throws<KeyNotFoundException>(() => SpriteSheets.Fog.IndexOf(ProceduralSprites.WallSprite));
            Assert.False(SpriteSheets.Fog.TryIndexOf("Agent_Idle_0", out _));
        }

        [Fact]
        public void ASheetRefusesTwoSpritesOfOneName()
        {
            var a = new PixelSprite("Twin", 1, 1, new[] { Rgba32.Clear });
            var b = new PixelSprite("Twin", 1, 1, new[] { Rgba32.Clear });
            Assert.Throws<System.ArgumentException>(() => new SpriteSheet(DrawSheet.World, new[] { a, b }));
        }

        [Fact]
        public void OnlyTheFogIsSampledSmoothly()
            => Assert.Equal(new[] { DrawSheet.Fog }, SpriteSheets.All.Where(SpriteSheets.Smooth));
    }
}
