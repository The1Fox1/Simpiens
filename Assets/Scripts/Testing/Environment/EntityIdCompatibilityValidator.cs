using System;
using System.Runtime.InteropServices;
using Simpiens.Simulation;
using UnityEngine;

namespace Simpiens.Testing.Environment
{
    /// <summary>
    /// Epic CORE C3: <see cref="EntityId"/> is bit-compatible with <c>UnityEngine.GUID</c>, which it replaced. For seeded and
    /// random values, both give the same memory layout, equality, ordering, hash code (which keys <c>SimRandom</c>) and hex
    /// text, and parse each other's text. Unity-only: it compares against the engine type.
    /// </summary>
    public class EntityIdCompatibilityValidator
    {
        private const int Samples = 5000;

        public void RunAllValidationTests()
        {
            Debug.Log("[EntityIdCompatibilityValidator] Beginning Epic CORE C3 validation...");

            ValidateLayout();
            ValidateValuesMatchGuid();
            ValidateKnownIds();

            Debug.Log("<color=green>[EntityIdCompatibilityValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateLayout()
        {
            Assert(Marshal.SizeOf<EntityId>() == Marshal.SizeOf<UnityEngine.GUID>(), $"Same size ({Marshal.SizeOf<EntityId>()} vs {Marshal.SizeOf<UnityEngine.GUID>()} bytes).");
            var guid = new UnityEngine.GUID(0x01020304u, 0x05060708u, 0x090a0b0cu, 0x0d0e0f10u);
            var id = new EntityId(0x01020304u, 0x05060708u, 0x090a0b0cu, 0x0d0e0f10u);
            Assert(Bytes(guid).AsSpan().SequenceEqual(Bytes(id)), "Same bytes in memory: unmanaged save records keep their format.");
            Debug.Log("[PASS] Test 1: same size and memory layout as GUID.");
        }

        private void ValidateValuesMatchGuid()
        {
            var rng = new System.Random(20261017);
            var previousGuid = default(UnityEngine.GUID);
            var previousId = default(EntityId);
            for (int i = 0; i < Samples; i++)
            {
                uint a = Next(rng, i), b = Next(rng, i + 1), c = Next(rng, i + 2), d = Next(rng, i + 3);
                var guid = new UnityEngine.GUID(a, b, c, d);
                var id = new EntityId(a, b, c, d);

                Assert(id.GetHashCode() == guid.GetHashCode(), $"Hash code matches for {guid}.");
                Assert(id.ToString() == guid.ToString(), $"Hex text matches: {id} vs {guid}.");
                Assert(id.IsEmpty == guid.Empty(), $"Emptiness matches for {guid}.");
                Assert(Math.Sign(id.CompareTo(previousId)) == Math.Sign(guid.CompareTo(previousGuid)), $"Ordering matches for {guid} vs {previousGuid}.");
                Assert((id == previousId) == (guid == previousGuid), "Equality matches.");

                Assert(EntityId.TryParse(guid.ToString(), out var parsed) == UnityEngine.GUID.TryParse(guid.ToString(), out _), "TryParse agrees.");
                Assert(parsed == id || id.IsEmpty, $"EntityId parses GUID's text {guid}.");
                Assert(new UnityEngine.GUID(id.ToString()) == guid, $"GUID parses EntityId's text {id}.");

                previousGuid = guid;
                previousId = id;
            }
            Debug.Log($"[PASS] Test 2: {Samples} values match GUID's hash, text, emptiness, ordering, equality and parsing.");
        }

        private void ValidateKnownIds()
        {
            // The reserved player id is written as text
            var guid = new UnityEngine.GUID("00000000000000000000000000000001");
            var id = new EntityId("00000000000000000000000000000001");
            Assert(Bytes(guid).AsSpan().SequenceEqual(Bytes(id)), $"The player id's text gives the same id ({id} vs {guid}).");
            Assert(WorldRegistry.PlayerEntityId == id, "WorldRegistry.PlayerEntityId is the same id.");
            Assert(new EntityId("not an id").IsEmpty && !EntityId.TryParse("", out _), "Bad text gives the empty id.");
            Debug.Log("[PASS] Test 3: the player id and bad text behave as with GUID.");
        }

        // Spread values over small, large and repeated words so ordering ties are exercised
        private static uint Next(System.Random rng, int i)
        {
            switch (i % 5)
            {
                case 0: return 0u;
                case 1: return (uint)rng.Next(0, 4);
                default: return (uint)rng.Next() ^ ((uint)rng.Next() << 16);
            }
        }

        private static byte[] Bytes<T>(T value) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            var bytes = new byte[size];
            var handle = GCHandle.Alloc(value, GCHandleType.Pinned);
            try { Marshal.Copy(handle.AddrOfPinnedObject(), bytes, 0, size); }
            finally { handle.Free(); }
            return bytes;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception($"[EntityIdCompatibilityValidator ASSERTION FAILED] {message}");
        }
    }
}
