using System;
using Simpiens.Intervention;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.World;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>What the dashboard reads, and the one queue it writes to. Any of them may be null (tests); the models then show nothing and send nothing.</summary>
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
            IPlayerInterventionService intervention = null)
        {
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

        /// <summary>The context of a composed simulation (<c>HeadlessSimulation.Services</c>).</summary>
        public static DashboardContext From(IServiceProvider services)
        {
            T Get<T>() where T : class => services?.GetService(typeof(T)) as T;
            return new DashboardContext(Get<ISimulationManager>(), Get<ISimulationClock>(), Get<IWorldRegistry>(), Get<ISectorRegistry>(),
                Get<IAgentIdentityService>(), Get<INarrativeChronicleService>(), Get<INarrativeFormatter>(), Get<IPlayerInterventionService>());
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

        public FacilityZoneService Zones { get; }

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
