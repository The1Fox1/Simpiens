using System;
using System.Collections.Generic;

namespace Simpiens.Presentation
{
    /// <summary>What a <see cref="HostCommand"/> asks the session's host to do.</summary>
    public enum HostCommandType
    {
        /// <summary>Run at <see cref="HostCommand.Speed"/> times real time (kept through a pause).</summary>
        SetSpeed,
        /// <summary>Stop the clock; the speed is kept for the resume.</summary>
        Pause,
        /// <summary>Run again at the kept speed.</summary>
        Resume,
        /// <summary>Capture the session now and write it to the quick-save slot.</summary>
        QuickSave,
        /// <summary>Replace the session with the quick save.</summary>
        QuickLoad,
        /// <summary>Respawn as the registered session <see cref="HostCommand.SessionId"/>.</summary>
        SwitchSession,
    }

    /// <summary>
    /// A request to whoever runs the session (review D3): its speed, a pause, a save, a load or a switch. These are the
    /// host's decisions, not a view's, so the dashboard and the keys only queue them (<see cref="IHostCommands"/>), and
    /// the host applies them between frames, before the ticks, the way the player's influence enters as typed commands
    /// (ROADMAP §1A.1). In a multiplayer game the same commands would travel to the server.
    /// </summary>
    public readonly struct HostCommand : IEquatable<HostCommand>
    {
        private HostCommand(HostCommandType type, float speed = 0f, string sessionId = null)
        {
            Type = type;
            Speed = speed;
            SessionId = sessionId;
        }

        public HostCommandType Type { get; }

        /// <summary><see cref="HostCommandType.SetSpeed"/>: the multiple of real time, above zero.</summary>
        public float Speed { get; }

        /// <summary><see cref="HostCommandType.SwitchSession"/>: the session's id.</summary>
        public string SessionId { get; }

        public static HostCommand SetSpeed(float speed)
            => speed > 0f ? new HostCommand(HostCommandType.SetSpeed, speed) : throw new ArgumentOutOfRangeException(nameof(speed), speed, "A speed is above zero; pausing is its own command.");

        public static readonly HostCommand Pause = new HostCommand(HostCommandType.Pause);
        public static readonly HostCommand Resume = new HostCommand(HostCommandType.Resume);
        public static readonly HostCommand QuickSave = new HostCommand(HostCommandType.QuickSave);
        public static readonly HostCommand QuickLoad = new HostCommand(HostCommandType.QuickLoad);

        public static HostCommand SwitchSession(string sessionId)
            => !string.IsNullOrEmpty(sessionId) ? new HostCommand(HostCommandType.SwitchSession, sessionId: sessionId) : throw new ArgumentException("A switch names its session.", nameof(sessionId));

        public bool Equals(HostCommand other) => Type == other.Type && Speed.Equals(other.Speed) && string.Equals(SessionId, other.SessionId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is HostCommand other && Equals(other);

        public override int GetHashCode() => HashCode.Combine((int)Type, Speed, SessionId);

        public override string ToString() => Type switch
        {
            HostCommandType.SetSpeed => $"SetSpeed({Speed}x)",
            HostCommandType.SwitchSession => $"SwitchSession({SessionId})",
            _ => Type.ToString(),
        };
    }

    /// <summary>Where the views send <see cref="HostCommand"/>s: the host applies them before its next ticks.</summary>
    public interface IHostCommands
    {
        void Enqueue(in HostCommand command);
    }

    /// <summary>The client's queue of host commands, applied in the order they came (<see cref="Drain"/>).</summary>
    public sealed class HostCommandQueue : IHostCommands
    {
        private List<HostCommand> _pending = new List<HostCommand>(8);
        private List<HostCommand> _draining = new List<HostCommand>(8);

        public int Count => _pending.Count;

        public void Enqueue(in HostCommand command) => _pending.Add(command);

        /// <summary>
        /// The queued commands, oldest first, and an empty queue. A command queued while these are applied (a load
        /// raising events that queue another) waits for the next drain. The list is reused: read it before draining again.
        /// </summary>
        public IReadOnlyList<HostCommand> Drain()
        {
            (_pending, _draining) = (_draining, _pending);
            _pending.Clear();
            return _draining;
        }
    }
}
