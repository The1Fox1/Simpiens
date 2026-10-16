using System.Runtime.InteropServices;
using MemoryPack;
using Simpiens.Cognition.Contracts;
using Simpiens.Cognition.Groups;
using Simpiens.Cognition.Memory;
using Simpiens.Cognition.Social;
using UnityEngine;

namespace Simpiens.Simulation.Persistence
{
    /// <summary>One crawler in a save: its names, its state, its pack and its memory.</summary>
    [MemoryPackable]
    public sealed partial class CrawlerSave
    {
        /// <summary>The view's name, shown by the dashboard in brackets ("SwarmAgent_3").</summary>
        public string Name { get; set; }

        /// <summary>The log's label ("Crawler3").</summary>
        public string Label { get; set; }

        public CrawlerRecord Record { get; set; }
        public InventorySave Inventory { get; set; }
        public MemorySave Memory { get; set; }
    }

    /// <summary>
    /// A crawler's plain state, written and read by <see cref="Cognition.AgentState"/>. Its current intent, plan, goal,
    /// last decision and look are work in progress and are not saved: the crawler decides afresh after a load.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CrawlerRecord
    {
        public GUID Id;
        public Vector2 Position;
        public AgentNeeds Needs;
        public float Frustration;
        public AgentActivity Activity;
        public GroupMembership Membership;
        public IdentityTag Identity;
        public AgentPersonality Personality;
        public int NavigationalStalls;
        public float TantrumTimer;
        public float YieldingTimer;
        public float GossipTimer;
        public float SharingTimer;
        public float RallyTimer;
        public float DefiantTimer;
        public float LastGossipTimestamp;
        public float LastYieldTimestamp;
        public float LastTantrumTimestamp;
        public uint LastEvaluatedDirectiveTick;
        public uint ScoutReportTick;
        public ushort ScoutReportBountyId;
        public bool IsExhaustionCollapsed;
        public bool CanBuyFoodAtStall;
    }

    /// <summary>
    /// A crawler's memory in a save, written and read by <see cref="AgentMemory"/>. Keyed records are sorted by id and
    /// the event ledger is oldest first, so the bytes don't depend on dictionary order.
    /// </summary>
    [MemoryPackable]
    public sealed partial class MemorySave
    {
        public SpatialMemoryRecord[] Sightings { get; set; }

        /// <summary>A scout's private notes on the uncharted sector it was hired to survey.</summary>
        public SurveyNoteRecord[] SurveyNotes { get; set; }

        /// <summary>Entities avoided until a tick.</summary>
        public IdTickRecord[] Blacklist { get; set; }

        /// <summary>The last tick this crawler gossiped with each peer.</summary>
        public IdTickRecord[] GossipCooldowns { get; set; }

        public PeerAffinityRecord[] Affinities { get; set; }

        /// <summary>The event ledger, oldest first.</summary>
        public MemoryEvent[] Events { get; set; }

        /// <summary>The last tick (plus one) this crawler stood in each sector, for sector ids below 64.</summary>
        public uint[] SectorVisits { get; set; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SurveyNoteRecord
    {
        public SpatialMemoryRecord Record;
        public byte SectorId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IdTickRecord
    {
        public GUID Id;
        public uint Tick;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PeerAffinityRecord
    {
        public GUID PeerId;
        public AffinityRecord Affinity;
    }
}
