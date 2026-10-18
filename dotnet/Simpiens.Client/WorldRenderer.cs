using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Presentation;

namespace Simpiens.Client
{
    /// <summary>
    /// Draws a sorted <see cref="DrawList"/> through the top-down camera (Epic CLIENT M0). The list is already in draw
    /// order, so the batch draws in submission order (<see cref="SpriteSortMode.Deferred"/>) and never sorts by depth
    /// itself. Pixel art: point sampling, and colours that aren't premultiplied.
    /// </summary>
    public sealed class WorldRenderer
    {
        private readonly SpriteBatch _batch;
        private readonly SpriteAtlas _atlas;

        public WorldRenderer(SpriteBatch batch, SpriteAtlas atlas)
        {
            _batch = batch;
            _atlas = atlas;
        }

        public void Draw(DrawList items, in TopDownCamera camera)
        {
            _batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            for (int i = 0; i < items.Count; i++)
            {
                ref readonly var item = ref items[i];
                var source = _atlas.Source(item.SpriteIndex);
                var centre = camera.WorldToScreen(new Vec2(item.X, item.Y));
                float width = item.Width * camera.PixelsPerUnit, height = item.Height * camera.PixelsPerUnit;
                var topLeft = new Vector2(centre.X - width * 0.5f, centre.Y - height * 0.5f);
                var scale = new Vector2(width / source.Width, height / source.Height);
                var tint = new Color(item.Tint.R, item.Tint.G, item.Tint.B, item.Tint.A);
                _batch.Draw(_atlas.Texture, topLeft, source, tint, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
            _batch.End();
        }
    }
}
