using System;
using System.Collections;
using System.Collections.Generic;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// The sprites of one <see cref="DrawSheet"/>, in order (graphics V1): a sprite's index is its place here, and only
    /// the sheet gives it out. A draw item's <see cref="DrawItem.SpriteIndex"/> is an index into its sheet; the client
    /// packs the sheet into one texture. Immutable; built once.
    /// </summary>
    public sealed class SpriteSheet : IReadOnlyList<PixelSprite>
    {
        private readonly PixelSprite[] _sprites;
        private readonly Dictionary<string, int> _byName;
        private readonly Dictionary<PixelSprite, int> _bySprite;

        public SpriteSheet(DrawSheet id, IEnumerable<PixelSprite> sprites)
        {
            Id = id;
            _sprites = new List<PixelSprite>(sprites ?? throw new ArgumentNullException(nameof(sprites))).ToArray();
            _byName = new Dictionary<string, int>(_sprites.Length, StringComparer.Ordinal);
            _bySprite = new Dictionary<PixelSprite, int>(_sprites.Length, ReferenceEqualityComparer.Instance);
            for (int i = 0; i < _sprites.Length; i++)
            {
                if (!_byName.TryAdd(_sprites[i].Name, i)) throw new ArgumentException($"Sheet {id} has two sprites named '{_sprites[i].Name}'.", nameof(sprites));
                _bySprite[_sprites[i]] = i;
            }
        }

        public DrawSheet Id { get; }

        public int Count => _sprites.Length;

        public PixelSprite this[int index] => _sprites[index];

        /// <summary>The index of the sprite named <paramref name="name"/>; throws for a name the sheet doesn't have.</summary>
        public int IndexOf(string name)
            => _byName.TryGetValue(name, out int index) ? index : throw new KeyNotFoundException($"Sheet {Id} has no sprite named '{name}'.");

        /// <summary>The index of <paramref name="sprite"/> (by reference; no string is hashed); throws for a sprite the sheet doesn't hold.</summary>
        public int IndexOf(PixelSprite sprite)
            => _bySprite.TryGetValue(sprite, out int index) ? index : throw new KeyNotFoundException($"Sheet {Id} doesn't hold {sprite}.");

        public bool TryIndexOf(string name, out int index) => _byName.TryGetValue(name, out index);

        public IEnumerator<PixelSprite> GetEnumerator() => ((IEnumerable<PixelSprite>)_sprites).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
