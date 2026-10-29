using System;

namespace Simpiens.Presentation
{
    /// <summary>
    /// Which image a draw item's sprite index refers to. The renderer starts a new batch when the sheet changes;
    /// each sheet has its own sampling (pixel art is point-sampled). What each holds: <see cref="Art.SpriteSheets"/>.
    /// </summary>
    public enum DrawSheet : byte
    {
        /// <summary>The world's art for both view modes: the 16x16 sprites and the isometric placeholders (graphics V3.2).</summary>
        World = 0,
        /// <summary><see cref="FogCloud.Sprite"/>, sampled smoothly.</summary>
        Fog = 1,
        /// <summary><see cref="Cutscenes.CutsceneArt.Backdrops"/>: a hard cutscene's still backdrops, in their own index space.</summary>
        Backdrop = 2,
    }

    /// <summary>
    /// One sprite to draw this frame: where it stands in the world, and how big it is in art pixels. The projection
    /// places it (<see cref="ScreenQuad"/>); the item itself knows nothing of the screen.
    /// </summary>
    public struct DrawItem
    {
        public DrawSheet Sheet;
        public int SpriteIndex;
        /// <summary>The anchor: the floor point the sprite stands on, in world units.</summary>
        public float X;
        public float Y;
        /// <summary>The anchor's height above the floor, in world units (visual only).</summary>
        public float Z;
        /// <summary>Where the anchor sits in the sprite, 0 to 1 from its top-left: 0.5, 0.5 is its centre (top-down), 0.5, 1 its feet.</summary>
        public float PivotX;
        public float PivotY;
        /// <summary>Size at zoom 1, in art pixels; for an item <see cref="OnFloor"/>, the floor rectangle's size in world units.</summary>
        public float Width;
        public float Height;
        /// <summary>
        /// True for a rectangle lying on the floor (the fog's cloud, M3 design D6): centred on the anchor, sized in world
        /// units, drawn in floor space under the view's transform (<see cref="View.FloorToScreen"/>), so it takes the
        /// projection's shape. Its texture's top row lies along the rectangle's far (high y) edge.
        /// </summary>
        public bool OnFloor;
        /// <summary>Counter-clockwise in the world (y up), in radians, about the pivot.</summary>
        public float Rotation;
        /// <summary>Mirrored left to right (a cutscene character facing left).</summary>
        public bool FlipX;
        public Rgba32 Tint;
        public ulong SortKey;
        /// <summary>What a click on it selects (<see cref="DrawList.PickedEntity"/>), or -1 when it can't be clicked.</summary>
        public int Pick;
        /// <summary>
        /// Its anchor lands on a whole screen pixel (graphics V3.1, epic F19): a standing piece (a prop, a crawler, its
        /// shadow, the ring) then keeps one shape wherever it moves at a zoom, instead of shimmering as its sub-pixel
        /// position changes which texels the point sampling picks. Tiles and walls don't snap: they tile, and snapping each
        /// would open hairline gaps at in-between zooms.
        /// </summary>
        public bool SnapToPixel;
    }

    /// <summary>
    /// Where a draw item lands on screen through a view. The renderer draws it and the picker hit-tests it, so a click
    /// always lands on what is drawn.
    /// </summary>
    public readonly struct ScreenQuad
    {
        public ScreenQuad(Vec2 anchor, float width, float height, float pivotX, float pivotY)
        {
            Anchor = anchor;
            Width = width;
            Height = height;
            PivotX = pivotX;
            PivotY = pivotY;
        }

        /// <summary>The item's anchor on screen, in pixels from the top-left.</summary>
        public Vec2 Anchor { get; }
        /// <summary>Size in screen pixels.</summary>
        public float Width { get; }
        public float Height { get; }
        public float PivotX { get; }
        public float PivotY { get; }

        public float Left => Anchor.X - Width * PivotX;
        public float Top => Anchor.Y - Height * PivotY;
        public Vec2 Centre => new Vec2(Left + Width * 0.5f, Top + Height * 0.5f);

        public static ScreenQuad Of(in DrawItem item, in View view)
        {
            if (item.OnFloor) return FloorBounds(item, view);
            var anchor = view.WorldToScreen(new Vec2(item.X, item.Y), item.Z);
            if (item.SnapToPixel) anchor = new Vec2(SimMath.Round(anchor.X), SimMath.Round(anchor.Y));
            return new ScreenQuad(anchor, item.Width * view.Zoom, item.Height * view.Zoom, item.PivotX, item.PivotY);
        }

        /// <summary>A floor rectangle's screen bounds (the box round the shape the projection makes of it), centred on its centre's place.</summary>
        private static ScreenQuad FloorBounds(in DrawItem item, in View view)
        {
            float hw = item.Width * 0.5f, hh = item.Height * 0.5f;
            var a = view.WorldToScreen(new Vec2(item.X - hw, item.Y - hh));
            var b = view.WorldToScreen(new Vec2(item.X + hw, item.Y - hh));
            var c = view.WorldToScreen(new Vec2(item.X - hw, item.Y + hh));
            var d = view.WorldToScreen(new Vec2(item.X + hw, item.Y + hh));
            float width = SimMath.Max(SimMath.Max(a.X, b.X), SimMath.Max(c.X, d.X)) - SimMath.Min(SimMath.Min(a.X, b.X), SimMath.Min(c.X, d.X));
            float height = SimMath.Max(SimMath.Max(a.Y, b.Y), SimMath.Max(c.Y, d.Y)) - SimMath.Min(SimMath.Min(a.Y, b.Y), SimMath.Min(c.Y, d.Y));
            return new ScreenQuad(view.WorldToScreen(new Vec2(item.X, item.Y)), width, height, 0.5f, 0.5f);
        }

        /// <summary>
        /// True when any of the quad can land in a viewport of the given size: its rectangle, grown on every side by half
        /// its longer side (room for any turn about its pivot), meets the viewport. What doesn't is culled, and isn't in
        /// the picture.
        /// </summary>
        public bool Touches(int viewportWidth, int viewportHeight)
        {
            float grow = SimMath.Max(Width, Height) * 0.5f;
            return Left - grow < viewportWidth && Left + Width + grow > 0f && Top - grow < viewportHeight && Top + Height + grow > 0f;
        }
    }

    /// <summary>
    /// The draw order as one 64-bit key: the layer (<see cref="RenderLayer"/>, 8 bits), the depth within the layer (32),
    /// the role at equal depth (<see cref="DrawRole"/>, 4) and insertion order (20). Sorting by it is stable, which
    /// <c>SpriteBatch</c>'s own depth sort isn't, and allocates nothing. Lower keys are drawn first, so higher keys end
    /// up on top.
    /// </summary>
    public static class DrawSortKey
    {
        private const int LayerOffset = 128;
        /// <summary>The most items one frame can sort: insertion order has 20 bits.</summary>
        public const int MaxSequence = (1 << 20) - 1;

        /// <param name="layer">-128 to 127.</param>
        /// <param name="depth">Within a layer, higher depth draws later.</param>
        /// <param name="role">At equal depth, the earlier role draws first.</param>
        /// <param name="sequence">Insertion order, the last tie-break: 0 to <see cref="MaxSequence"/>.</param>
        public static ulong Make(int layer, float depth, DrawRole role, int sequence)
        {
            if (layer < -LayerOffset || layer >= LayerOffset) throw new ArgumentOutOfRangeException(nameof(layer), layer, "A draw layer is -128 to 127.");
            if ((uint)sequence > MaxSequence) throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "A frame sorts at most 2^20 items.");
            return ((ulong)(uint)(layer + LayerOffset) << 56) | ((ulong)OrderedBits(depth) << 24) | ((ulong)((uint)role & 0xFu) << 20) | (uint)sequence;
        }

        public static int Layer(ulong key) => (int)(key >> 56) - LayerOffset;

        public static DrawRole Role(ulong key) => (DrawRole)((key >> 20) & 0xFu);

        public static int Sequence(ulong key) => (int)(key & MaxSequence);

        /// <summary>A float's bits, flipped so that unsigned order is numeric order (NaN aside).</summary>
        private static uint OrderedBits(float value)
        {
            uint bits = BitConverter.SingleToUInt32Bits(value == 0f ? 0f : value); // -0 sorts as +0
            return (bits & 0x80000000u) != 0 ? ~bits : bits | 0x80000000u;
        }
    }

    /// <summary>
    /// A frame's sprites, filled by the scene builder and sorted once for drawing. Its arrays grow and are then
    /// reused, so a steady frame allocates nothing.
    /// </summary>
    public sealed class DrawList
    {
        private DrawItem[] _items = new DrawItem[256];
        private ulong[] _keys = new ulong[256];
        private EntityId[] _picks = new EntityId[64];
        private int _pickCount;

        public int Count { get; private set; }

        public ref readonly DrawItem this[int index] => ref _items[index];

        public void Clear()
        {
            Count = 0;
            _pickCount = 0;
        }

        /// <summary>
        /// Adds a sprite anchored at <paramref name="x"/>, <paramref name="y"/> on the floor, centred on it, sized in art
        /// pixels. Returns the item, to set what the defaults don't cover (height, pivot, <see cref="Pickable"/>).
        /// </summary>
        public ref DrawItem Add(int layer, DrawRole role, float depth, int spriteIndex, float x, float y, float width, float height, Rgba32 tint, float rotation = 0f, DrawSheet sheet = DrawSheet.World)
        {
            if (Count == _items.Length)
            {
                Array.Resize(ref _items, Count * 2);
                Array.Resize(ref _keys, Count * 2);
            }
            ulong key = DrawSortKey.Make(layer, depth, role, Count);
            _items[Count] = new DrawItem
            {
                Sheet = sheet, SpriteIndex = spriteIndex, X = x, Y = y, PivotX = 0.5f, PivotY = 0.5f, Width = width, Height = height,
                Rotation = rotation, Tint = tint, SortKey = key, Pick = -1,
            };
            _keys[Count] = key;
            return ref _items[Count++];
        }

        /// <summary>A pick slot for <paramref name="entity"/>, for an item's <see cref="DrawItem.Pick"/>: a click on the item selects it.</summary>
        public int Pickable(EntityId entity)
        {
            if (_pickCount == _picks.Length) Array.Resize(ref _picks, _pickCount * 2);
            _picks[_pickCount] = entity;
            return _pickCount++;
        }

        /// <summary>The entity behind a pick slot.</summary>
        public EntityId PickedEntity(int pick) => _picks[pick];

        /// <summary>
        /// Drops the items that can't land in <paramref name="view"/>'s viewport (<see cref="ScreenQuad.Touches"/>), before
        /// sorting (review R1): the cost of a frame follows what is on screen, not the map's size. What is culled isn't
        /// drawn and can't be clicked.
        /// </summary>
        public void Cull(in View view)
        {
            int kept = 0;
            for (int i = 0; i < Count; i++)
            {
                if (!ScreenQuad.Of(_items[i], view).Touches(view.ViewportWidth, view.ViewportHeight)) continue;
                if (kept != i)
                {
                    _items[kept] = _items[i];
                    _keys[kept] = _keys[i];
                }
                kept++;
            }
            Count = kept;
        }

        /// <summary>Puts the items in draw order. Keys are unique (the sequence), so the order is total and stable.</summary>
        public void Sort() => Array.Sort(_keys, _items, 0, Count);
    }
}
