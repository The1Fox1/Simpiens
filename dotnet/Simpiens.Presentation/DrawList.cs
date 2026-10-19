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

    /// <summary>One sprite to draw this frame, in world units: centred on <see cref="X"/>, <see cref="Y"/>.</summary>
    public struct DrawItem
    {
        public DrawSheet Sheet;
        public int SpriteIndex;
        public float X;
        public float Y;
        public float Width;
        public float Height;
        /// <summary>Counter-clockwise in the world (y up), in radians, about the sprite's centre.</summary>
        public float Rotation;
        public Rgba32 Tint;
        public ulong SortKey;
    }

    /// <summary>
    /// The draw order as one 64-bit key: the layer (<see cref="RenderLayer"/>), then the depth within the layer, then
    /// insertion order. Sorting by it is stable, which <c>SpriteBatch</c>'s own depth sort isn't, and allocates nothing.
    /// Lower keys are drawn first, so higher keys end up on top.
    /// </summary>
    public static class DrawSortKey
    {
        private const int LayerOffset = 128;

        /// <param name="layer">-128 to 127.</param>
        /// <param name="depth">Within a layer, higher depth draws later.</param>
        /// <param name="sequence">Insertion order, the tie-break: 0 to 2^24 - 1.</param>
        public static ulong Make(int layer, float depth, int sequence)
        {
            if (layer < -LayerOffset || layer >= LayerOffset) throw new ArgumentOutOfRangeException(nameof(layer), layer, "A draw layer is -128 to 127.");
            return ((ulong)(uint)(layer + LayerOffset) << 56) | ((ulong)OrderedBits(depth) << 24) | ((uint)sequence & 0xFFFFFFu);
        }

        public static int Layer(ulong key) => (int)(key >> 56) - LayerOffset;

        public static int Sequence(ulong key) => (int)(key & 0xFFFFFFu);

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

        public int Count { get; private set; }

        public ref readonly DrawItem this[int index] => ref _items[index];

        public void Clear() => Count = 0;

        public void Add(int layer, float depth, int spriteIndex, float x, float y, float width, float height, Rgba32 tint, float rotation = 0f, DrawSheet sheet = DrawSheet.Sprites)
        {
            if (Count == _items.Length)
            {
                Array.Resize(ref _items, Count * 2);
                Array.Resize(ref _keys, Count * 2);
            }
            ulong key = DrawSortKey.Make(layer, depth, Count);
            _items[Count] = new DrawItem { Sheet = sheet, SpriteIndex = spriteIndex, X = x, Y = y, Width = width, Height = height, Rotation = rotation, Tint = tint, SortKey = key };
            _keys[Count] = key;
            Count++;
        }

        /// <summary>Puts the items in draw order. Keys are unique (the sequence), so the order is total and stable.</summary>
        public void Sort() => Array.Sort(_keys, _items, 0, Count);
    }
}
