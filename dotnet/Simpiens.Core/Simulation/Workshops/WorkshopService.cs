using System;
using System.Collections.Generic;
using Simpiens.Diagnostics;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.World;
using ZLogger;

namespace Simpiens.Simulation.Workshops
{
    /// <summary>What the workshops need from the simulation: the stall they work from (Q7).</summary>
    public interface IWorkshopHost
    {
        IReadOnlyList<TradeStallData> ActiveStalls { get; }
        void UpdateStall(TradeStallData stall);
    }

    /// <summary>
    /// The outpost's stations (Epic 10 Part 2, W0.5, D5): one <see cref="StationState"/> per station of the loaded map,
    /// in the map's order, owned here as the guild's state is by its service, and saved in its own block. It follows
    /// the map the simulation loads; a respawn starts every station unbuilt. Main thread only.
    /// </summary>
    public sealed class WorkshopService
    {
        private MapDefinition _map;
        private StationState[] _stations = Array.Empty<StationState>();

        /// <summary>The loaded map's stations.</summary>
        public int Count => _stations.Length;

        /// <summary>The <paramref name="index"/>-th station's state, a copy.</summary>
        public StationState this[int index] => _stations[index];

        /// <summary>Where the <paramref name="index"/>-th station stands, and what kind it is.</summary>
        public StationDefinition DefinitionAt(int index) => _map.Stations[index];

        /// <summary>Lays out <paramref name="map"/>'s stations, none built, unless it is the map already followed. Returns this.</summary>
        public WorkshopService Follow(MapDefinition map)
        {
            if (ReferenceEquals(map, _map)) return this;
            _map = map;
            int count = map != null ? map.Stations.Count : 0;
            _stations = count == 0 ? Array.Empty<StationState>() : new StationState[count];
            for (int i = 0; i < count; i++) _stations[i] = StationState.Unbuilt(map.Stations[i].Kind);
            return this;
        }

        /// <summary>Forgets every station (a respawn or a load); the next <see cref="Follow"/> lays the map's out afresh.</summary>
        public void Reset()
        {
            _map = null;
            _stations = Array.Empty<StationState>();
        }

        /// <summary>The station with <paramref name="key"/> on the map, or -1.</summary>
        public int IndexOf(string key)
        {
            for (int i = 0; i < _stations.Length; i++)
            {
                if (_map.Stations[i].Key == key) return i;
            }
            return -1;
        }

        /// <summary>The first station of <paramref name="kind"/>, or -1.</summary>
        public int IndexOf(StationKind kind)
        {
            for (int i = 0; i < _stations.Length; i++)
            {
                if (_stations[i].Kind == kind) return i;
            }
            return -1;
        }

        /// <summary>Builds the station (W1.1's command pays for it first). False when there is none, or it already stands.</summary>
        public bool Build(int index)
        {
            if ((uint)index >= (uint)_stations.Length || _stations[index].Built) return false;
            _stations[index].Built = true;
            SimLog.Economy.ZLogInformation($"The {_stations[index].Kind} was built at {_map.Stations[index].Key}");
            return true;
        }

        /// <summary>
        /// Orders <paramref name="count"/> makes of <paramref name="item"/> at a built station whose recipe it is; the
        /// same item again changes how many are left, the current make included. Another item, or none (a cancel), gives
        /// a started make's inputs back to the stall. Refused while a finished make waits for room.
        /// </summary>
        public bool SetOrder(IWorkshopHost host, int index, ItemType item, ushort count)
        {
            if ((uint)index >= (uint)_stations.Length) return false;
            ref var station = ref _stations[index];
            if (!station.Built || station.HeldOutput > 0) return false;
            bool cancel = item == ItemType.None || count == 0;
            if (!cancel)
            {
                var recipe = RecipeBook.For(item);
                if (recipe == null || recipe.Station != station.Kind) return false;
            }
            if (!cancel && item == station.Order)
            {
                station.Remaining = count;
                return true;
            }
            if (station.InputsTaken) GiveBackInputs(host, station.Order);
            station.Order = cancel ? ItemType.None : item;
            station.Remaining = cancel ? (ushort)0 : count;
            station.Progress = 0;
            station.InputsTaken = false;
            return true;
        }

        /// <summary>One tick of an artisan's work at the station, on the session's stall (<see cref="Crafting.Work"/>).</summary>
        public CraftStep Work(IWorkshopHost host, int index)
        {
            if ((uint)index >= (uint)_stations.Length || host?.ActiveStalls == null || host.ActiveStalls.Count == 0) return CraftStep.Idle;
            var stall = host.ActiveStalls[0]; // the session's one stall: what the stations draw on and stock (Q7)
            ref var station = ref _stations[index];
            var item = station.Order;
            var step = Crafting.Work(ref station, ref stall, out bool stallChanged);
            if (stallChanged) host.UpdateStall(stall);
            if (step == CraftStep.Made) SimLog.Economy.ZLogInformation($"The {station.Kind} made {ItemCatalog.NameOf(item)} ({station.Remaining} left in the order)");
            return step;
        }

        public void CaptureTo(WorkshopSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            save.Stations = (StationState[])_stations.Clone();
        }

        /// <summary>
        /// Puts a save's stations back, after the map is followed: each station whose kind matches the map's, in the
        /// map's order. A save without the block leaves every station unbuilt.
        /// </summary>
        public void RestoreFrom(WorkshopSave save)
        {
            for (int i = 0; i < _stations.Length; i++) _stations[i] = StationState.Unbuilt(_stations[i].Kind);
            var saved = save?.Stations;
            if (saved == null) return;
            for (int i = 0; i < _stations.Length && i < saved.Length; i++)
            {
                if (saved[i].Kind == _stations[i].Kind) _stations[i] = saved[i];
            }
        }

        private static void GiveBackInputs(IWorkshopHost host, ItemType order)
        {
            var recipe = RecipeBook.For(order);
            if (recipe == null || host?.ActiveStalls == null || host.ActiveStalls.Count == 0) return;
            var stall = host.ActiveStalls[0];
            for (int i = 0; i < recipe.InputCount; i++)
            {
                var input = recipe.GetInput(i);
                if (stall.Trades(input.Item)) stall = stall.WithStock(input.Item, (ushort)Math.Min(ushort.MaxValue, stall.StockOf(input.Item) + input.Count));
            }
            host.UpdateStall(stall);
        }
    }
}
