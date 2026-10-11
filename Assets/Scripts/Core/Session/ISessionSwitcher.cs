using System;
using System.Collections.Generic;

namespace Simpiens.Core.Session
{
    /// <summary>
    /// Switches the running game between registered sessions. Every switch is a full respawn with the same seed:
    /// nothing carries across. Main thread only.
    /// </summary>
    public interface ISessionSwitcher
    {
        IReadOnlyList<ISessionDefinition> Sessions { get; }

        /// <summary>The session spawned by the last respawn (the default session before the first one).</summary>
        ISessionDefinition Current { get; }

        void SwitchTo(ISessionDefinition session);

        /// <summary>Raised after a session has been spawned.</summary>
        event Action<ISessionDefinition> SessionChanged;
    }
}
