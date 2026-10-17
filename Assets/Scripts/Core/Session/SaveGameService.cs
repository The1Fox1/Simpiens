using System;
using System.Threading.Tasks;
using System.IO;
using Simpiens.Diagnostics;
using Simpiens.Simulation.Persistence;
using ZLogger;

namespace Simpiens.Core.Session
{
    /// <summary>Captures and loads whole sessions (implemented by <see cref="SwarmSpawner"/>). Main thread.</summary>
    public interface ISessionPersistence
    {
        /// <summary>The whole session at the current tick. Between ticks.</summary>
        SessionSave Capture();

        /// <summary>Loads a save over the running session, or refuses with a player-facing reason and changes nothing.</summary>
        bool TryRestore(SessionSave save, out string refusal);
    }

    /// <summary>What a save or a load did, in words the player can read.</summary>
    public readonly struct SaveOutcome
    {
        public readonly bool Succeeded;
        public readonly long Tick;
        public readonly string Message;

        public SaveOutcome(bool succeeded, long tick, string message)
        {
            Succeeded = succeeded;
            Tick = tick;
            Message = message;
        }
    }

    /// <summary>The player's save slot (Milestone 1: one quick-save slot).</summary>
    public interface ISaveGameService
    {
        string QuickSavePath { get; }
        bool HasQuickSave { get; }

        /// <summary>True while a save is being written: a second save waits for the first.</summary>
        bool IsSaving { get; }

        /// <summary>Captures the session now, between ticks, and writes it to the slot off the main thread (the dashboard).</summary>
        Task<SaveOutcome> QuickSaveAsync();

        /// <summary>Captures the session now and writes it to the slot before returning (on quit, in tests).</summary>
        SaveOutcome QuickSave();

        /// <summary>Loads the slot into the running game, or refuses with a reason and changes nothing.</summary>
        SaveOutcome QuickLoad();
    }

    /// <summary>
    /// The quick-save slot: <c>Saves/quicksave.simsave</c> under the folder the host gives it (Unity's persistent data
    /// path in the game). A save is captured on the
    /// main thread between ticks, encoded there (a few tens of kilobytes) and written on the thread pool, through a
    /// temporary file so a crash mid-write never leaves half a save in the slot. A load reads and checks the file
    /// (<see cref="SaveFile"/>), then hands it to the session (<see cref="ISessionPersistence.TryRestore"/>), which
    /// refuses a save for another session, seed or map.
    /// </summary>
    public sealed class SaveGameService : ISaveGameService
    {
        public const string QuickSaveName = "quicksave";

        private readonly ISessionPersistence _session;

        public SaveGameService(ISessionPersistence session, string directory)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            Directory = directory ?? throw new ArgumentNullException(nameof(directory));
            QuickSavePath = Path.Combine(Directory, QuickSaveName + SaveFile.Extension);
        }

        public string Directory { get; }
        public string QuickSavePath { get; }
        public bool HasQuickSave => File.Exists(QuickSavePath);
        public bool IsSaving { get; private set; }

        public async Task<SaveOutcome> QuickSaveAsync()
        {
            if (IsSaving) return new SaveOutcome(false, -1, "A save is already being written.");
            if (!TryEncode(out byte[] file, out long tick, out var failed)) return failed;

            IsSaving = true;
            try
            {
                string path = QuickSavePath;
                await Task.Run(() => WriteThroughTemporaryFile(path, file));
                return Written(tick, file.Length);
            }
            catch (Exception e)
            {
                return WriteFailed(tick, e);
            }
            finally
            {
                IsSaving = false;
            }
        }

        public SaveOutcome QuickSave()
        {
            if (IsSaving) return new SaveOutcome(false, -1, "A save is already being written.");
            if (!TryEncode(out byte[] file, out long tick, out var failed)) return failed;

            try
            {
                WriteThroughTemporaryFile(QuickSavePath, file);
                return Written(tick, file.Length);
            }
            catch (Exception e)
            {
                return WriteFailed(tick, e);
            }
        }

        /// <summary>Captures and encodes on the calling (main) thread: the capture must happen between ticks.</summary>
        private bool TryEncode(out byte[] file, out long tick, out SaveOutcome failed)
        {
            try
            {
                var save = _session.Capture();
                file = SaveFile.Encode(save);
                tick = save.Header.Tick;
                failed = default;
                return true;
            }
            catch (Exception e)
            {
                SimLog.Bootstrap.ZLogError(e, $"Quick save failed while capturing the session");
                file = null;
                tick = -1;
                failed = new SaveOutcome(false, -1, "The game could not be saved.");
                return false;
            }
        }

        private SaveOutcome Written(long tick, int bytes)
        {
            SimLog.Bootstrap.ZLogInformation($"Quick save written at t{tick}: {bytes} bytes to {QuickSavePath}");
            return new SaveOutcome(true, tick, $"Saved at tick {tick}.");
        }

        private SaveOutcome WriteFailed(long tick, Exception e)
        {
            SimLog.Bootstrap.ZLogError(e, $"Quick save failed while writing {QuickSavePath}");
            return new SaveOutcome(false, tick, $"The save could not be written ({e.GetType().Name}).");
        }

        public SaveOutcome QuickLoad()
        {
            if (IsSaving) return new SaveOutcome(false, -1, "Wait for the save to finish.");
            if (!HasQuickSave) return new SaveOutcome(false, -1, "There is no quick save yet.");

            byte[] file;
            try
            {
                file = File.ReadAllBytes(QuickSavePath);
            }
            catch (Exception e)
            {
                SimLog.Bootstrap.ZLogWarning($"Quick load could not read {QuickSavePath}: {e.Message}");
                return new SaveOutcome(false, -1, $"The save could not be read ({e.GetType().Name}).");
            }

            if (!SaveFile.TryDecode(file, out var save, out string refusal) || !_session.TryRestore(save, out refusal))
            {
                SimLog.Bootstrap.ZLogWarning($"Quick load refused: {refusal}");
                return new SaveOutcome(false, -1, refusal);
            }
            return new SaveOutcome(true, save.Header.Tick, $"Loaded the save from tick {save.Header.Tick}.");
        }

        private static void WriteThroughTemporaryFile(string path, byte[] bytes)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) System.IO.Directory.CreateDirectory(directory);

            string temporary = path + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }
}
