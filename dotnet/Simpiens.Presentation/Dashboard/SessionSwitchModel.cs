using Simpiens.Core.Session;
using Simpiens.Diagnostics;
using ZLogger;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// The session switch (Unity's, top right under the header): shown only when a test session (the Dev Map) is
    /// registered, which only development builds do. Switching is a full respawn with the same seed. On a test session
    /// a banner says it is not the game. The button queues the switch as a host command (review D3): the host respawns
    /// before its next ticks, never inside the click.
    /// </summary>
    public sealed class SessionSwitchModel
    {
        private readonly ISessionSwitcher _switcher;
        private readonly IHostCommands _commands;

        public SessionSwitchModel(ISessionSwitcher switcher, IHostCommands commands = null)
        {
            _switcher = switcher;
            _commands = commands;
        }

        public bool Visible => _switcher != null && _commands != null && _switcher.Sessions.Count > 1;

        /// <summary>The session the button leads to: the next registered one after the current.</summary>
        public ISessionDefinition Next
        {
            get
            {
                if (!Visible) return null;
                var sessions = _switcher.Sessions;
                int current = 0;
                for (int i = 0; i < sessions.Count; i++)
                {
                    if (sessions[i] == _switcher.Current) current = i;
                }
                return sessions[(current + 1) % sessions.Count];
            }
        }

        public string ButtonText => Visible ? $"SWITCH TO {Next.DisplayName.ToUpperInvariant()}" : string.Empty;
        public bool BannerVisible => Visible && _switcher.Current != _switcher.Default;
        public string BannerText => Visible ? $"{_switcher.Current.DisplayName.ToUpperInvariant()}: TEST SESSION (NOT THE GAME)" : string.Empty;

        public void Switch()
        {
            if (!Visible) return;
            var next = Next;
            SimLog.Bootstrap.ZLogInformation($"Session switch asked: {_switcher.Current.DisplayName} -> {next.DisplayName}");
            _commands.Enqueue(HostCommand.SwitchSession(next.Id));
        }
    }
}
