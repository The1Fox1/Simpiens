using System;
using System.Buffers;
using MemoryPack;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>
    /// The only code that calls MemoryPack (Milestone 1 save/load). Saves go through here, so the serializer can be
    /// replaced without touching the code that captures and restores state.
    /// Unmanaged records (structs without references, such as <see cref="Economy.ItemInstance"/>) are copied as raw
    /// memory, so their layout is the save format; <c>[MemoryPackable]</c> containers use generated code.
    /// </summary>
    public static class SaveSerializer
    {
        public static byte[] Serialize<T>(T value) => MemoryPackSerializer.Serialize(value);

        /// <summary>
        /// Appends <paramref name="value"/> to <paramref name="buffer"/>. With a buffer that is kept and cleared between
        /// saves this allocates nothing once warm, for unmanaged records, arrays of them and containers holding only those.
        /// </summary>
        public static void Serialize<T>(ArrayBufferWriter<byte> buffer, in T value) =>
            MemoryPackSerializer.Serialize<T, ArrayBufferWriter<byte>>(buffer, value);

        public static T Deserialize<T>(ReadOnlySpan<byte> bytes) => MemoryPackSerializer.Deserialize<T>(bytes);

        /// <summary>
        /// Reads over <paramref name="value"/>: an existing container is filled in place, and its arrays are reused when
        /// the saved length matches, so loading into kept objects allocates nothing (strings excepted). Returns the
        /// number of bytes read.
        /// </summary>
        public static int Deserialize<T>(ReadOnlySpan<byte> bytes, ref T value) =>
            MemoryPackSerializer.Deserialize(bytes, ref value);
    }
}
