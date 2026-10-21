using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Simpiens.Presentation;

namespace Simpiens.Client
{
    /// <summary>
    /// Draws a sorted <see cref="DrawList"/> through a <see cref="View"/> (Epic CLIENT). The list is already in draw order,
    /// so each batch draws in submission order (<see cref="SpriteSortMode.Deferred"/>) and never sorts by depth itself;
    /// a new batch starts only when the sheet changes, with that sheet's sampling (point for pixel art, linear for the
    /// fog). Colours are not premultiplied. Sprites turn about their pivot.
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

        public void Draw(DrawList items, in View camera)
        {
            int open = -1;
            for (int i = 0; i < items.Count; i++)
            {
                ref readonly var item = ref items[i];
                int sheet = (int)item.Sheet;
                if (sheet != open)
                {
                    if (open >= 0) _batch.End();
                    _batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, _sheets[sheet].Sampling, DepthStencilState.None, RasterizerState.CullNone);
                    open = sheet;
                }

                var atlas = _sheets[sheet];
                var source = atlas.Source(item.SpriteIndex);
                var quad = ScreenQuad.Of(item, camera);
                var scale = new Vector2(quad.Width / source.Width, quad.Height / source.Height);
                var origin = new Vector2(source.Width * item.PivotX, source.Height * item.PivotY);
                var tint = new Color(item.Tint.R, item.Tint.G, item.Tint.B, item.Tint.A);
                // World rotation is counter-clockwise with y up; on screen (y down) that is a negative angle
                _batch.Draw(atlas.Texture, new Vector2(quad.Anchor.X, quad.Anchor.Y), source, tint, -item.Rotation, origin, scale, SpriteEffects.None, 0f);
            }
            if (open >= 0) _batch.End();
        }

        /// <summary>
        /// Draws the map's text over it, in screen space: every glyph 1:1 at its whole-pixel place, point-sampled, a batch
        /// per page in the order given (shadows were written before their text).
        /// </summary>
        public void DrawGlyphs(List<ScreenGlyph> glyphs, FontAtlases atlases)
        {
            Texture2D open = null;
            for (int i = 0; i < glyphs.Count; i++)
            {
                var glyph = glyphs[i];
                var page = atlases.Page(glyph.Face, glyph.Page);
                if (!ReferenceEquals(page, open))
                {
                    if (open != null) _batch.End();
                    _batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                    open = page;
                }
                _batch.Draw(page, new Rectangle(glyph.X, glyph.Y, glyph.Width, glyph.Height), new Rectangle(glyph.SourceX, glyph.SourceY, glyph.Width, glyph.Height),
                    new Color(glyph.Color.R, glyph.Color.G, glyph.Color.B, glyph.Color.A));
            }
            if (open != null) _batch.End();
        }
    }
}
