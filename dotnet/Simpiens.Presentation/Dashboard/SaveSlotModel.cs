using System.Threading.Tasks;
using Simpiens.Core.Session;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// The header's quick save and load (Unity's Save and Load buttons): Save captures the session now, between ticks,
    /// and writes it off the game thread; the outcome is picked up by <see cref="Poll"/> on the game thread (the client
    /// has no synchronization context to resume on). Load replaces the running session, or says why it can't.
    /// </summary>
    public sealed class SaveSlotModel
    {
        private readonly ISaveGameService _slot;
        private Task<SaveOutcome> _pending;

        public SaveSlotModel(ISaveGameService slot) => _slot = slot;

        /// <summary>Save and Load show only with a slot (Unity hid them without one).</summary>
        public bool Available => _slot != null;

        /// <summary>The last outcome, in Unity's words ("Saved at tick 300.", "There is no quick save yet.", ...).</summary>
        public string Status { get; private set; } = string.Empty;

        public bool IsSaving => _pending != null;

        public void Save()
        {
            if (_slot == null || _pending != null) return;
            Status = "Saving...";
            _pending = _slot.QuickSaveAsync(); // captured and encoded before this returns; written on the thread pool
            Poll();
        }

        /// <summary>Shows a finished save's outcome. Call once a frame.</summary>
        public void Poll()
        {
            if (_pending == null || !_pending.IsCompleted) return;
            Status = _pending.IsCompletedSuccessfully ? _pending.Result.Message : "The save could not be written.";
            _pending = null;
        }

        /// <summary>A successful load raises the session's SessionChanged first, so the dashboard has already rebuilt.</summary>
        public void Load()
        {
            if (_slot == null) return;
            if (_pending != null)
            {
                Status = "Wait for the save to finish.";
                return;
            }
            Status = _slot.QuickLoad().Message;
        }
    }
}
