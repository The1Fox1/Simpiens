using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Contracts;
using Simpiens.Diagnostics;
using Simpiens.Narrative;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.People;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.World;
using ZLogger;

namespace Simpiens.Core.Session
{
    /// <summary>
    /// Drifters (Epic 10 G1, producer decision Q7): strangers who come in by the Road Gate with gold of their own, live
    /// as crawlers for a stay and leave. The host admits them on a seeded schedule (the session's DrifterTuning) and
    /// removes those who walked out (LeaveOutpostGoal, SimulationManager.DepartOutpost) at the end of the tick, before
    /// views capture the frame. A map without spawn points admits none.
    /// </summary>
    public sealed partial class SessionHost
    {
        /// <summary>A drifter's spawn point must be this clear of other crawlers; otherwise the arrival waits.</summary>
        public const float ArrivalClearance = 0.8f;

        /// <summary>Ticks an arrival waits when every spawn point is crowded.</summary>
        public const uint CrowdedGateRetryTicks = 50;

        private long _nextDrifterTick = -1;
        private int _driftersArrived;
        private readonly List<DepartedCrawler> _departed = new List<DepartedCrawler>(16);

        // Newcomers (Epic 10 Part 3, K5.6, Q4): a resident's death brings one, 2 game days on; when each is due, and how many came
        private readonly List<uint> _newcomersDue = new List<uint>(4);
        private int _newcomersArrived;

        /// <summary>How long after a resident's death a newcomer settles (Q4: 2 game days).</summary>
        public const int NewcomerAfterHours = 48;

        /// <summary>A newcomer's hunger as it arrives (the outpost's residents start at 20).</summary>
        public const float NewcomerArrivalHunger = 20f;

        /// <summary>Newcomers who have settled since the session started, and the ticks the next are due.</summary>
        public int NewcomersArrived => _newcomersArrived;
        public IReadOnlyList<uint> NewcomersDue => _newcomersDue;

        /// <summary>A crawler walked out by the Road Gate, or died (Epic 10 Part 3, K5.4a), and was removed: views drop what they drew for it.</summary>
        public event Action<AgentRuntime, WorldEntity> CrawlerDeparted;

        /// <summary>Drifters admitted since the session started (also numbers their names).</summary>
        public int DriftersArrived => _driftersArrived;

        /// <summary>Everyone who has left this session, in the order they left (Epic UI-Beta B1, plan D4).</summary>
        public IReadOnlyList<DepartedCrawler> Departed => _departed;

        /// <summary>The tick of the next drifter's arrival, or -1 when the session admits none.</summary>
        public long NextDrifterTick => _nextDrifterTick;

        private DrifterTuning Drifters => _simulationManager != null ? _simulationManager.Economy.Drifters : DrifterTuning.None;

        // On every respawn: the first arrival, if this session admits drifters
        private void ScheduleFirstDrifter(MapDefinition map)
        {
            _driftersArrived = 0;
            _newcomersArrived = 0;
            _newcomersDue.Clear();
            var tuning = Drifters;
            long first = tuning.FirstArrivalTick + Jitter(tuning, 0);
            _nextDrifterTick = tuning.IsEnabled && HasSpawnPoint(map) ? (first < 1 ? 1 : first) : -1;
        }

        private void CaptureDriftersTo(SessionSave save)
        {
            var departed = new DepartedSave[_departed.Count];
            for (int i = 0; i < departed.Length; i++)
            {
                departed[i] = new DepartedSave { Id = _departed[i].Id, GivenName = _departed[i].GivenName, Look = _departed[i].Look, Cause = _departed[i].Cause, CauseDetail = _departed[i].CauseDetail, Tick = _departed[i].Tick };
            }
            Array.Sort(departed, (a, b) => a.Id.CompareTo(b.Id));
            save.Population = new PopulationSave
            {
                NextDrifterTick = _nextDrifterTick, DriftersArrived = _driftersArrived, Departed = departed,
                NewcomersDue = _newcomersDue.ToArray(), NewcomersArrived = _newcomersArrived, // K5.6
            };
        }

        private void RestoreDriftersFrom(SessionSave save, MapDefinition map)
        {
            if (save.Population != null)
            {
                _nextDrifterTick = save.Population.NextDrifterTick;
                _driftersArrived = save.Population.DriftersArrived;
                _newcomersArrived = save.Population.NewcomersArrived;
                if (save.Population.NewcomersDue != null) _newcomersDue.AddRange(save.Population.NewcomersDue);
                var departed = save.Population.Departed ?? Array.Empty<DepartedSave>();
                for (int i = 0; i < departed.Length; i++)
                {
                    _departed.Add(new DepartedCrawler(departed[i].Id, departed[i].GivenName, departed[i].Look, departed[i].Cause, departed[i].CauseDetail, departed[i].Tick));
                    if (!string.IsNullOrEmpty(departed[i].GivenName)) _identity?.RegisterCustomName(departed[i].Id, departed[i].GivenName);
                }
            }
            else
            {
                ScheduleFirstDrifter(map);
            }
        }

        // End of every tick, before TickCompleted: out with those who left, then in with the next drifter if due
        private void TickPopulation(long tick)
        {
            for (int i = _crawlers.Count - 1; i >= 0; i--)
            {
                if (_crawlers[i].State.HasLeft) RemoveCrawlerAt(i);
            }
            TickNewcomers(tick);

            if (_nextDrifterTick < 0 || tick < _nextDrifterTick) return;
            var tuning = Drifters;
            int present = 0;
            for (int i = 0; i < _crawlers.Count; i++)
            {
                if (_crawlers[i].State.IsDrifter) present++;
            }
            if (present >= tuning.MaxPresent)
            {
                _nextDrifterTick = tick + tuning.ArrivalIntervalTicks + Jitter(tuning, _driftersArrived + 1);
                return;
            }
            if (!TryPickArrivalPoint(_current?.Map, _driftersArrived, out var gate))
            {
                _nextDrifterTick = tick + CrowdedGateRetryTicks;
                return;
            }
            AdmitDrifter(tick, gate, tuning);
            _nextDrifterTick = tick + tuning.ArrivalIntervalTicks + Jitter(tuning, _driftersArrived);
        }

        private void AdmitDrifter(long tick, Vec2 gate, in DrifterTuning tuning)
        {
            int number = ++_driftersArrived;
            var state = SpawnCrawler(gate, NextEntityId(), $"Drifter_{number}", $"Drifter{number}").State;
            ulong seed = _settings != null ? _settings.Seed : SimulationSettings.DefaultSeed;
            state.Personality = AgentPersonality.Generate(seed, state.AgentId);
            GiveIdentity(state);

            var rng = SimRandom.For(seed, state.AgentId, tick, SimRandomSalt.DrifterTraits);
            uint purse = (uint)SimMath.RoundToInt(Draw(ref rng, tuning.PurseMin, tuning.PurseMax));
            state.Hunger = Draw(ref rng, tuning.ArrivalHungerMin, tuning.ArrivalHungerMax);
            uint stay = (uint)SimMath.RoundToInt(Draw(ref rng, tuning.StayMinTicks, tuning.StayMaxTicks));
            state.DepartsAtTick = (uint)tick + (stay < 1u ? 1u : stay);
            if (state.Inventory != null) state.Inventory.Gold = (int)purse;
            _simulationManager?.EconomyLedger?.RecordFaucet(purse, "DrifterPurses");

            SimLog.Economy.ZLogInformation($"{SimLog.Agent(state.AgentId)} arrived by the Road Gate with {purse}g, staying until t{state.DepartsAtTick} ({state.Personality.ToString()})");
            int sector = _worldMap?.Sectors != null ? _worldMap.Sectors.GetSectorAt(gate).Id : 0;
            _simulationManager?.Chronicle?.RecordEvent(new ChronicleEvent(ChronicleEventType.DrifterArrived, state.AgentId, default, sector, (uint)tick));
        }

        // A newcomer due settles at a spawn point clear of crawlers; a crowded gate makes it wait for the next tick (K5.6)
        private void TickNewcomers(long tick)
        {
            for (int i = 0; i < _newcomersDue.Count; i++)
            {
                if (tick < _newcomersDue[i]) continue;
                if (!TryPickArrivalPoint(_current?.Map, _driftersArrived + _newcomersArrived, out var gate)) return;
                _newcomersDue.RemoveAt(i);
                AdmitNewcomer(tick, gate);
                return; // one a tick
            }
        }

        // A resident who settles (Q4): a new look and name, a seeded personality, the session's starting purse (a faucet)
        private void AdmitNewcomer(long tick, Vec2 gate)
        {
            int number = ++_newcomersArrived;
            var state = SpawnCrawler(gate, NextEntityId(), $"Newcomer_{number}", $"Newcomer{number}").State;
            ulong seed = _settings != null ? _settings.Seed : SimulationSettings.DefaultSeed;
            state.Personality = AgentPersonality.Generate(seed, state.AgentId);
            GiveIdentity(state);
            state.Hunger = NewcomerArrivalHunger;
            uint purse = _simulationManager != null ? (uint)_simulationManager.Economy.StartingGold : 0u;
            if (state.Inventory != null) state.Inventory.Gold = (int)purse;
            if (purse > 0) _simulationManager?.EconomyLedger?.RecordFaucet(purse, "NewcomerPurses");
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(state.AgentId)} settled at the outpost by the Road Gate with {purse}g ({state.Personality.ToString()})");
            int sector = _worldMap?.Sectors != null ? _worldMap.Sectors.GetSectorAt(gate).Id : 0;
            _simulationManager?.Chronicle?.RecordEvent(new ChronicleEvent(ChronicleEventType.NewcomerArrived, state.AgentId, default, sector, (uint)tick));
        }

        private void RemoveCrawlerAt(int index)
        {
            var entry = _crawlers[index];
            var runtime = entry.Runtime;
            var entity = _registry?.GetEntity(runtime.State.AgentId);
            uint tick = _clock != null ? (uint)_clock.CurrentTick : 0u;
            if (runtime.State.DiedOf != DeathCause.None && !runtime.State.IsDrifter) _newcomersDue.Add(tick + DayClock.Hours(NewcomerAfterHours)); // K5.6
            _departed.Add(new DepartedCrawler(runtime.State.AgentId, runtime.State.GivenName, runtime.State.Look, runtime.State.DiedOf, runtime.State.DiedDetail, tick));
            runtime.Dispose();
            if (entity != null) _registry.Unregister(entity);
            _crawlers.RemoveAt(index);
            CrawlerDeparted?.Invoke(runtime, entity);
        }

        // The spawn points in definition order, starting at the arrival's turn; the first one clear of crawlers
        private bool TryPickArrivalPoint(MapDefinition map, int turn, out Vec2 point)
        {
            point = default;
            if (map == null) return false;
            int count = 0;
            for (int i = 0; i < map.Pois.Count; i++)
            {
                if (map.Pois[i].Kind == PoiKind.SpawnPoint) count++;
            }
            for (int k = 0; k < count; k++)
            {
                int wanted = (turn + k) % count, seen = 0;
                for (int i = 0; i < map.Pois.Count; i++)
                {
                    if (map.Pois[i].Kind != PoiKind.SpawnPoint) continue;
                    if (seen++ != wanted) continue;
                    if (IsClearOfCrawlers(map.Pois[i].Position))
                    {
                        point = map.Pois[i].Position;
                        return true;
                    }
                    break;
                }
            }
            return false;
        }

        private bool IsClearOfCrawlers(Vec2 position)
        {
            for (int i = 0; i < _crawlers.Count; i++)
            {
                if (Vec2.Distance(_crawlers[i].State.Position, position) < ArrivalClearance) return false;
            }
            return true;
        }

        private static bool HasSpawnPoint(MapDefinition map)
        {
            if (map == null) return false;
            for (int i = 0; i < map.Pois.Count; i++)
            {
                if (map.Pois[i].Kind == PoiKind.SpawnPoint) return true;
            }
            return false;
        }

        // The seeded shift of arrival number n, in [-jitter, +jitter] ticks
        private long Jitter(in DrifterTuning tuning, int n)
        {
            if (tuning.ArrivalJitterTicks == 0) return 0;
            ulong seed = _settings != null ? _settings.Seed : SimulationSettings.DefaultSeed;
            var rng = SimRandom.For(seed, default, n, SimRandomSalt.DrifterSchedule);
            return SimMath.RoundToInt(Draw(ref rng, -(float)tuning.ArrivalJitterTicks, tuning.ArrivalJitterTicks));
        }

        // A draw in [min, max) rounded to float after every step: Unity's Mono keeps a scalar chain's intermediates at
        // double precision and CoreCLR doesn't, so SimRandom.Range's chain can differ in its last bit (Epic CORE F1)
        private static float Draw(ref SimRandom rng, float min, float max)
        {
            float span = (float)(max - min);
            float scaled = (float)(rng.NextFloat() * span);
            return (float)(min + scaled);
        }
    }
}
