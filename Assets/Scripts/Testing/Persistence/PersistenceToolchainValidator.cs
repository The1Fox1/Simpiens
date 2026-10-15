using System;
using MemoryPack;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Persistence;
using Unity.Collections.LowLevel.Unsafe;
using Debug = UnityEngine.Debug;

namespace Simpiens.Testing.Persistence
{
    /// <summary>
    /// Milestone 1 completion plan, step 0: MemoryPack is installed. Its source generator runs in the production
    /// assembly and in this one, and unmanaged records are saved as raw memory copies (so their layout is the format).
    /// </summary>
    public class PersistenceToolchainValidator
    {
        public void RunAllValidationTests()
        {
            Debug.Log("[PersistenceToolchainValidator] Beginning MemoryPack toolchain validation...");

            ValidateProductionTypeRoundTrips();
            ValidateTestAssemblyTypeRoundTrips();
            ValidateUnmanagedRecordsAreMemoryCopies();

            Debug.Log("<color=green>[PersistenceToolchainValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        /// <summary>The generator ran in Simpiens: the save header round-trips field for field.</summary>
        private void ValidateProductionTypeRoundTrips()
        {
            var header = new SessionSaveHeader
            {
                BuildVersion = "0.1.0",
                SessionId = "outpost",
                MapFingerprint = 0xF1F2F3F4F5F6F7F8UL,
                Seed = 20261009UL,
                Tick = 3000,
                EntityIdCounter = 57,
            };

            byte[] bytes = SaveSerializer.Serialize(header);
            var back = SaveSerializer.Deserialize<SessionSaveHeader>(bytes);

            Assert(back != null, "The header deserializes.");
            Assert(back.FormatVersion == SessionSaveHeader.CurrentFormatVersion, $"Format version {back.FormatVersion} (expected {SessionSaveHeader.CurrentFormatVersion}).");
            Assert(back.BuildVersion == "0.1.0" && back.SessionId == "outpost", "Strings round-trip.");
            Assert(back.MapFingerprint == header.MapFingerprint && back.Seed == header.Seed, "Fingerprint and seed round-trip.");
            Assert(back.Tick == 3000 && back.EntityIdCounter == 57, "Tick and id counter round-trip.");
        }

        /// <summary>The generator also ran in Simpiens.Testing: a type declared here round-trips.</summary>
        private void ValidateTestAssemblyTypeRoundTrips()
        {
            var probe = new ToolchainProbe { Name = "probe", Values = new[] { 3, 1, 4, 1, 5 } };

            var back = SaveSerializer.Deserialize<ToolchainProbe>(SaveSerializer.Serialize(probe));

            Assert(back != null && back.Name == "probe", "The test assembly's type round-trips its string.");
            Assert(back.Values != null && back.Values.Length == 5 && back.Values[2] == 4, "The test assembly's type round-trips its array.");
        }

        /// <summary>
        /// An array of unmanaged records is a 4-byte length followed by the records' raw memory: no per-field code.
        /// That is why changing a saved struct's layout changes the save format.
        /// </summary>
        private void ValidateUnmanagedRecordsAreMemoryCopies()
        {
            var items = new[]
            {
                new ItemInstance(1, ItemType.Ration, 3, 5),
                new ItemInstance(2, ItemType.IronOre, 1, 8, quality: 1, durability: 60),
                new ItemInstance(3, ItemType.MedicinalHerb, 2, 12, quality: 2, durability: 99),
            };

            byte[] bytes = SaveSerializer.Serialize(items);
            int expected = sizeof(int) + items.Length * UnsafeUtility.SizeOf<ItemInstance>();
            Assert(bytes.Length == expected, $"ItemInstance[3] is {bytes.Length} bytes (expected a 4-byte length plus 3 raw records: {expected}).");

            var back = SaveSerializer.Deserialize<ItemInstance[]>(bytes);
            Assert(back != null && back.Length == items.Length, "The array length round-trips.");
            for (int i = 0; i < items.Length; i++)
            {
                Assert(back[i].Equals(items[i]), $"Item {i} round-trips.");
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"[ASSERTION FAILED] {message}");
            }
        }
    }

    /// <summary>A type declared in the test assembly, to prove the generator runs here too.</summary>
    [MemoryPackable]
    internal partial class ToolchainProbe
    {
        public string Name { get; set; }
        public int[] Values { get; set; }
    }
}
