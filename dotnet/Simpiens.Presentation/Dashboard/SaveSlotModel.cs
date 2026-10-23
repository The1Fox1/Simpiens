using System.Threading.Tasks;
using Simpiens.Core.Session;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// The header's quick save and load (Unity's Save and Load buttons), as data. The buttons queue host commands
    /// (review D3); the host applies them before its next ticks and reports here: a save is captured then, between
    /// ticks, and written off the game thread, its outcome picked up by <see cref="Poll"/> on the game thread (the
    /// client has no synchronization context to resume on); a load replaces the running session, or says why it can't.
    /// </summary>
    public sealed class SaveSlotModel
    {
        private readonly ISaveGameService _slot;
        private readonly IHostCommands _commands;
        private Task<SaveOutcome> _pending;

        public SaveSlotModel(ISaveGameService slot, IHostCommands commands = null)
        {
            _slot = slot;
            _commands = commands;
        }

        /// <summary>Save and Load show only with a slot and a host to ask (Unity hid them without a slot).</summary>
        public bool Available => _slot != null && _commands != null;

        /// <summary>The last outcome, in Unity's words ("Saved at tick 300.", "There is no quick save yet.", ...).</summary>
        public string Status { get; private set; } = string.Empty;

        public bool IsSaving => _pending != null;

        /// <summary>Asks the host for a quick save.</summary>
        public void Save()
        {
            if (!Available || _pending != null) return;
            Status = "Saving...";
            _commands.Enqueue(HostCommand.QuickSave);
        }

        /// <summary>Asks the host for a quick load. A successful load raises the session's SessionChanged first, so the dashboard has rebuilt by the time it shows.</summary>
        public void Load()
        {
            if (!Available) return;
            if (_pending != null)
            {
                Status = "Wait for the save to finish.";
                return;
            }
            _commands.Enqueue(HostCommand.QuickLoad);
        }

        /// <summary>The host started writing a save (captured and encoded before the task began).</summary>
        public void Track(Task<SaveOutcome> save)
        {
            _pending = save;
            Poll();
        }

        /// <summary>The host's answer to a load, or why a save didn't start.</summary>
        public void Report(string message) => Status = message ?? string.Empty;

        /// <summary>Shows a finished save's outcome. Call once a frame.</summary>
        public void Poll()
        {
            if (_pending == null || !_pending.IsCompleted) return;
            Status = _pending.IsCompletedSuccessfully ? _pending.Result.Message : "The save could not be written.";
            _pending = null;
        }
    }
}
