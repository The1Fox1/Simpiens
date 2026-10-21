using System;

namespace Simpiens.Presentation
{
    /// <summary>
    /// Which image a draw item's sprite index refers to. The renderer starts a new batch when the sheet changes;
    /// each sheet has its own sampling (pixel art is point-sampled).
    /// </summary>
    public enum DrawSheet : byte
    {
        /// <summary><see cref="ProceduralSprites.All"/>.</summary>
        Sprites = 0,
        /// <summary><see cref="FogCloud.Sprite"/>, sampled smoothly.</summary>
        Fog = 1,
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
        /// <summary>Size at zoom 1, in art pixels.</summary>
        public float Width;
        public float Height;
        /// <summary>Counter-clockwise in the world (y up), in radians, about the pivot.</summary>
        public float Rotation;
        public Rgba32 Tint;
        public ulong SortKey;
        /// <summary>What a click on it selects (<see cref="DrawList.PickedEntity"/>), or -1 when it can't be clicked.</summary>
        public int Pick;
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
            => new ScreenQuad(view.WorldToScreen(new Vec2(item.X, item.Y), item.Z), item.Width * view.Zoom, item.Height * view.Zoom, item.PivotX, item.PivotY);
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
        public ref DrawItem Add(int layer, DrawRole role, float depth, int spriteIndex, float x, float y, float width, float height, Rgba32 tint, float rotation = 0f, DrawSheet sheet = DrawSheet.Sprites)
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

        /// <summary>Puts the items in draw order. Keys are unique (the sequence), so the order is total and stable.</summary>
        public void Sort() => Array.Sort(_keys, _items, 0, Count);
    }
}
