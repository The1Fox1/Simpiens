using System;
using Simpiens.Core.Session;
using Simpiens.Intervention;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.World;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>
    /// What the dashboard reads, and the two queues it writes to: the player's influence on the simulation, and the
    /// host's commands (speed, pause, save, load, switch). Any of them may be null (tests); the models then show nothing
    /// and send nothing.
    /// </summary>
    public sealed class DashboardContext
    {
        public DashboardContext(
            ISimulationManager simulation = null,
            ISimulationClock clock = null,
            IWorldRegistry registry = null,
            ISectorRegistry sectors = null,
            IAgentIdentityService identity = null,
            INarrativeChronicleService chronicle = null,
            INarrativeFormatter formatter = null,
            IPlayerInterventionService intervention = null,
            ISaveGameService saveGame = null,
            ISessionSwitcher sessions = null,
            IHostCommands hostCommands = null)
        {
            HostCommands = hostCommands;
            SaveGame = saveGame;
            Sessions = sessions;
            Simulation = simulation;
            Clock = clock;
            Registry = registry;
            Sectors = sectors;
            Identity = identity;
            Chronicle = chronicle ?? simulation?.Chronicle;
            Formatter = formatter;
            Intervention = intervention;
            Zones = new FacilityZoneService(sectors);
        }

        /// <summary>
        /// The context of a composed simulation (<c>HeadlessSimulation.Services</c>). It requires every service the game
        /// always has (review U4), so a wiring mistake fails here, at start, instead of showing an empty panel; only the
        /// save slot and the host's commands are optional. The constructor, where anything may be null, is for unit tests.
        /// </summary>
        public static DashboardContext From(IServiceProvider services, ISaveGameService saveGame = null, IHostCommands hostCommands = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            T Need<T>() where T : class => services.GetService(typeof(T)) as T
                ?? throw new InvalidOperationException($"The dashboard needs {typeof(T).Name}, and the simulation's services have none.");
            return new DashboardContext(Need<ISimulationManager>(), Need<ISimulationClock>(), Need<IWorldRegistry>(), Need<ISectorRegistry>(),
                Need<IAgentIdentityService>(), Need<INarrativeChronicleService>(), Need<INarrativeFormatter>(), Need<IPlayerInterventionService>(),
                saveGame ?? services.GetService(typeof(ISaveGameService)) as ISaveGameService, Need<ISessionSwitcher>(), hostCommands);
        }

        public ISimulationManager Simulation { get; }
        public ISimulationClock Clock { get; }
        public IWorldRegistry Registry { get; }
        public ISectorRegistry Sectors { get; }
        public IAgentIdentityService Identity { get; }
        public INarrativeChronicleService Chronicle { get; }
        public INarrativeFormatter Formatter { get; }

        /// <summary>The player's commands go here, and only here (ROADMAP §1A.1).</summary>
        public IPlayerInterventionService Intervention { get; }

        /// <summary>The host's commands go here: speed, pause, save, load and switch (review D3).</summary>
        public IHostCommands HostCommands { get; }

        public FacilityZoneService Zones { get; }

        /// <summary>The quick-save slot, if the host gives one.</summary>
        public ISaveGameService SaveGame { get; }

        /// <summary>The registered sessions (the Dev Map only in development builds).</summary>
        public ISessionSwitcher Sessions { get; }

        /// <summary>The outpost's stall (the first), if there is one.</summary>
        public bool TryGetStall(out Simpiens.Simulation.Economy.TradeStallData stall)
        {
            var stalls = Simulation?.ActiveStalls;
            if (stalls != null && stalls.Count > 0)
            {
                stall = stalls[0];
                return true;
            }
            stall = default;
            return false;
        }

        public string FriendlyName(EntityId id, string fallback) => Identity != null ? Identity.GetFriendlyName(id) : fallback;
    }

    /// <summary>How a message to the player reads: Unity's green, red and amber.</summary>
    public enum FeedbackTone : byte
    {
        None = 0,
        Ok = 1,
        Error = 2,
        Notice = 3
    }

    /// <summary>A line of feedback under a form.</summary>
    public readonly struct Feedback
    {
        public readonly string Text;
        public readonly FeedbackTone Tone;

        public Feedback(string text, FeedbackTone tone)
        {
            Text = text;
            Tone = tone;
        }

        public static readonly Feedback None = new Feedback(string.Empty, FeedbackTone.None);
    }
}
