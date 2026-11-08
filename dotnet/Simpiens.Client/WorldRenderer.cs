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
    /// <para>
    /// A silhouette (<see cref="DrawItem.Silhouette"/>: the red coat over a hurt crawler, Epic 10 Part 2, S3, D71) is drawn
    /// through the stock <see cref="BasicEffect"/> with its fog forced to 100% (<c>FogStart == FogEnd</c>): the fog
    /// replaces each texel's colour with the fog's and keeps its alpha, so the sprite's shape is filled flat with the item's
    /// colour, with no shader of our own, in KNI and MonoGame alike. The effect writes premultiplied colour, so its batch
    /// blends premultiplied. A silhouette starts a batch of its own, as a change of sheet does.
    /// </para>
    /// </summary>
    public sealed class WorldRenderer : System.IDisposable
    {
        /// <summary>
        /// Screen pixels every point-sampled quad is shifted by, right and down (graphics V3.1, epic F12). At an in-between
        /// scale a pixel's centre can land exactly on a texel's edge, and then float rounding, down to the atlas's width,
        /// picks the texel: the picture changed when the atlas did. Shifted, the centre is decided by geometry. At a whole
        /// scale it changes nothing (a centre is half a texel from any edge). Not a binary fraction, so a zoom step can't
        /// line an edge up with it; large enough to survive a GPU's 8-bit sub-pixel rasterizer at the closest zoom.
        /// </summary>
        public const float SampleBias = 0.13f;

        private readonly SpriteBatch _batch;
        private readonly SpriteAtlas[] _sheets;
        private readonly BasicEffect _silhouette;

        /// <param name="sheets">One atlas per <see cref="DrawSheet"/>, by its value.</param>
        public WorldRenderer(SpriteBatch batch, SpriteAtlas[] sheets)
        {
            _batch = batch;
            _sheets = sheets;
            _silhouette = new BasicEffect(batch.GraphicsDevice)
            {
                TextureEnabled = true,
                VertexColorEnabled = true,
                LightingEnabled = false,
                FogEnabled = true,
                FogStart = 0f,
                FogEnd = 0f, // start and end alike: everything is wholly fogged
            };
        }

        public void Draw(DrawList items, in View camera)
        {
            int open = -1;
            bool openOnFloor = false, openSilhouette = false;
            Rgba32 openFill = default;
            var floorToScreen = ToMatrix(camera.FloorToScreen());
            for (int i = 0; i < items.Count; i++)
            {
                ref readonly var item = ref items[i];
                int sheet = (int)item.Sheet;
                bool fill = item.Silhouette;
                if (sheet != open || item.OnFloor != openOnFloor || fill != openSilhouette || (fill && !SameColour(item.Tint, openFill)))
                {
                    if (open >= 0) _batch.End();
                    // What lies on the floor is drawn in floor units under the view's transform (M3 design D6)
                    var transform = item.OnFloor ? floorToScreen : Matrix.Identity;
                    if (fill)
                    {
                        Silhouette(transform, item.Tint);
                        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, _sheets[sheet].Sampling, DepthStencilState.None, RasterizerState.CullNone, _silhouette, transform);
                    }
                    else
                    {
                        _batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, _sheets[sheet].Sampling, DepthStencilState.None, RasterizerState.CullNone, null, transform);
                    }
                    open = sheet;
                    openOnFloor = item.OnFloor;
                    openSilhouette = fill;
                    openFill = item.Tint;
                }

                var atlas = _sheets[sheet];
                var source = atlas.Source(item.SpriteIndex);
                if (item.OnFloor)
                {
                    // From the rectangle's near corner up its height; flipped so the texture's top row lies along its far edge
                    var size = new Vector2(item.Width / source.Width, item.Height / source.Height);
                    _batch.Draw(atlas.Texture, new Vector2(item.X - item.Width * 0.5f, item.Y - item.Height * 0.5f), source,
                        new Color(item.Tint.R, item.Tint.G, item.Tint.B, item.Tint.A), 0f, Vector2.Zero, size, SpriteEffects.FlipVertically, 0f);
                    continue;
                }
                var quad = ScreenQuad.Of(item, camera);
                var scale = new Vector2(quad.Width / source.Width, quad.Height / source.Height);
                var origin = new Vector2(source.Width * item.PivotX, source.Height * item.PivotY);
                var tint = new Color(item.Tint.R, item.Tint.G, item.Tint.B, item.Tint.A);
                // World rotation is counter-clockwise with y up; on screen (y down) that is a negative angle
                _batch.Draw(atlas.Texture, new Vector2(quad.Anchor.X + SampleBias, quad.Anchor.Y + SampleBias), source, tint, -item.Rotation, origin, scale, item.FlipX ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            }
            if (open >= 0) _batch.End();
        }

        /// <summary>
        /// Readies the silhouette effect for a batch in <paramref name="colour"/>, under <paramref name="transform"/>. A custom
        /// effect takes the place of the batch's own vertex shader, so it is given the batch's projection: the viewport's
        /// pixels, y down, as <c>SpriteBatch</c> sets for its own.
        /// </summary>
        private void Silhouette(in Matrix transform, Rgba32 colour)
        {
            var viewport = _batch.GraphicsDevice.Viewport;
            _silhouette.World = Matrix.Identity;
            _silhouette.View = transform;
            _silhouette.Projection = Matrix.CreateOrthographicOffCenter(0f, viewport.Width, viewport.Height, 0f, 0f, -1f);
            _silhouette.FogColor = new Vector3(colour.R / 255f, colour.G / 255f, colour.B / 255f);
        }

        public void Dispose() => _silhouette.Dispose();

        private static bool SameColour(Rgba32 a, Rgba32 b) => a.R == b.R && a.G == b.G && a.B == b.B;

        /// <summary>The engine-free affine as XNA's row-vector matrix: <c>[x y 0 1] M</c>.</summary>
        private static Matrix ToMatrix(in Affine2 a)
            => new Matrix(a.XX, a.YX, 0f, 0f, a.XY, a.YY, 0f, 0f, 0f, 0f, 1f, 0f, a.OffsetX, a.OffsetY, 0f, 1f);

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
                var page = atlases.Page(glyph.Page);
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
