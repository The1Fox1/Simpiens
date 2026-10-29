using System;
using System.Collections.Generic;

namespace Simpiens.Presentation.Art
{
    /// <summary>A thing's art in a catalogue (graphics V1): a dense id, resolved from its key once, at setup.</summary>
    public readonly struct ArtId : IEquatable<ArtId>
    {
        public ArtId(int value) => Value = value;

        public int Value { get; }

        public static ArtId None => new ArtId(-1);

        public bool IsNone => Value < 0;

        public bool Equals(ArtId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ArtId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(ArtId a, ArtId b) => a.Value == b.Value;
        public static bool operator !=(ArtId a, ArtId b) => a.Value != b.Value;
        public override string ToString() => IsNone ? "none" : Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// How one thing is drawn in one view mode (graphics V1): its sprite frames in a sheet, how fast they turn over,
    /// its size and pivot, and its pulse. A piece's size is <c>(Width × motion) × UnitScale</c>, in that order, so a
    /// table can keep sizes in world units (top-down: <see cref="UnitScale"/> is the art pixels a unit) or in art
    /// pixels (<see cref="UnitScale"/> 1); the motion is the pulse, or a crawler's squash.
    /// </summary>
    public readonly struct ArtDefinition
    {
        internal ArtDefinition(string key, DrawSheet sheet, int firstFrame, int frameCount, float framesPerSecond, float width, float height, float unitScale,
            float pivotX, float pivotY, float pulseRate, float pulseAmplitude)
        {
            Key = key;
            Sheet = sheet;
            FirstFrame = firstFrame;
            FrameCount = frameCount;
            FramesPerSecond = framesPerSecond;
            Width = width;
            Height = height;
            UnitScale = unitScale;
            PivotX = pivotX;
            PivotY = pivotY;
            PulseRate = pulseRate;
            PulseAmplitude = pulseAmplitude;
        }

        public string Key { get; }
        public DrawSheet Sheet { get; }
        /// <summary>Where its frames start in the catalogue's frame table (sprite indices in <see cref="Sheet"/>).</summary>
        public int FirstFrame { get; }
        public int FrameCount { get; }
        /// <summary>How fast the frames turn over with the view's clock; 0: the caller picks the frame (a crawler's pose).</summary>
        public float FramesPerSecond { get; }
        public float Width { get; }
        public float Height { get; }
        public float UnitScale { get; }
        /// <summary>Where the anchor sits in the sprite, 0 to 1 from its top-left.</summary>
        public float PivotX { get; }
        public float PivotY { get; }
        /// <summary>The size breathes by <c>1 + sin(t × rate) × amplitude</c>; amplitude 0: no pulse.</summary>
        public float PulseRate { get; }
        public float PulseAmplitude { get; }
    }

    /// <summary>
    /// What one view mode draws, as data (graphics V1, the sprite catalogue of Epic CLIENT M7 pulled forward): every
    /// thing's art by key (<see cref="ArtKeys"/>), resolved once to an <see cref="ArtId"/> so a frame reads arrays and
    /// never hashes a string or allocates. Built and checked by its <see cref="Builder"/>; immutable after.
    /// </summary>
    public sealed class ArtCatalog
    {
        private readonly ArtDefinition[] _definitions;
        private readonly int[] _frames;
        private readonly Dictionary<string, ArtId> _byKey;
        private readonly Dictionary<string, ArtId> _nodesByArtKey;
        private readonly Dictionary<string, ArtId>[] _items; // by ItemPicture, then the item's art key
        private readonly ArtId _missing;

        private ArtCatalog(string name, ArtDefinition[] definitions, int[] frames, Dictionary<string, ArtId> byKey, Dictionary<string, ArtId> nodes, Dictionary<string, ArtId>[] items, Builder builder)
        {
            CarryOffset = builder.CarryOffset;
            CarryHeight = builder.CarryHeight;
            DisplaySlots = builder.DisplaySlots ?? System.Array.Empty<(Vec2 Offset, float Height)>();
            DisplayPicture = builder.DisplayPicture;
            Vec2 nameOffset = builder.NameOffset;
            float nameHeight = builder.NameHeight;
            Name = name;
            _definitions = definitions;
            _frames = frames;
            _byKey = byKey;
            _nodesByArtKey = nodes;
            _items = items;
            _missing = byKey[ArtKeys.Missing];
            NameOffset = nameOffset;
            NameHeight = nameHeight;
        }

        public string Name { get; }

        public int Count => _definitions.Length;

        /// <summary>
        /// Where the selected crawler's name hangs: <see cref="NameOffset"/> on the floor from the crawler, then
        /// <see cref="NameHeight"/> world units above it. Its bottom sits there (<see cref="MapLabelLayout"/>).
        /// </summary>
        public Vec2 NameOffset { get; }

        public float NameHeight { get; }

        /// <summary>Where a crawler's carried goods hang (graphics V5): <see cref="CarryOffset"/> on the floor from it, <see cref="CarryHeight"/> units up.</summary>
        public Vec2 CarryOffset { get; }

        public float CarryHeight { get; }

        /// <summary>Where the stall shows its goods (graphics V5): each slot a floor offset from the stall and a height.</summary>
        public IReadOnlyList<(Vec2 Offset, float Height)> DisplaySlots { get; }

        /// <summary>Which of an item's pictures the stall shows.</summary>
        public ItemPicture DisplayPicture { get; }

        /// <summary>The id of <paramref name="key"/>; at setup only, never per frame. Throws for a key the catalogue doesn't have.</summary>
        public ArtId Id(string key)
            => _byKey.TryGetValue(key, out var id) ? id : throw new KeyNotFoundException($"The {Name} art catalogue has no '{key}'.");

        public bool TryId(string key, out ArtId id) => _byKey.TryGetValue(key, out id);

        public ref readonly ArtDefinition this[ArtId id] => ref _definitions[id.Value];

        /// <summary>Every key, in the order they were added.</summary>
        public IEnumerable<string> Keys
        {
            get
            {
                for (int i = 0; i < _definitions.Length; i++) yield return _definitions[i].Key;
            }
        }

        /// <summary>What a thing without art of its own is drawn with (<see cref="ArtKeys.Missing"/>).</summary>
        public ArtId Missing => _missing;

        /// <summary>
        /// The node that yields an item whose art key is <paramref name="itemArtKey"/> (<c>node/</c> + it), or
        /// <see cref="Missing"/> when the catalogue has none. No string is built: allocation-free per frame.
        /// </summary>
        public ArtId Node(string itemArtKey)
            => itemArtKey != null && _nodesByArtKey.TryGetValue(itemArtKey, out var id) ? id : _missing;

        /// <summary>An item's <paramref name="picture"/> (<see cref="ArtKeys.Item"/>), or <see cref="Missing"/>. One dictionary probe, no string built.</summary>
        public ArtId Picture(string itemArtKey, ItemPicture picture)
            => itemArtKey != null && _items[(int)picture].TryGetValue(itemArtKey, out var id) ? id : _missing;

        /// <summary>The sprite index of <paramref name="id"/>'s <paramref name="frame"/> (wrapped to its frame count).</summary>
        public int Sprite(ArtId id, int frame)
        {
            ref readonly var art = ref _definitions[id.Value];
            int f = frame % art.FrameCount;
            if (f < 0) f += art.FrameCount;
            return _frames[art.FirstFrame + f];
        }

        /// <summary>
        /// <paramref name="id"/> at <paramref name="viewSeconds"/>: the frame its rate gives (the first, without one), its
        /// size with its pulse.
        /// </summary>
        public ArtPiece Piece(ArtId id, float viewSeconds)
        {
            ref readonly var art = ref _definitions[id.Value];
            int frame = art.FramesPerSecond > 0f ? (int)(viewSeconds * art.FramesPerSecond) : 0;
            if (art.PulseAmplitude == 0f) return Make(art, Sprite(id, frame), art.Width * art.UnitScale, art.Height * art.UnitScale);
            float pulse = 1f + MathF.Sin(viewSeconds * art.PulseRate) * art.PulseAmplitude;
            return Make(art, Sprite(id, frame), art.Width * pulse * art.UnitScale, art.Height * pulse * art.UnitScale);
        }

        /// <summary><paramref name="id"/> in <paramref name="frame"/>, squashed by <paramref name="scaleX"/>, <paramref name="scaleY"/> (a crawler's pose).</summary>
        public ArtPiece Piece(ArtId id, int frame, float scaleX, float scaleY)
        {
            ref readonly var art = ref _definitions[id.Value];
            return Make(art, Sprite(id, frame), art.Width * scaleX * art.UnitScale, art.Height * scaleY * art.UnitScale);
        }

        private static ArtPiece Make(in ArtDefinition art, int sprite, float width, float height)
            => new ArtPiece(sprite, width, height, art.PivotX, art.PivotY, art.Sheet);

        /// <summary>Collects a catalogue's rows and checks them when it builds.</summary>
        public sealed class Builder
        {
            private readonly string _name;
            private readonly List<(string Key, DrawSheet Sheet, string[] Sprites, float Fps, float Width, float Height, float UnitScale, float PivotX, float PivotY, float PulseRate, float PulseAmplitude)> _rows
                = new List<(string, DrawSheet, string[], float, float, float, float, float, float, float, float)>();

            public Builder(string name) => _name = name ?? throw new ArgumentNullException(nameof(name));

            public Vec2 NameOffset { get; set; }

            public float NameHeight { get; set; }

            public Vec2 CarryOffset { get; set; }

            public float CarryHeight { get; set; }

            public (Vec2 Offset, float Height)[] DisplaySlots { get; set; }

            public ItemPicture DisplayPicture { get; set; } = ItemPicture.Map;

            /// <summary>
            /// A row: <paramref name="key"/> drawn from <paramref name="sprites"/> (by name, in <paramref name="sheet"/>),
            /// <paramref name="width"/> by <paramref name="height"/> in units of <paramref name="unitScale"/> art pixels.
            /// </summary>
            public Builder Add(string key, DrawSheet sheet, string[] sprites, float width, float height, float unitScale = 1f, float pivotX = 0.5f, float pivotY = 0.5f,
                float framesPerSecond = 0f, float pulseRate = 0f, float pulseAmplitude = 0f)
            {
                _rows.Add((key, sheet, sprites, framesPerSecond, width, height, unitScale, pivotX, pivotY, pulseRate, pulseAmplitude));
                return this;
            }

            /// <summary>Resolves every sprite name in its sheet; refuses a duplicate key, an unknown sprite, no frames, a negative size, or no missing art.</summary>
            public ArtCatalog Build()
            {
                var definitions = new ArtDefinition[_rows.Count];
                var frames = new List<int>();
                var byKey = new Dictionary<string, ArtId>(_rows.Count, StringComparer.Ordinal);
                var nodes = new Dictionary<string, ArtId>(StringComparer.Ordinal);
                var items = new Dictionary<string, ArtId>[3];
                for (int p = 0; p < items.Length; p++) items[p] = new Dictionary<string, ArtId>(StringComparer.Ordinal);
                for (int i = 0; i < _rows.Count; i++)
                {
                    var row = _rows[i];
                    if (string.IsNullOrEmpty(row.Key)) throw new ArgumentException($"The {_name} art catalogue has a row with no key.");
                    if (!byKey.TryAdd(row.Key, new ArtId(i))) throw new ArgumentException($"The {_name} art catalogue has '{row.Key}' twice.");
                    if (row.Sprites == null || row.Sprites.Length == 0) throw new ArgumentException($"'{row.Key}' has no frames.");
                    if (!(row.Width >= 0f && row.Height >= 0f && row.UnitScale > 0f)) throw new ArgumentException($"'{row.Key}' is {row.Width} by {row.Height} at scale {row.UnitScale}.");
                    var sheet = SpriteSheets.Contents(row.Sheet);
                    int first = frames.Count;
                    foreach (var sprite in row.Sprites)
                    {
                        if (!sheet.TryIndexOf(sprite, out int index)) throw new ArgumentException($"'{row.Key}' draws '{sprite}', which sheet {row.Sheet} doesn't have.");
                        frames.Add(index);
                    }
                    definitions[i] = new ArtDefinition(row.Key, row.Sheet, first, row.Sprites.Length, row.Fps, row.Width, row.Height, row.UnitScale,
                        row.PivotX, row.PivotY, row.PulseRate, row.PulseAmplitude);
                    if (row.Key.StartsWith(ArtKeys.NodePrefix, StringComparison.Ordinal)) nodes[row.Key.Substring(ArtKeys.NodePrefix.Length)] = new ArtId(i);
                    if (row.Key.StartsWith(ArtKeys.ItemPrefix, StringComparison.Ordinal)) IndexItem(items, row.Key, new ArtId(i));
                }
                if (!byKey.ContainsKey(ArtKeys.Missing)) throw new ArgumentException($"The {_name} art catalogue has no '{ArtKeys.Missing}': every catalogue draws something for art it lacks.");
                return new ArtCatalog(_name, definitions, frames.ToArray(), byKey, nodes, items, this);
            }

            /// <summary>Files an <c>item/&lt;art key&gt;/&lt;picture&gt;</c> row under its picture and art key; refuses an unknown picture.</summary>
            private void IndexItem(Dictionary<string, ArtId>[] items, string key, ArtId id)
            {
                int slash = key.LastIndexOf('/');
                string artKey = key.Substring(ArtKeys.ItemPrefix.Length, Math.Max(0, slash - ArtKeys.ItemPrefix.Length)), picture = key.Substring(slash + 1);
                for (int p = 0; p < items.Length; p++)
                {
                    if (picture != ArtKeys.PictureName((ItemPicture)p)) continue;
                    items[p][artKey] = id;
                    return;
                }
                throw new ArgumentException($"The {_name} art catalogue's '{key}' names no item picture (icon, map, carried).");
            }
        }
    }
}
