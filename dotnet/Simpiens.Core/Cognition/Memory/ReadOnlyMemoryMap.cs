using System.Collections;
using System.Collections.Generic;

namespace Simpiens.Cognition.Memory
{
    /// <summary>
    /// A read-only view of one of a memory's maps, so cognition reading <see cref="IAgentMemory"/> off-thread cannot
    /// write it (single writer: the main thread). Made once per map, never per read; <c>foreach</c> binds to the
    /// dictionary's struct enumerator, so it allocates nothing, unlike enumerating it as an <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
    /// </summary>
    public sealed class ReadOnlyMemoryMap<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _map;

        public ReadOnlyMemoryMap(Dictionary<TKey, TValue> map) => _map = map;

        public int Count => _map.Count;

        public TValue this[TKey key] => _map[key];

        public bool ContainsKey(TKey key) => _map.ContainsKey(key);

        public bool TryGetValue(TKey key, out TValue value) => _map.TryGetValue(key, out value);

        public Dictionary<TKey, TValue>.Enumerator GetEnumerator() => _map.GetEnumerator();

        public IEnumerable<TKey> Keys => _map.Keys;

        public IEnumerable<TValue> Values => _map.Values;

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => _map.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _map.GetEnumerator();
    }
}
