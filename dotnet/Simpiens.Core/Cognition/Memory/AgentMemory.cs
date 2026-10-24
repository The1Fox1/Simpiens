using System.Collections.Generic;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.Spatial;
using Simpiens.Cognition.Social;

namespace Simpiens.Cognition.Memory
{
    public class AgentMemory : IAgentMemory
    {
        public const uint DefaultDecayTicks = 1000;
        public const uint ResourceDecayTicks = 1500;
        public const uint PawnDecayTicks = 400;
        public const uint AnchorDecayTicks = 5000;

        public MemoryLedger EventLedger { get; }
        public Dictionary<EntityId, SpatialMemoryRecord> SpatialMemoryMap { get; }

        [System.ThreadStatic]
        private static List<EntitySnapshot> _queryResults;

        [System.ThreadStatic]
        private static List<EntityId> _keysToRemove;

        [System.ThreadStatic]
        private static List<MemoryEvent> _gossipEventBuffer;

        private readonly Dictionary<EntityId, uint> _blacklistedEntities;
        private readonly Dictionary<EntityId, uint> _lastGossipWithAgent;
        private readonly Dictionary<EntityId, AffinityRecord> _affinityMap;

        private readonly AgentMemorySnapshot[] _snapshots;
        private int _snapshotIndex;

        public Dictionary<EntityId, AffinityRecord> AffinityMap => _affinityMap;
        public IReadOnlyDictionary<EntityId, uint> BlacklistedEntities => _blacklistedEntities;
        public Dictionary<EntityId, uint> BlacklistMap => _blacklistedEntities;

        public AgentMemory(int ledgerCapacity = 50, int initialMapCapacity = 64)
        {
            EventLedger = new MemoryLedger(ledgerCapacity);
            SpatialMemoryMap = new Dictionary<EntityId, SpatialMemoryRecord>(initialMapCapacity);
            _blacklistedEntities = new Dictionary<EntityId, uint>();
            _lastGossipWithAgent = new Dictionary<EntityId, uint>();
            _affinityMap = new Dictionary<EntityId, AffinityRecord>(16);

            _snapshots = new AgentMemorySnapshot[2]
            {
                new AgentMemorySnapshot(initialMapCapacity, 16),
                new AgentMemorySnapshot(initialMapCapacity, 16)
            };
            _snapshotIndex = 0;
        }

        public static uint GetDecayThreshold(EntityType type)
        {
            switch (type)
            {
                case EntityType.Pawn: return PawnDecayTicks;
                case EntityType.Resource: return ResourceDecayTicks;
                case EntityType.Anchor: return AnchorDecayTicks;
                case EntityType.Bunk: return AnchorDecayTicks; // fixtures: remembered as long as a hearth
                default: return DefaultDecayTicks;
            }
        }

        public void BlacklistEntity(EntityId entityId, uint untilTick)
        {
            _blacklistedEntities[entityId] = untilTick;
        }

        public bool IsBlacklisted(EntityId entityId, uint currentTick)
        {
            if (_blacklistedEntities.TryGetValue(entityId, out var untilTick))
            {
                return currentTick < untilTick;
            }
            return false;
        }

        public void RemoveMemory(EntityId entityId)
        {
            SpatialMemoryMap.Remove(entityId);
            _blacklistedEntities.Remove(entityId);
        }

        private readonly struct SurveyNote
        {
            public readonly SpatialMemoryRecord Record;
            public readonly byte SectorId;

            public SurveyNote(in SpatialMemoryRecord record, byte sectorId)
            {
                Record = record;
                SectorId = sectorId;
            }
        }

        // What a scout has seen inside the uncharted sector it was hired to survey. Private: never gossiped, never
        // planned on, until a successful report publishes it to everyone (SimulationManager.PublishSurvey).
        private readonly Dictionary<EntityId, SurveyNote> _surveyNotes = new Dictionary<EntityId, SurveyNote>(8);
        private List<EntityId> _surveyKeys;

        public int SurveyNoteCount => _surveyNotes.Count;

        // Last tick this agent stood in each sector (ids below 64), so searches try the room it saw least recently
        private readonly uint[] _sectorLastVisit = new uint[64];

        public uint LastSectorVisit(byte sectorId) => sectorId < _sectorLastVisit.Length ? _sectorLastVisit[sectorId] : 0u;

        public void NoteSectorVisit(byte sectorId, uint tick)
        {
            // Stored +1 so tick 0 still counts as a visit
            if (sectorId < _sectorLastVisit.Length) _sectorLastVisit[sectorId] = tick + 1;
        }

        /// <summary>Static landmarks are hidden by the fog of war; pawns stay visible.</summary>
        public static bool IsLandmark(EntityType type)
        {
            return type == EntityType.Resource || type == EntityType.Anchor || type == EntityType.Stall || type == EntityType.Bunk;
        }

        /// <summary>Moves this agent's survey notes for one sector into <paramref name="into"/>.</summary>
        public void TakeSurveyNotes(byte sectorId, List<SpatialMemoryRecord> into)
        {
            if (_surveyNotes.Count == 0) return;
            _surveyKeys ??= new List<EntityId>(8);
            _surveyKeys.Clear();
            foreach (var kvp in _surveyNotes)
            {
                if (kvp.Value.SectorId == sectorId) _surveyKeys.Add(kvp.Key);
            }
            for (int i = 0; i < _surveyKeys.Count; i++)
            {
                into.Add(_surveyNotes[_surveyKeys[i]].Record);
                _surveyNotes.Remove(_surveyKeys[i]);
            }
        }

        /// <summary>Learns a published survey; a fresher record already held is kept.</summary>
        public void ReceiveSurvey(List<SpatialMemoryRecord> survey)
        {
            for (int i = 0; i < survey.Count; i++)
            {
                var record = survey[i];
                if (SpatialMemoryMap.TryGetValue(record.EntityId, out var mine) && mine.LastSeenTick >= record.LastSeenTick) continue;
                SpatialMemoryMap[record.EntityId] = record;
            }
        }

        /// <summary>
        /// Perceives what is in range and in line of sight. Landmarks inside an uncharted sector are not perceived
        /// (fog of war), except by the scout surveying that sector (<paramref name="surveySectorId"/>), whose
        /// sightings become private survey notes.
        /// </summary>
        public void UpdateMemory(SharedWorldSnapshot snapshot, GridPoint currentPos, int visionRadius, uint currentTick, byte surveySectorId = 0)
        {
            if (_queryResults == null)
            {
                _queryResults = new List<EntitySnapshot>(64);
            }
            _queryResults.Clear();

            Vec2 queryPos = new Vec2(currentPos.X, currentPos.Y);
            if (snapshot?.Map != null) NoteSectorVisit(snapshot.Map.FindSector(queryPos).Id, currentTick);
            
            // Query the global snapshot strictly within the agent's visionRadius
            // This populates our thread-local _queryResults without allocating memory
            snapshot.GetEntitiesInRange(queryPos, visionRadius, _queryResults);

            int count = _queryResults.Count;
            for (int i = 0; i < count; i++)
            {
                var entity = _queryResults[i];

                // Line-of-sight check: agents cannot perceive through walls
                if (snapshot != null && !snapshot.HasLineOfSight(queryPos, entity.Position))
                {
                    continue;
                }

                GridPoint entityPosInt = new GridPoint(SimMath.RoundToInt(entity.Position.X), SimMath.RoundToInt(entity.Position.Y));

                // Fog of war: nobody knows what is inside an uncharted sector until a scout reports back
                if (snapshot != null && IsLandmark(entity.Type) && !snapshot.IsCharted(entity.Position))
                {
                    if (surveySectorId != 0 && snapshot.Map.FindSector(entity.Position).Id == surveySectorId)
                    {
                        _surveyNotes[entity.Id] = new SurveyNote(
                            new SpatialMemoryRecord(entity.Id, entity.Type, entityPosInt, currentTick, entity.ResourceKind), surveySectorId);
                    }
                    continue;
                }

                if (SpatialMemoryMap.TryGetValue(entity.Id, out var record))
                {
                    // Update existing record
                    record.LastKnownLocation = entityPosInt;
                    record.LastSeenTick = currentTick;
                    record.ResourceKind = entity.ResourceKind;
                    SpatialMemoryMap[entity.Id] = record;
                }
                else
                {
                    // Create new record
                    SpatialMemoryMap[entity.Id] = new SpatialMemoryRecord(entity.Id, entity.Type, entityPosInt, currentTick, entity.ResourceKind);
                    
                    // Add event to the ledger for spotting new entities
                    MemoryEventType eventType = entity.Type == EntityType.Resource ? MemoryEventType.ResourceSpotted : MemoryEventType.AgentSpotted;
                    EventLedger.AddEvent(new MemoryEvent(eventType, entity.Id, entityPosInt, currentTick));
                }
            }

            // Prune memory: if we remember something at a location we can currently see, but we didn't just see it, it must be gone.
            if (_keysToRemove == null)
            {
                _keysToRemove = new List<EntityId>(16);
            }
            _keysToRemove.Clear();

            foreach (var kvp in SpatialMemoryMap)
            {
                var record = kvp.Value;
                
                // If we just saw it this tick, it's still there
                if (record.LastSeenTick == currentTick) continue;

                // 1. Line-of-sight pruning: Can we see its last known location?
                float dist = Vec2.Distance(queryPos, new Vec2(record.LastKnownLocation.X, record.LastKnownLocation.Y));
                if (dist <= visionRadius)
                {
                    // If obstructed by a wall, the agent cannot verify whether the entity is gone
                    if (snapshot != null && !snapshot.HasLineOfSight(queryPos, new Vec2(record.LastKnownLocation.X, record.LastKnownLocation.Y)))
                    {
                        continue;
                    }

                    // The location is in vision, but the entity is not. It has been destroyed or moved away.
                    _keysToRemove.Add(kvp.Key);
                    continue;
                }

                // 2. Temporal decay: If unobserved for longer than its retention threshold, it fades.
                uint decayThreshold = GetDecayThreshold(record.Type);
                if (currentTick >= record.LastSeenTick && (currentTick - record.LastSeenTick) > decayThreshold)
                {
                    _keysToRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _keysToRemove.Count; i++)
            {
                SpatialMemoryMap.Remove(_keysToRemove[i]);
            }

            // Clean up expired blacklists
            _keysToRemove.Clear();
            foreach (var kvp in _blacklistedEntities)
            {
                if (currentTick >= kvp.Value)
                {
                    _keysToRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _keysToRemove.Count; i++)
            {
                _blacklistedEntities.Remove(_keysToRemove[i]);
            }
        }

        /// <summary>
        /// Writes this memory into a save (Milestone 1 save/load). Keyed records are sorted by id and the event ledger is
        /// oldest first, so the result doesn't depend on the order the dictionaries were filled in.
        /// </summary>
        public void CaptureTo(MemorySave save)
        {
            if (save == null) throw new System.ArgumentNullException(nameof(save));

            var sightings = new SpatialMemoryRecord[SpatialMemoryMap.Count];
            int n = 0;
            foreach (var kvp in SpatialMemoryMap) sightings[n++] = kvp.Value;
            System.Array.Sort(sightings, (a, b) => a.EntityId.CompareTo(b.EntityId));
            save.Sightings = sightings;

            var notes = new SurveyNoteRecord[_surveyNotes.Count];
            n = 0;
            foreach (var kvp in _surveyNotes) notes[n++] = new SurveyNoteRecord { Record = kvp.Value.Record, SectorId = kvp.Value.SectorId };
            System.Array.Sort(notes, (a, b) => a.Record.EntityId.CompareTo(b.Record.EntityId));
            save.SurveyNotes = notes;

            save.Blacklist = SortedTicks(_blacklistedEntities);
            save.GossipCooldowns = SortedTicks(_lastGossipWithAgent);

            var affinities = new PeerAffinityRecord[_affinityMap.Count];
            n = 0;
            foreach (var kvp in _affinityMap) affinities[n++] = new PeerAffinityRecord { PeerId = kvp.Key, Affinity = kvp.Value };
            System.Array.Sort(affinities, (a, b) => a.PeerId.CompareTo(b.PeerId));
            save.Affinities = affinities;

            save.Events = EventLedger.ToArray();
            save.SectorVisits = (uint[])_sectorLastVisit.Clone();
        }

        /// <summary>Replaces everything this memory holds with a save's (a load).</summary>
        public void RestoreFrom(MemorySave save)
        {
            if (save == null) throw new System.ArgumentNullException(nameof(save));

            SpatialMemoryMap.Clear();
            _surveyNotes.Clear();
            _blacklistedEntities.Clear();
            _lastGossipWithAgent.Clear();
            _affinityMap.Clear();

            if (save.Sightings != null)
            {
                for (int i = 0; i < save.Sightings.Length; i++) SpatialMemoryMap[save.Sightings[i].EntityId] = save.Sightings[i];
            }
            if (save.SurveyNotes != null)
            {
                for (int i = 0; i < save.SurveyNotes.Length; i++)
                {
                    var note = save.SurveyNotes[i];
                    _surveyNotes[note.Record.EntityId] = new SurveyNote(note.Record, note.SectorId);
                }
            }
            RestoreTicks(_blacklistedEntities, save.Blacklist);
            RestoreTicks(_lastGossipWithAgent, save.GossipCooldowns);
            if (save.Affinities != null)
            {
                for (int i = 0; i < save.Affinities.Length; i++) _affinityMap[save.Affinities[i].PeerId] = save.Affinities[i].Affinity;
            }

            EventLedger.Restore(save.Events);
            System.Array.Clear(_sectorLastVisit, 0, _sectorLastVisit.Length);
            if (save.SectorVisits != null)
            {
                System.Array.Copy(save.SectorVisits, _sectorLastVisit, System.Math.Min(save.SectorVisits.Length, _sectorLastVisit.Length));
            }
        }

        private static void RestoreTicks(Dictionary<EntityId, uint> into, IdTickRecord[] records)
        {
            if (records == null) return;
            for (int i = 0; i < records.Length; i++) into[records[i].Id] = records[i].Tick;
        }

        private static IdTickRecord[] SortedTicks(Dictionary<EntityId, uint> ticks)
        {
            var records = new IdTickRecord[ticks.Count];
            int n = 0;
            foreach (var kvp in ticks) records[n++] = new IdTickRecord { Id = kvp.Key, Tick = kvp.Value };
            System.Array.Sort(records, (a, b) => a.Id.CompareTo(b.Id));
            return records;
        }

        public AffinityRecord GetAffinity(EntityId peerId, uint currentTick)
        {
            if (_affinityMap.TryGetValue(peerId, out var record))
            {
                return record.RegressTowardNeutral(currentTick);
            }
            return AffinityRecord.Neutral;
        }

        public IAgentMemory GetSnapshot(uint currentTick)
        {
            _snapshotIndex = 1 - _snapshotIndex;
            var snap = _snapshots[_snapshotIndex];
            snap.PopulateFrom(this, currentTick);
            return snap;
        }

        public bool TryGetSpatialRecord(EntityId entityId, out SpatialMemoryRecord record)
        {
            return SpatialMemoryMap.TryGetValue(entityId, out record);
        }

        public void PruneStaleResourceMemories(uint currentTick)
        {
            if (_keysToRemove == null)
            {
                _keysToRemove = new List<EntityId>(16);
            }
            _keysToRemove.Clear();

            foreach (var kvp in SpatialMemoryMap)
            {
                if (kvp.Value.Type == EntityType.Resource)
                {
                    _keysToRemove.Add(kvp.Key);
                }
            }

            for (int i = 0; i < _keysToRemove.Count; i++)
            {
                SpatialMemoryMap.Remove(_keysToRemove[i]);
            }
        }

        public void RecordInteraction(EntityId peerId, sbyte deltaWarmth, sbyte deltaTrust, sbyte deltaFear, sbyte deltaRespect, uint currentTick)
        {
            var current = GetAffinity(peerId, currentTick);
            _affinityMap[peerId] = current.WithInteraction(deltaWarmth, deltaTrust, deltaFear, deltaRespect, currentTick);
        }

        public void SetAffinity(EntityId peerId, AffinityRecord record)
        {
            _affinityMap[peerId] = record;
        }

        public void RecordEvent(MemoryEventType type, EntityId subjectId, GridPoint location, uint simulationTick)
        {
            EventLedger.AddEvent(new MemoryEvent(type, subjectId, location, simulationTick));
        }

        public bool IsTrusted(EntityId peerId, uint currentTick, sbyte threshold = 20) => GetAffinity(peerId, currentTick).Trust >= threshold;
        public bool IsDistrusted(EntityId peerId, uint currentTick, sbyte threshold = -20) => GetAffinity(peerId, currentTick).Trust <= threshold;
        public bool IsFeared(EntityId peerId, uint currentTick, sbyte threshold = 40) => GetAffinity(peerId, currentTick).Fear >= threshold;
        public bool IsWarm(EntityId peerId, uint currentTick, sbyte threshold = 20) => GetAffinity(peerId, currentTick).Warmth >= threshold;
        public bool IsRespected(EntityId peerId, uint currentTick, sbyte threshold = 30) => GetAffinity(peerId, currentTick).Respect >= threshold;

        /// <summary>
        /// Calculates social submission score toward a peer: Fear + (Respect * 0.8).
        /// High submission indicates willingness to yield contested resources to this peer.
        /// </summary>
        public float CalculateSubmissionScore(EntityId peerId, uint currentTick)
        {
            var affinity = GetAffinity(peerId, currentTick);
            return affinity.Fear + (affinity.Respect * 0.8f);
        }

        /// <summary>
        /// Calculates altruism utility score toward a distressed peer:
        /// (Warmth * 0.5) + (Trust * 0.3) - (SelfHunger * 0.4) + (IsTargetCollapsed ? 30 : 0).
        /// Scores >= 25 indicate willingness to share food with this peer.
        /// </summary>
        public float CalculateAltruismScore(EntityId peerId, uint currentTick, float selfHunger, bool isTargetCollapsed)
        {
            var affinity = GetAffinity(peerId, currentTick);
            float collapseBonus = isTargetCollapsed ? 30f : 0f;
            return (affinity.Warmth * 0.5f) + (affinity.Trust * 0.3f) - (selfHunger * 0.4f) + collapseBonus;
        }

        public bool CanGossipWith(EntityId peerId, uint currentTick, uint cooldownTicks = 300)
        {
            if (_lastGossipWithAgent.TryGetValue(peerId, out var lastTick))
            {
                return currentTick >= lastTick + cooldownTicks;
            }
            return true;
        }

        public bool TryGossip(AgentMemory peerMemory, EntityId peerId, EntityId myId, uint currentTick, uint cooldownTicks = 300)
        {
            if (peerMemory == null) return false;

            if (!CanGossipWith(peerId, currentTick, cooldownTicks)) return false;

            // Relational pre-filter: Distrusted or terrified agents refuse to converse
            var myAffinity = GetAffinity(peerId, currentTick);
            var peerAffinity = peerMemory.GetAffinity(myId, currentTick);
            if (myAffinity.Trust < -20 || myAffinity.Fear > 60 || peerAffinity.Trust < -20 || peerAffinity.Fear > 60)
            {
                return false;
            }

            // Mutual spatial knowledge exchange
            // Share this agent's spatial memory to peer
            foreach (var kvp in SpatialMemoryMap)
            {
                var myRecord = kvp.Value;
                if (peerMemory.IsBlacklisted(myRecord.EntityId, currentTick)) continue;

                if (peerMemory.SpatialMemoryMap.TryGetValue(myRecord.EntityId, out var peerRecord))
                {
                    if (myRecord.LastSeenTick > peerRecord.LastSeenTick)
                    {
                        peerMemory.SpatialMemoryMap[myRecord.EntityId] = myRecord;
                    }
                }
                else
                {
                    peerMemory.SpatialMemoryMap[myRecord.EntityId] = myRecord;
                }
            }

            // Share peer's spatial memory to this agent
            foreach (var kvp in peerMemory.SpatialMemoryMap)
            {
                var peerRecord = kvp.Value;
                if (IsBlacklisted(peerRecord.EntityId, currentTick)) continue;

                if (SpatialMemoryMap.TryGetValue(peerRecord.EntityId, out var myRecord))
                {
                    if (peerRecord.LastSeenTick > myRecord.LastSeenTick)
                    {
                        SpatialMemoryMap[peerRecord.EntityId] = peerRecord;
                    }
                }
                else
                {
                    SpatialMemoryMap[peerRecord.EntityId] = peerRecord;
                }
            }

            // Record gossip events in ledgers
            EventLedger.AddEvent(new MemoryEvent(MemoryEventType.GossipShared, peerId, GridPoint.Zero, currentTick));
            peerMemory.EventLedger.AddEvent(new MemoryEvent(MemoryEventType.GossipReceived, myId, GridPoint.Zero, currentTick));

            // Record relational bonding across the 4 axes:
            // Mutual knowledge sharing builds warmth (+5), trust (+10), respect (+5), and relieves fear (-5)
            RecordInteraction(peerId, deltaWarmth: 5, deltaTrust: 10, deltaFear: -5, deltaRespect: 5, currentTick);
            peerMemory.RecordInteraction(myId, deltaWarmth: 5, deltaTrust: 10, deltaFear: -5, deltaRespect: 5, currentTick);

            // Set cooldowns
            _lastGossipWithAgent[peerId] = currentTick;
            peerMemory._lastGossipWithAgent[myId] = currentTick;

            return true;
        }
    }
}
