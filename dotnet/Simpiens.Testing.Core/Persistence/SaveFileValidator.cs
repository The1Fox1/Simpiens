using System;
using System.IO;
using Simpiens.Core.Session;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Persistence;

namespace Simpiens.Testing.Persistence
{
    /// <summary>
    /// Milestone 1 completion plan, step 4: saves as files. A file round-trips; a file from another save format, a
    /// cut-off file, a damaged one and one that isn't a save are each refused with a reason before anything is loaded;
    /// the quick-save slot writes through a temporary file and loads back, and refuses a missing or bad slot.
    /// </summary>
    public class SaveFileValidator
    {
        public void RunAllValidationTests()
        {
            TestLog.Log("[SaveFileValidator] Beginning save file validation...");

            ValidateFileRoundTrips();
            ValidateBadFilesAreRefused();
            ValidateQuickSaveSlot();

            TestLog.Log("<color=green>[SaveFileValidator] ALL 3 TESTS PASSED SUCCESSFULLY!</color>");
        }

        private void ValidateFileRoundTrips()
        {
            var save = SampleSave(1234);
            byte[] file = SaveFile.Encode(save);

            Assert(file.Length > SaveFile.HeaderSize, "A save file holds a header and the game.");
            Assert(SaveFile.TryDecode(file, out var back, out string refusal), $"A save file reads back ({refusal}).");
            Assert(back.Header.Tick == 1234 && back.Economy.Stalls.Length == 1 && back.Economy.Stalls[0].TreasuryGold == 321, "The game in the file is the game that was saved.");
            AssertSameBytes(file, SaveFile.Encode(back), "Encoding the decoded save");

            TestLog.Log($"[PASS] Test 1: a save file ({file.Length} bytes) round-trips.");
        }

        private void ValidateBadFilesAreRefused()
        {
            byte[] good = SaveFile.Encode(SampleSave(50));

            var otherFormat = (byte[])good.Clone();
            otherFormat[8] = (byte)(SessionSaveHeader.CurrentFormatVersion + 1);
            ExpectRefusal(otherFormat, "format", "a save from another format");

            var cutOff = new byte[good.Length - 7];
            Array.Copy(good, cutOff, cutOff.Length);
            ExpectRefusal(cutOff, "incomplete", "a cut-off save");

            var damaged = (byte[])good.Clone();
            damaged[damaged.Length - 3] ^= 0x5A;
            ExpectRefusal(damaged, "damaged", "a damaged save");

            ExpectRefusal(System.Text.Encoding.UTF8.GetBytes("not a save at all, just text"), "not a Simpiens save", "a text file");
            ExpectRefusal(Array.Empty<byte>(), "not a Simpiens save", "an empty file");

            TestLog.Log("[PASS] Test 2: another format, a cut-off file, a damaged file and a file that isn't a save are each refused with a reason.");
        }

        private void ExpectRefusal(byte[] file, string expectedWords, string what)
        {
            bool read = SaveFile.TryDecode(file, out var save, out string refusal);
            Assert(!read && save == null, $"{what} was read.");
            Assert(!string.IsNullOrEmpty(refusal) && refusal.IndexOf(expectedWords, StringComparison.OrdinalIgnoreCase) >= 0,
                $"{what} was refused with '{refusal}', expected it to mention '{expectedWords}'.");
        }

        private void ValidateQuickSaveSlot()
        {
            string directory = Path.Combine(Path.GetTempPath(), "Simpiens", "SaveFileValidator");
            if (Directory.Exists(directory)) Directory.Delete(directory, true);

            var session = new FakeSession { Next = SampleSave(777) };
            var slot = new SaveGameService(session, directory);

            var missing = slot.QuickLoad();
            Assert(!missing.Succeeded && missing.Message.Contains("no quick save"), $"Loading an empty slot is refused ({missing.Message}).");

            var saved = slot.QuickSave();
            Assert(saved.Succeeded && saved.Tick == 777, $"The quick save succeeded at t777 ({saved.Message}).");
            Assert(slot.HasQuickSave && !File.Exists(slot.QuickSavePath + ".tmp"), "The slot holds the save and no temporary file is left.");

            session.Next = SampleSave(999);
            var overwritten = slot.QuickSave();
            Assert(overwritten.Succeeded, $"A second save replaces the first ({overwritten.Message}).");

            var loaded = slot.QuickLoad();
            Assert(loaded.Succeeded && loaded.Tick == 999 && session.Restored?.Header.Tick == 999, $"The slot loads the latest save ({loaded.Message}).");

            session.RefuseWith = "The save is of a session this game doesn't have ('elsewhere').";
            var refused = slot.QuickLoad();
            Assert(!refused.Succeeded && refused.Message == session.RefuseWith, "A refusal from the session reaches the player.");

            File.WriteAllBytes(slot.QuickSavePath, new byte[] { 1, 2, 3 });
            var bad = slot.QuickLoad();
            Assert(!bad.Succeeded && bad.Message.Contains("not a Simpiens save"), $"A bad slot is refused ({bad.Message}).");

            Directory.Delete(directory, true);
            TestLog.Log("[PASS] Test 3: the quick-save slot writes, replaces and loads its save, and refuses an empty, refused or bad slot.");
        }

        private static SessionSave SampleSave(long tick)
        {
            return new SessionSave
            {
                Header = new SessionSaveHeader { BuildVersion = "test", SessionId = "outpost", MapFingerprint = 42UL, Seed = 7UL, Tick = tick, EntityIdCounter = 30 },
                Crawlers = Array.Empty<CrawlerSave>(),
                Economy = new EconomySave
                {
                    Stalls = new[] { new TradeStallData(new EntityId(1u, 2u, 3u, 4u), Vec2.One, treasuryGold: 321) },
                    Bounties = Array.Empty<BountyContract>(),
                    Ledger = new LedgerSave()
                },
                World = new WorldSave(),
                Groups = new GroupSave(),
                Chronicle = new ChronicleSave()
            };
        }

        private sealed class FakeSession : ISessionPersistence
        {
            public SessionSave Next;
            public SessionSave Restored;
            public string RefuseWith;

            public SessionSave Capture() => Next;

            public bool TryRestore(SessionSave save, out string refusal)
            {
                refusal = RefuseWith;
                if (refusal != null) return false;
                Restored = save;
                return true;
            }
        }

        private static void AssertSameBytes(byte[] a, byte[] b, string what)
        {
            Assert(a.Length == b.Length, $"{what}: {a.Length} vs {b.Length} bytes.");
            for (int i = 0; i < a.Length; i++)
            {
                Assert(a[i] == b[i], $"{what}: the bytes differ at offset {i}.");
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
}
