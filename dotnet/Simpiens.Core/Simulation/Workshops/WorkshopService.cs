using System;
using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Cognition.Groups;
using Simpiens.Diagnostics;
using Simpiens.Narrative;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Persistence;
using Simpiens.Simulation.World;
using ZLogger;

namespace Simpiens.Simulation.Workshops
{
    /// <summary>
    /// What the workshops need from the simulation: the stall they work from and pay from (Q7), the ledger, the crawlers
    /// they hire, the groups their crews are, and the guild (one role at a time, D6).
    /// </summary>
    public interface IWorkshopHost
    {
        IReadOnlyList<TradeStallData> ActiveStalls { get; }
        void UpdateStall(TradeStallData stall);
        IEconomyLedger EconomyLedger { get; }
        INarrativeChronicleService ChronicleService { get; }
        EconomyTuning Economy { get; }
        int GetSectorForPosition(Vec2 position);
        bool TryGetCrawler(EntityId crawlerId, out AgentState crawler);
        IGroupRegistry Groups { get; }
        Simpiens.Simulation.Guild.GuildService Guild { get; }
    }

    /// <summary>
    /// The outpost's stations (Epic 10 Part 2, W0.5, D5): one <see cref="StationState"/> per station of the loaded map,
    /// in the map's order, owned here as the guild's state is by its service, and saved in its own block. It follows
    /// the map the simulation loads; a respawn starts every station unbuilt. Main thread only.
    /// </summary>
    public sealed class WorkshopService
    {
        /// <summary>The ledger's sink for building stations (D10).</summary>
        public const string ConstructionSinkName = "Construction";

        private MapDefinition _map;
        private StationState[] _stations = Array.Empty<StationState>();

        // The artisans' contracts in hire order (the order wages are paid in), and who can't be hired again yet (D9)
        private readonly List<ArtisanContract> _artisans = new List<ArtisanContract>(4);
        private readonly Dictionary<EntityId, uint> _cooldowns = new Dictionary<EntityId, uint>();
        private readonly List<ArtisanContract> _quitting = new List<ArtisanContract>(4);

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
            _artisans.Clear();
            _cooldowns.Clear();
        }

        /// <summary>Every artisan's contract, in hire order.</summary>
        public IReadOnlyList<ArtisanContract> Artisans => _artisans;

        public bool IsArtisan(EntityId crawlerId) => IndexOfArtisan(crawlerId) >= 0;

        public bool TryGetContract(EntityId crawlerId, out ArtisanContract contract)
        {
            int i = IndexOfArtisan(crawlerId);
            contract = i >= 0 ? _artisans[i] : default;
            return i >= 0;
        }

        /// <summary>Who works the station at <paramref name="index"/>; default when nobody does.</summary>
        public EntityId ArtisanAt(int index)
        {
            for (int i = 0; i < _artisans.Count; i++)
            {
                if (_artisans[i].StationIndex == index) return _artisans[i].CrawlerId;
            }
            return default;
        }

        /// <summary>The tick before which <paramref name="crawlerId"/> can't be hired again (0 when it can).</summary>
        public uint CooldownUntil(EntityId crawlerId) => _cooldowns.TryGetValue(crawlerId, out uint until) ? until : 0;

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

        /// <summary>The rule's verdict on building the station at <paramref name="index"/> now, without acting.</summary>
        public WorkshopRules.Check CanBuild(IWorkshopHost host, int index)
        {
            bool hasStall = host?.ActiveStalls != null && host.ActiveStalls.Count > 0;
            return WorkshopRules.CheckBuild(this, index, hasStall, hasStall ? host.ActiveStalls[0].TreasuryGold : 0, Tuning(host));
        }

        /// <summary>
        /// Builds the station at <paramref name="index"/> (D10): the stall pays the construction, which leaves the economy
        /// as the <see cref="ConstructionSinkName"/> sink, and the station stands, ready for an artisan and an order.
        /// Refused, it changes nothing.
        /// </summary>
        public WorkshopRules.Check Build(IWorkshopHost host, int index, uint tick)
        {
            var check = CanBuild(host, index);
            if (check != WorkshopRules.Check.Ok)
            {
                SimLog.Economy.ZLogWarning($"Build refused for station {index}: {check} ({WorkshopRules.Describe(check, this, index, Tuning(host))})");
                return check;
            }
            uint cost = Tuning(host).ConstructionCost;
            var stall = host.ActiveStalls[0];
            host.UpdateStall(stall.WithTreasury(stall.TreasuryGold - cost));
            host.EconomyLedger?.RecordSink(cost, ConstructionSinkName);
            _stations[index].Built = true;

            var station = _map.Stations[index];
            SimLog.Economy.ZLogInformation($"The {_stations[index].Kind} was built at {station.Key} for {cost}g (treasury now {stall.TreasuryGold - cost}g)");
            host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.StationBuilt, default, WorldRegistry.PlayerEntityId,
                host.GetSectorForPosition(station.Position), tick, ChronicleIntensity.Notable, (byte)_stations[index].Kind));
            return WorkshopRules.Check.Ok;
        }

        private static WorkshopTuning Tuning(IWorkshopHost host) => host?.Economy?.Workshops ?? WorkshopTuning.Default;

        /// <summary>The rule's verdict on hiring <paramref name="crawlerId"/> at the station at <paramref name="index"/> for <paramref name="wage"/> now, without acting.</summary>
        public WorkshopRules.Check CanHire(IWorkshopHost host, EntityId crawlerId, int index, ushort wage, uint tick)
        {
            host.TryGetCrawler(crawlerId, out var crawler);
            return WorkshopRules.CheckHire(this, host.Guild, crawler, index, wage, tick, Tuning(host));
        }

        /// <summary>
        /// Hires <paramref name="crawlerId"/> to work the station at <paramref name="index"/> for <paramref name="wage"/>
        /// a visit (D9, D11): it joins the station's crew, a Workshop group led by the patron and anchored at the station,
        /// made with the station's first hire; it keeps its settlement; its contract starts. No fee changes hands.
        /// Refused, it changes nothing.
        /// </summary>
        public WorkshopRules.Check Hire(IWorkshopHost host, EntityId crawlerId, int index, ushort wage, uint tick)
        {
            var check = CanHire(host, crawlerId, index, wage, tick);
            if (check != WorkshopRules.Check.Ok)
            {
                SimLog.Economy.ZLogWarning($"Hire refused for {SimLog.Agent(crawlerId)} at station {index}: {check}");
                return check;
            }
            host.TryGetCrawler(crawlerId, out var crawler);
            var tuning = Tuning(host);
            ref var station = ref _stations[index];
            var place = _map.Stations[index];
            var groups = host.Groups;
            if (groups != null)
            {
                if (station.CrewGroupId == 0 || !groups.TryGetGroup(station.CrewGroupId, out _))
                {
                    station.CrewGroupId = groups.CreateGroup(GroupType.Workshop, WorldRegistry.PlayerEntityId, place.Position, initialCohesion: 100);
                }
                if (station.CrewGroupId != 0) groups.AddMember(station.CrewGroupId, crawlerId, RoleRank.Follower, tuning.InitialAllegiance);
                crawler.Memberships = groups.GetMemberships(crawlerId);
            }
            _artisans.Add(new ArtisanContract(crawlerId, (byte)index, wage, tick));
            _cooldowns.Remove(crawlerId);

            SimLog.Economy.ZLogInformation($"{SimLog.Agent(crawlerId)} took up work at the {station.Kind} for {wage}g a visit");
            host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.ArtisanHired, crawlerId, WorldRegistry.PlayerEntityId,
                host.GetSectorForPosition(place.Position), tick, ChronicleIntensity.Dramatic, wage));
            return WorkshopRules.Check.Ok;
        }

        /// <summary>The player lets an artisan go: it leaves its crew only, loses trust, and can't be hired again for a while.</summary>
        public WorkshopRules.Check Dismiss(IWorkshopHost host, EntityId crawlerId, uint tick)
        {
            var check = WorkshopRules.CheckDismiss(this, crawlerId);
            if (check != WorkshopRules.Check.Ok) return check;
            var tuning = Tuning(host);
            TryGetContract(crawlerId, out var contract);
            Leave(host, crawlerId, tick, tuning);
            if (host.TryGetCrawler(crawlerId, out var crawler))
            {
                crawler.Memory?.RecordInteraction(WorldRegistry.PlayerEntityId, 0, tuning.DismissTrust, 0, 0, tick);
            }
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(crawlerId)} was let go from the {_stations[contract.StationIndex].Kind}");
            host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.ArtisanDismissed, crawlerId, WorldRegistry.PlayerEntityId,
                host.GetSectorForPosition(_map.Stations[contract.StationIndex].Position), tick, ChronicleIntensity.Notable, (byte)_stations[contract.StationIndex].Kind));
            return WorkshopRules.Check.Ok;
        }

        /// <summary>The player sets an artisan's wage per caravan visit.</summary>
        public WorkshopRules.Check SetWage(IWorkshopHost host, EntityId crawlerId, ushort wage, uint tick)
        {
            var check = WorkshopRules.CheckSetWage(this, crawlerId, wage, Tuning(host));
            if (check != WorkshopRules.Check.Ok) return check;
            int i = IndexOfArtisan(crawlerId);
            _artisans[i] = _artisans[i].WithWage(wage);
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(crawlerId)}'s wage at the {_stations[_artisans[i].StationIndex].Kind} is now {wage}g a visit");
            return WorkshopRules.Check.Ok;
        }

        /// <summary>
        /// The caravan's visit pays every artisan's wage from <paramref name="stall"/>, after the guild's members, in hire
        /// order, each in full or not at all (D9), as a member's is: a paid artisan's trust, warmth and allegiance rise; an
        /// unpaid one's fall, and its unpaid visits count up. Then an artisan quits below the trust floor or after too
        /// many unpaid visits in a row, leaving its crew only. Wages are conserved transfers. Returns the stall as it is after.
        /// </summary>
        public TradeStallData PayWages(IWorkshopHost host, TradeStallData stall, uint tick)
        {
            if (_artisans.Count == 0 || host.ActiveStalls.Count == 0 || stall.StallEntityId != host.ActiveStalls[0].StallEntityId) return stall;
            var tuning = Tuning(host);
            var groups = host.Groups;
            bool changed = false;
            for (int i = 0; i < _artisans.Count; i++)
            {
                var contract = _artisans[i];
                if (!host.TryGetCrawler(contract.CrawlerId, out var artisan)) continue;
                byte crew = _stations[contract.StationIndex].CrewGroupId;
                bool paid = stall.TreasuryGold >= contract.Wage && artisan.Inventory != null;
                if (paid)
                {
                    if (host.EconomyLedger != null) host.EconomyLedger.TryPayFromStall(stall.StallEntityId, ref stall, artisan.Inventory, contract.Wage, StallPayment.ArtisanWage);
                    else
                    {
                        stall = stall.WithTreasury(stall.TreasuryGold - contract.Wage);
                        artisan.Inventory.AddGold(contract.Wage);
                    }
                    changed = true;
                    artisan.Memory?.RecordInteraction(WorldRegistry.PlayerEntityId, tuning.Paid.Warmth, tuning.Paid.Trust, 0, tuning.Paid.Respect, tick);
                    if (crew != 0) groups?.UpdateAllegiance(contract.CrawlerId, crew, tuning.PaidAllegiance);
                    _artisans[i] = contract.WithUnpaidVisits(0);
                }
                else
                {
                    artisan.Memory?.RecordInteraction(WorldRegistry.PlayerEntityId, tuning.Unpaid.Warmth, tuning.Unpaid.Trust, 0, tuning.Unpaid.Respect, tick);
                    if (crew != 0) groups?.UpdateAllegiance(contract.CrawlerId, crew, tuning.UnpaidAllegiance);
                    _artisans[i] = contract.WithUnpaidVisits((byte)Math.Min(byte.MaxValue, contract.UnpaidVisits + 1));
                    SimLog.Economy.ZLogWarning($"{SimLog.Agent(contract.CrawlerId)} went unpaid at the {_stations[contract.StationIndex].Kind} ({contract.Wage}g; the treasury holds {stall.TreasuryGold}g), {_artisans[i].UnpaidVisits} visit(s) in a row");
                    host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.ArtisanUnpaid, contract.CrawlerId, WorldRegistry.PlayerEntityId,
                        host.GetSectorForPosition(_map.Stations[contract.StationIndex].Position), tick, ChronicleIntensity.Notable, (byte)_stations[contract.StationIndex].Kind));
                }
                if (groups != null) artisan.Memberships = groups.GetMemberships(contract.CrawlerId);
            }
            if (changed) host.UpdateStall(stall);

            // Quitting, in hire order: below the trust floor, or after too many unpaid visits in a row
            _quitting.Clear();
            for (int i = 0; i < _artisans.Count; i++)
            {
                var contract = _artisans[i];
                if (!host.TryGetCrawler(contract.CrawlerId, out var artisan)) continue;
                var (trust, _) = Simpiens.Simulation.Guild.GuildRules.Standing(artisan, tick);
                if (trust < tuning.QuitTrust || contract.UnpaidVisits >= tuning.MaxUnpaidVisits) _quitting.Add(contract);
            }
            for (int i = 0; i < _quitting.Count; i++)
            {
                var contract = _quitting[i];
                var kind = _stations[contract.StationIndex].Kind;
                Leave(host, contract.CrawlerId, tick, tuning);
                SimLog.Economy.ZLogInformation($"{SimLog.Agent(contract.CrawlerId)} quit the {kind} ({contract.UnpaidVisits} unpaid visit(s) in a row)");
                host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.ArtisanQuit, contract.CrawlerId, WorldRegistry.PlayerEntityId,
                    host.GetSectorForPosition(_map.Stations[contract.StationIndex].Position), tick, ChronicleIntensity.Dramatic, (byte)kind));
            }
            return stall;
        }

        /// <summary>
        /// An artisan walks out on its shift's order (D13: it deserted the crew's directive, Epic 8): it quits the station
        /// as an unpaid one does, keeping its settlement, and the chronicle tells of it.
        /// </summary>
        public void Desert(IWorkshopHost host, EntityId crawlerId, uint tick)
        {
            if (!TryGetContract(crawlerId, out var contract)) return;
            var kind = _stations[contract.StationIndex].Kind;
            Leave(host, crawlerId, tick, Tuning(host));
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(crawlerId)} walked out of the {kind}: it wouldn't work its shift");
            host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.ArtisanQuit, crawlerId, WorldRegistry.PlayerEntityId,
                host.GetSectorForPosition(_map.Stations[contract.StationIndex].Position), tick, ChronicleIntensity.Dramatic, (byte)kind));
        }

        /// <summary>Ends an artisan's contract (quitting or dismissal): it leaves its crew only, and can't be hired again for a while.</summary>
        internal void Leave(IWorkshopHost host, EntityId crawlerId, uint tick, in WorkshopTuning tuning)
        {
            int i = IndexOfArtisan(crawlerId);
            if (i < 0) return;
            byte crew = _stations[_artisans[i].StationIndex].CrewGroupId;
            _artisans.RemoveAt(i);
            _cooldowns[crawlerId] = tick + tuning.RehireCooldownTicks;
            var groups = host.Groups;
            if (groups != null && crew != 0)
            {
                groups.RemoveMember(crew, crawlerId);
                if (host.TryGetCrawler(crawlerId, out var crawler)) crawler.Memberships = groups.GetMemberships(crawlerId);
            }
        }

        /// <summary>A crawler who left the outpost works no station; its contract ends with it (the groups already let it go).</summary>
        public void Forget(EntityId crawlerId)
        {
            int i = IndexOfArtisan(crawlerId);
            if (i >= 0) _artisans.RemoveAt(i);
            _cooldowns.Remove(crawlerId);
        }

        private int IndexOfArtisan(EntityId crawlerId)
        {
            for (int i = 0; i < _artisans.Count; i++)
            {
                if (_artisans[i].CrawlerId == crawlerId) return i;
            }
            return -1;
        }

        /// <summary>
        /// <paramref name="crawlerId"/>'s part in its station's work at <paramref name="tick"/> (D13): on shift when it is an
        /// artisan, not on a break, its station stands with an order, and the tick falls in one of its blocks. Default otherwise.
        /// </summary>
        public Simpiens.Cognition.Contracts.StationOrder OrderFor(EntityId crawlerId, uint tick)
        {
            int i = IndexOfArtisan(crawlerId);
            if (i < 0 || !IsOnShift(_artisans[i], tick)) return default;
            var place = _map.Stations[_artisans[i].StationIndex];
            return new Simpiens.Cognition.Contracts.StationOrder(_artisans[i].StationIndex, place.Position, place.Radius);
        }

        private bool IsOnShift(in ArtisanContract contract, uint tick)
        {
            if (contract.OnBreak || contract.StationIndex >= _stations.Length) return false;
            var station = _stations[contract.StationIndex];
            return station.Built && station.HasOrder && ShiftBlocks.Covers(contract.Shifts, tick);
        }

        /// <summary>
        /// Keeps each crew's standing order with its artisan's shift (D13), once a tick on the main thread, as the guild
        /// keeps its expedition's: on shift, the crew's directive is to work the station, issued at the start of each block
        /// and expiring at its end, so each block's order is judged anew (Epic 8); off shift, or on a break, there is none.
        /// First, overwork (D15, D16): an artisan on shift at the break line breaks down, and one on a break is back once
        /// rested.
        /// </summary>
        public void TickShifts(IWorkshopHost host, uint tick)
        {
            var groups = host?.Groups;
            if (groups == null) return;
            var tuning = Tuning(host);
            for (int i = 0; i < _artisans.Count; i++)
            {
                if (host.TryGetCrawler(_artisans[i].CrawlerId, out var crawler) && crawler != null)
                {
                    if (_artisans[i].OnBreak)
                    {
                        if (crawler.Energy >= tuning.RestedEnergy) _artisans[i] = _artisans[i].WithOnBreak(false);
                    }
                    else if (IsOnShift(_artisans[i], tick))
                    {
                        // On shift without a gap: three blocks in a row break it whatever its energy, the break line sooner (D35)
                        ushort run = (ushort)Math.Min(ushort.MaxValue, _artisans[i].ShiftRunTicks + 1);
                        _artisans[i] = _artisans[i].WithShiftRun(run);
                        if (run >= tuning.BreakAfterTicks || crawler.Energy <= tuning.BreakEnergy) BreakDown(host, i, crawler, tick, tuning);
                    }
                    else if (_artisans[i].ShiftRunTicks != 0)
                    {
                        _artisans[i] = _artisans[i].WithShiftRun(0);
                    }
                }
                var contract = _artisans[i];
                byte crew = contract.StationIndex < _stations.Length ? _stations[contract.StationIndex].CrewGroupId : (byte)0;
                if (crew == 0 || !groups.TryGetGroup(crew, out var group)) continue;
                bool ordered = group.ActiveDirective.Type == DirectiveType.WorkStation;
                if (IsOnShift(contract, tick))
                {
                    if (ordered && group.ActiveDirective.IsActive(tick)) continue;
                    var place = _map.Stations[contract.StationIndex];
                    uint blockEnd = tick - tick % ShiftBlocks.TicksEach + ShiftBlocks.TicksEach - 1;
                    groups.SetDirective(crew, new GroupDirective(DirectiveType.WorkStation, crew, WorldRegistry.PlayerEntityId,
                        place.Position, default, tick, blockEnd, place.Radius));
                }
                else if (ordered)
                {
                    groups.ClearDirective(crew);
                }
            }
        }

        /// <summary>
        /// An artisan worked past the end of its strength (D16): it breaks down, panicking as a tantrum does, loses trust and
        /// warmth toward the player, and works no shift until rested. Repeated breaks bring it under the quit line, and it
        /// quits at a caravan visit (W1.2's rule).
        /// </summary>
        private void BreakDown(IWorkshopHost host, int index, AgentState crawler, uint tick, in WorkshopTuning tuning)
        {
            var contract = _artisans[index];
            _artisans[index] = contract.WithOnBreak(true);
            crawler.Memory?.RecordInteraction(WorldRegistry.PlayerEntityId, tuning.Break.Warmth, tuning.Break.Trust, 0, tuning.Break.Respect, tick);
            crawler.TriggerTantrumVisual();
            var kind = _stations[contract.StationIndex].Kind;
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(contract.CrawlerId)} broke down at the {kind}: overworked, {contract.ShiftRunTicks / DayClock.TicksPerHour} h on shift at energy {crawler.Energy:F0}");
            host.ChronicleService?.RecordEvent(new ChronicleEvent(ChronicleEventType.ArtisanBrokeDown, contract.CrawlerId, WorldRegistry.PlayerEntityId,
                host.GetSectorForPosition(_map.Stations[contract.StationIndex].Position), tick, ChronicleIntensity.Dramatic, (byte)kind));
        }

        /// <summary>
        /// The player's order (D32): <paramref name="count"/> makes of <paramref name="item"/> at the station at
        /// <paramref name="index"/>, through <see cref="WorkshopRules.CheckOrder"/>; none, or a count of 0, cancels.
        /// </summary>
        public WorkshopRules.Check Order(IWorkshopHost host, int index, ItemType item, ushort count)
        {
            var check = WorkshopRules.CheckOrder(this, index, item, count);
            if (check != WorkshopRules.Check.Ok)
            {
                SimLog.Economy.ZLogWarning($"Order refused at station {index}: {count} {ItemCatalog.NameOf(item)} ({check})");
                return check;
            }
            SetOrder(host, index, item, count);
            SimLog.Economy.ZLogInformation($"The {_stations[index].Kind} is ordered to make {count} {ItemCatalog.NameOf(item)}");
            return WorkshopRules.Check.Ok;
        }

        /// <summary>The player sets which blocks of the day an artisan works (D12), through <see cref="WorkshopRules.CheckSetShifts"/>.</summary>
        public WorkshopRules.Check SetShifts(IWorkshopHost host, EntityId crawlerId, byte shifts)
        {
            var check = WorkshopRules.CheckSetShifts(this, crawlerId, shifts);
            if (check != WorkshopRules.Check.Ok) return check;
            int i = IndexOfArtisan(crawlerId);
            _artisans[i] = _artisans[i].WithShifts(shifts);
            SimLog.Economy.ZLogInformation($"{SimLog.Agent(crawlerId)} works {ShiftBlocks.CountOf(shifts)} blocks a day at the {_stations[_artisans[i].StationIndex].Kind}");
            return WorkshopRules.Check.Ok;
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
        /// <summary>An artisan's grade from its practice (W4, Q8, D55): Standard, then Fine from 10 makes, Masterwork from 20.</summary>
        public static byte GradeOf(in ArtisanContract contract, in WorkshopTuning tuning)
        {
            int per = tuning.MakesPerGrade > 0 ? tuning.MakesPerGrade : 10;
            int grade = contract.Practice / per;
            return (byte)(grade < Grades.Masterwork ? grade : Grades.Masterwork);
        }

        public CraftStep Work(IWorkshopHost host, int index)
        {
            if ((uint)index >= (uint)_stations.Length || host?.ActiveStalls == null || host.ActiveStalls.Count == 0) return CraftStep.Idle;
            var stall = host.ActiveStalls[0]; // the session's one stall: what the stations draw on and stock (Q7)
            ref var station = ref _stations[index];
            var item = station.Order;
            // The make is at its artisan's grade, and a finished one is practice (W4, D55)
            int artisan = -1;
            for (int i = 0; i < _artisans.Count && artisan < 0; i++) if (_artisans[i].StationIndex == index) artisan = i;
            byte grade = artisan >= 0 ? GradeOf(_artisans[artisan], Tuning(host)) : Grades.Standard;
            uint made = station.Made;
            var step = Crafting.Work(ref station, ref stall, out bool stallChanged, grade);
            if (station.Made > made && artisan >= 0) _artisans[artisan] = _artisans[artisan].WithPractice((ushort)Math.Min(ushort.MaxValue, _artisans[artisan].Practice + 1));
            if (stallChanged) host.UpdateStall(stall);
            if (step == CraftStep.Made) SimLog.Economy.ZLogInformation($"The {station.Kind} made {ItemCatalog.NameOf(item)} ({station.Remaining} left in the order)");
            return step;
        }

        public void CaptureTo(WorkshopSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            save.Stations = (StationState[])_stations.Clone();
            save.Artisans = _artisans.ToArray();
            var cooldowns = new IdTickRecord[_cooldowns.Count];
            int n = 0;
            foreach (var kvp in _cooldowns) cooldowns[n++] = new IdTickRecord { Id = kvp.Key, Tick = kvp.Value };
            Array.Sort(cooldowns, (a, b) => a.Id.CompareTo(b.Id));
            save.Cooldowns = cooldowns;
        }

        /// <summary>
        /// Puts a save's stations back, after the map is followed: each station whose kind matches the map's, in the
        /// map's order; then the artisans' contracts, in hire order, and the cooldowns. A save without the block leaves
        /// every station unbuilt and nobody hired.
        /// </summary>
        public void RestoreFrom(WorkshopSave save)
        {
            for (int i = 0; i < _stations.Length; i++) _stations[i] = StationState.Unbuilt(_stations[i].Kind);
            _artisans.Clear();
            _cooldowns.Clear();
            var saved = save?.Stations;
            if (saved == null) return;
            for (int i = 0; i < _stations.Length && i < saved.Length; i++)
            {
                if (saved[i].Kind == _stations[i].Kind) _stations[i] = saved[i];
            }
            if (save.Artisans != null)
            {
                for (int i = 0; i < save.Artisans.Length; i++)
                {
                    if (save.Artisans[i].StationIndex < _stations.Length) _artisans.Add(save.Artisans[i]);
                }
            }
            if (save.Cooldowns != null)
            {
                for (int i = 0; i < save.Cooldowns.Length; i++) _cooldowns[save.Cooldowns[i].Id] = save.Cooldowns[i].Tick;
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
