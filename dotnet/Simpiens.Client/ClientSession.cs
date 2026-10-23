using System;
using Simpiens.Core.Session;
using Simpiens.Diagnostics;
using Simpiens.Hosting;
using Simpiens.Narrative;
using Simpiens.Presentation;
using Simpiens.Presentation.Cutscenes;
using Simpiens.Presentation.Dashboard;
using Simpiens.Simulation;
using ZLogger;

namespace Simpiens.Client
{
    /// <summary>
    /// What a client plays (Epic CLIENT, review A4): the simulation and its driver, the view's frames, the player's
    /// selection, the dashboard as data and the quick-save slot. It holds no XNA type, so tests build it alone without a
    /// GPU, and every screen shares it. The views read it; only typed commands change the simulation (ROADMAP §1A.1).
    /// </summary>
    public sealed class ClientSession : IDisposable
    {
        private readonly SimulationDriver _driver;
        private double _realSeconds;

        public ClientSession(ClientOptions options = null)
        {
            Options = options ?? new ClientOptions();
            // The core composed as every .NET host composes it, the session spawned at tick 0 before the first frame
            Simulation = LocalSimulation.Create(Options.Seed, Options.ConfigureServices);
            Simulation.Respawn(Options.SessionId);
            _driver = new SimulationDriver(Simulation.Clock, Options.TicksPerUpdate);
            // What the views draw, copied when each tick completes (and afresh on every respawn, switch or load)
            Frames.Follow(Simulation.Host, Simulation.Registry, Simulation.Sectors, Simulation.Clock, Simulation.Chronicle);

            Identity = Simulation.Identity;
            Selection = new WorldSelection(Frames, Simulation.Sectors, Identity);
            Scenes = new SceneDirector(Frames);
            if (Options.DemoScenes) DemoScenes.AddTo(Scenes);
            SaveGame = new SaveGameService(Simulation.Host, Options.SaveDirectory ?? ClientOptions.DefaultSaveDirectory());
            DashboardModel = new DashboardModel(DashboardContext.From(Simulation.Services, SaveGame, HostCommands), Selection);
            Simulation.Host.SessionChanged += OnSessionChanged;
        }

        public ClientOptions Options { get; }

        /// <summary>The simulation this client plays: the views read it, and only typed commands change it.</summary>
        public LocalSimulation Simulation { get; }

        /// <summary>The world as the views draw it: the last two completed ticks, as plain data (review A3).</summary>
        public WorldViewFrames Frames { get; } = new WorldViewFrames();

        /// <summary>Crawlers' names.</summary>
        public IAgentIdentityService Identity { get; }

        /// <summary>What starts dynamic scenes over the world, and the one playing (M3 design D9).</summary>
        public SceneDirector Scenes { get; }

        /// <summary>The player's selection: a crawler (followed or not) and a sector.</summary>
        public WorldSelection Selection { get; }

        /// <summary>The quick-save slot (<see cref="ClientOptions.SaveDirectory"/>).</summary>
        public ISaveGameService SaveGame { get; }

        /// <summary>The dashboard as data: what its views show, and where the player's clicks go.</summary>
        public DashboardModel DashboardModel { get; }

        /// <summary>
        /// The host's commands (review D3): speed, pause, quick save and load, the session switch. The dashboard and the
        /// keys queue them; <see cref="Update"/> applies them first, before the ticks, so a load or a switch never runs
        /// inside a click.
        /// </summary>
        public HostCommandQueue HostCommands { get; } = new HostCommandQueue();

        /// <summary>The speed the world runs at when it isn't paused (a multiple of real time).</summary>
        public float Speed { get; private set; } = 1f;

        /// <summary>The player paused the world (<see cref="HostCommand.Pause"/>).</summary>
        public bool Paused { get; private set; }

        /// <summary>The view's clock in seconds: what animations and fades run on (<see cref="ClientOptions.ViewClockFromTicks"/>).</summary>
        public float ViewSeconds => Options.ViewClockFromTicks ? Simulation.Clock.CurrentTick * SimulationClock.FixedTickDeltaTime : (float)_realSeconds;

        /// <summary>
        /// How far the clock is between the last two ticks, for drawing crawlers between them: the clock's own fraction
        /// live; 1 (the last tick exactly) when the view runs on ticks or every update steps whole ticks.
        /// </summary>
        public float ViewAlpha => Options.ViewClockFromTicks || Options.TicksPerUpdate > 0 ? 1f : Simulation.Clock.InterpolationAlpha;

        /// <summary>
        /// One frame's time: the view's clock moves on, and the world with it unless a screen holds it
        /// (<paramref name="advanceWorld"/> false: a hard cutscene). The world only ever steps whole ticks.
        /// </summary>
        public void Update(TimeSpan elapsed, bool advanceWorld = true)
        {
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero; // KNI can report a slightly negative frame time (epic F5)
            ApplyHostCommands();
            _realSeconds += elapsed.TotalSeconds;
            if (advanceWorld) _driver.Advance(elapsed);
            Frames.Alpha = ViewAlpha;
        }

        /// <summary>Applies the queued host commands in the order they came, between ticks.</summary>
        public void ApplyHostCommands()
        {
            if (HostCommands.Count == 0) return;
            var commands = HostCommands.Drain();
            for (int i = 0; i < commands.Count; i++) Apply(commands[i]);
        }

        private void Apply(in HostCommand command)
        {
            var clock = Simulation.Clock;
            switch (command.Type)
            {
                case HostCommandType.SetSpeed:
                    Speed = command.Speed;
                    if (!Paused) clock.SetTimeMultiplier(Speed);
                    break;
                case HostCommandType.Pause:
                    Paused = true;
                    _driver.Held = true;
                    clock.SetTimeMultiplier(0f);
                    break;
                case HostCommandType.Resume:
                    Paused = false;
                    _driver.Held = false;
                    clock.SetTimeMultiplier(Speed);
                    break;
                case HostCommandType.QuickSave:
                    if (DashboardModel.Saves.IsSaving) break;
                    DashboardModel.Saves.Track(SaveGame.QuickSaveAsync()); // captured here, between ticks; written on the thread pool
                    break;
                case HostCommandType.QuickLoad:
                    DashboardModel.Saves.Report(SaveGame.QuickLoad().Message);
                    break;
                case HostCommandType.SwitchSession:
                    SwitchTo(command.SessionId);
                    break;
            }
        }

        private void SwitchTo(string sessionId)
        {
            var switcher = DashboardModel.Context.Sessions;
            var sessions = switcher?.Sessions;
            for (int i = 0; sessions != null && i < sessions.Count; i++)
            {
                if (sessions[i].Id != sessionId) continue;
                SimLog.Bootstrap.ZLogInformation($"Session switch: {switcher.Current.DisplayName} -> {sessions[i].DisplayName}");
                switcher.SwitchTo(sessions[i]);
                return;
            }
            SimLog.Bootstrap.ZLogWarning($"Session switch refused: no registered session '{sessionId}'.");
        }

        /// <summary>
        /// A respawn, a session switch or a load replaced every entity (ids are reissued). The views see it as a new epoch
        /// of their frames (review A5); the dashboard still reads the live simulation, so it is told here.
        /// </summary>
        private void OnSessionChanged(ISessionDefinition session) => DashboardModel.SessionChanged();

        public void Dispose()
        {
            Simulation.Host.SessionChanged -= OnSessionChanged;
            Frames.Dispose();
            Simulation.Dispose();
        }
    }
}
