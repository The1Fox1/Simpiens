using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Presentation;

namespace Simpiens.Client
{
    /// <summary>
    /// Draws a sorted <see cref="DrawList"/> through the top-down camera (Epic CLIENT). The list is already in draw order,
    /// so each batch draws in submission order (<see cref="SpriteSortMode.Deferred"/>) and never sorts by depth itself;
    /// a new batch starts only when the sheet changes. Pixel art: point sampling, colours not premultiplied. Sprites
    /// turn about their centre.
    /// </summary>
    public sealed class WorldRenderer
    {
        private readonly SpriteBatch _batch;
        private readonly SpriteAtlas[] _sheets;

        /// <param name="sheets">One atlas per <see cref="DrawSheet"/>, by its value.</param>
        public WorldRenderer(SpriteBatch batch, SpriteAtlas[] sheets)
        {
            _batch = batch;
            _sheets = sheets;
        }

        public void Draw(DrawList items, in TopDownCamera camera)
        {
            int open = -1;
            for (int i = 0; i < items.Count; i++)
            {
                ref readonly var item = ref items[i];
                int sheet = (int)item.Sheet;
                if (sheet != open)
                {
                    if (open >= 0) _batch.End();
                    _batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                    open = sheet;
                }

                var atlas = _sheets[sheet];
                var source = atlas.Source(item.SpriteIndex);
                var centre = camera.WorldToScreen(new Vec2(item.X, item.Y));
                var scale = new Vector2(item.Width * camera.PixelsPerUnit / source.Width, item.Height * camera.PixelsPerUnit / source.Height);
                var origin = new Vector2(source.Width * 0.5f, source.Height * 0.5f);
                var tint = new Color(item.Tint.R, item.Tint.G, item.Tint.B, item.Tint.A);
                // World rotation is counter-clockwise with y up; on screen (y down) that is a negative angle
                _batch.Draw(atlas.Texture, new Vector2(centre.X, centre.Y), source, tint, -item.Rotation, origin, scale, SpriteEffects.None, 0f);
            }
            if (open >= 0) _batch.End();
        }
    }
}
