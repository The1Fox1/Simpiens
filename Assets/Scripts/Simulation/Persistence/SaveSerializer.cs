using System;
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

        public static T Deserialize<T>(ReadOnlySpan<byte> bytes) => MemoryPackSerializer.Deserialize<T>(bytes);
    }
}
