using System.Collections.Generic;
using Simpiens.Cognition;
using Simpiens.Intervention;
using Simpiens.Presentation.Art.People;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using Simpiens.Simulation.Guild;
using Simpiens.Simulation.Items;
using Simpiens.Simulation.Workshops;
using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>A resident who would take the work at the shown station now, at its ask (Epic 10 Part 2, W1.6a).</summary>
    public sealed class StationCandidateRow
    {
        public EntityId CrawlerId { get; internal set; }
        public string Name { get; internal set; } = string.Empty;
        public bool HasPortrait { get; internal set; }
        public PortraitKey Portrait { get; internal set; }
        /// <summary>"Trust 62 · Warmth 41".</summary>
        public string Standing { get; internal set; } = string.Empty;
        /// <summary>The least wage it takes, a visit.</summary>
        public int Ask { get; internal set; }
    }

    /// <summary>One thing the shown station makes, with the count the player is about to order.</summary>
    public sealed class StationRecipeRow
    {
        public ItemType Item { get; internal set; }
        public string Name { get; internal set; } = string.Empty;
        public string Icon { get; internal set; }
        /// <summary>"3 iron ore · 2 h of work".</summary>
        public string Recipe { get; internal set; } = string.Empty;
        /// <summary>How many the next order asks for.</summary>
        public int PendingCount { get; set; } = 1;

        /// <summary>Worn pieces of it waiting on the stall's shelf for the forge to mend (Epic 10 Part 2, W4, D58).</summary>
        public int Worn { get; internal set; }
    }

    /// <summary>
    /// The station window (Epic 10 Part 2, W1.6a): one of the map's stations at a time (the forge, the still), its
    /// building, its artisan or those who would take the work, its shifts and its order. The rules are the core's
    /// (<see cref="WorkshopRules"/>), in their words; the player's actions leave as typed commands, applied on the next
    /// tick: Build, Hire (with the day shift, D12), SetWage, SetShifts, Order and Dismiss. Views decide nothing.
    /// </summary>
    public sealed class StationModel : IDashboardWindow
    {
        public const int MaxCandidates = 3;
        public const int MaxOrder = 20;
        private const string Source = "Player";

        private readonly DashboardContext _context;
        private readonly MoodTracker _moods;
        private readonly List<StationCandidateRow> _candidates = new List<StationCandidateRow>(MaxCandidates);
        private readonly List<StationRecipeRow> _recipes = new List<StationRecipeRow>(4);
        private readonly List<(int Trust, AgentState Crawler)> _willing = new List<(int, AgentState)>(16);
        private int _recipesOf = -1; // the station whose recipes _recipes holds
        private EntityId _artisanShown;

        internal StationModel(DashboardContext context, MoodTracker moods = null)
        {
            _context = context;
            _moods = moods ?? new MoodTracker();
        }

        public bool IsOpen { get; private set; }

        /// <summary>The station shown: its index in the map's stations, which the commands name.</summary>
        public int Index { get; private set; }

        public int StationCount => Workshops?.Count ?? 0;

        /// <summary>A station's tab: "Forge", "Still".</summary>
        public string TabName(int index) => (uint)index < (uint)StationCount ? Capitalised(WorkshopRules.StationName(Workshops[index].Kind)) : string.Empty;

        public string Title { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public bool Built { get; private set; }

        /// <summary>"An empty plot" or "Standing · 3 made".</summary>
        public string Status { get; private set; } = string.Empty;

        // --- Building ---
        public bool CanBuild { get; private set; }
        /// <summary>"Build for 300g".</summary>
        public string BuildLabel { get; private set; } = string.Empty;
        /// <summary>What building does, or the rule's reason it can't be built now.</summary>
        public string BuildNote { get; private set; } = string.Empty;

        // --- The artisan ---
        public bool HasArtisan { get; private set; }
        public EntityId ArtisanId { get; private set; }
        public string ArtisanName { get; private set; } = string.Empty;
        public string ArtisanDoing { get; private set; } = string.Empty;
        /// <summary>"Energy 72 · on shift", "On a break until rested", "Off shift until 10:00".</summary>
        public string ArtisanState { get; private set; } = string.Empty;
        public bool ArtisanOnBreak { get; private set; }
        public bool ArtisanHasPortrait { get; private set; }
        public PortraitKey ArtisanPortrait { get; private set; }
        public int Wage { get; private set; }
        /// <summary>The wage the stepper sets; Apply sends it.</summary>
        public int PendingWage { get; private set; }
        public byte Shifts { get; private set; }
        /// <summary>The blocks the toggles set; Apply sends them.</summary>
        public byte PendingShifts { get; private set; }
        /// <summary>"8 hours a day", and a warning when the blocks tire it.</summary>
        public string ShiftsNote { get; private set; } = string.Empty;
        public bool ShiftsTire { get; private set; }
        public bool HasChanges => HasArtisan && (PendingWage != Wage || PendingShifts != Shifts);

        // --- Who would take the work ---
        public IReadOnlyList<StationCandidateRow> Candidates => _candidates;
        public string CandidatesNote { get; private set; } = string.Empty;

        // --- The order ---
        public IReadOnlyList<StationRecipeRow> Recipes => _recipes;
        public bool HasOrder { get; private set; }
        public bool CanOrder { get; private set; }
        /// <summary>"Making pickaxes: 2 to go · 45%", or "No order".</summary>
        public string OrderText { get; private set; } = string.Empty;
        /// <summary>Why the order waits, when it does.</summary>
        public string OrderNote { get; private set; } = string.Empty;

        public Feedback Feedback { get; private set; } = Feedback.None;

        /// <summary>A block's label: "06-10" (a hyphen: the baked fonts hold ASCII and Latin-1, no en dash).</summary>
        public static string BlockLabel(int block)
            => Invariant($"{ShiftBlocks.StartHour(block):00}-{ShiftBlocks.StartHour(block + 1):00}");

        public bool IsBlockOn(int block) => (PendingShifts & (1 << block)) != 0;

        /// <summary>Opens on the station at <paramref name="index"/> (a click on it), or the one shown last.</summary>
        public void Open(int index = -1)
        {
            if (index >= 0) Index = index;
            IsOpen = true;
            Feedback = Feedback.None;
            _artisanShown = default; // the steppers load from the contract
            Refresh();
        }

        public void Close() => IsOpen = false;

        /// <summary>Another station's tab.</summary>
        public void Show(int index)
        {
            if ((uint)index >= (uint)StationCount || index == Index) return;
            Index = index;
            Feedback = Feedback.None;
            _artisanShown = default;
            Refresh();
        }

        public void Refresh()
        {
            var workshops = Workshops;
            if (!IsOpen || workshops == null || workshops.Count == 0) return;
            if ((uint)Index >= (uint)workshops.Count) Index = 0;
            var tuning = Tuning;
            var state = workshops[Index];
            string station = WorkshopRules.StationName(state.Kind);
            Title = "The " + LongName(state.Kind);
            Description = DescriptionOf(state.Kind);
            Built = state.Built;
            Status = Built ? Invariant($"Standing · {state.Made} made") : Invariant($"An empty plot · {tuning.ConstructionCost}g to build");

            var host = _context.Simulation as IWorkshopHost;
            var build = host != null ? workshops.CanBuild(host, Index) : WorkshopRules.Check.NoStall;
            CanBuild = !Built && _context.Intervention != null && build == WorkshopRules.Check.Ok;
            BuildLabel = Invariant($"Build for {tuning.ConstructionCost}g");
            BuildNote = Built ? string.Empty
                : build == WorkshopRules.Check.Ok ? Invariant($"The stall pays {tuning.ConstructionCost}g and the {station} stands at once.")
                : WorkshopRules.Describe(build, workshops, Index, tuning);

            RefreshRecipes(state.Kind);
            bool stocked = _context.TryGetStall(out var stall);
            for (int i = 0; i < _recipes.Count; i++) _recipes[i].Worn = stocked ? stall.WornOf(_recipes[i].Item) : 0;
            RefreshArtisan(workshops, state, tuning);
            RefreshCandidates(workshops, host, tuning);
            RefreshOrder(state, station);
        }

        // --- The player's actions: typed commands, applied on the next tick ---

        public void Build()
        {
            if (!CanBuild) return;
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateBuildStation(Source, (byte)Index));
            Feedback = new Feedback(Invariant($"The {WorkshopRules.StationName(Workshops[Index].Kind)} is being built."), FeedbackTone.Ok);
        }

        /// <summary>Hire at its ask, working the day shift (D12: a hire works no block until given one, and the window offers the day shift).</summary>
        public void Hire(EntityId crawlerId)
        {
            if (_context.Intervention == null) return;
            StationCandidateRow row = null;
            for (int i = 0; i < _candidates.Count; i++) if (_candidates[i].CrawlerId == crawlerId) row = _candidates[i];
            if (row == null) return;
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateHireArtisan(Source, crawlerId, (byte)Index, (ushort)row.Ask));
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateSetShifts(Source, crawlerId, ShiftBlocks.DayShift));
            Feedback = new Feedback(Invariant($"{row.Name} is hired at {row.Ask}g a visit, on the day shift."), FeedbackTone.Ok);
        }

        public void LessWage() => PendingWage = SimMath.Clamp(PendingWage - 1, 1, Tuning.MaxWage);
        public void MoreWage() => PendingWage = SimMath.Clamp(PendingWage + 1, 1, Tuning.MaxWage);

        public void ToggleBlock(int block)
        {
            if ((uint)block >= ShiftBlocks.Count) return;
            PendingShifts = (byte)(PendingShifts ^ (1 << block));
            DescribeShifts();
        }

        /// <summary>Apply: the wage and the shifts that changed, as <see cref="SetWageCommand"/> and <see cref="SetShiftsCommand"/>.</summary>
        public void ApplyArtisan()
        {
            if (_context.Intervention == null || !HasArtisan) return;
            if (PendingWage != Wage) _context.Intervention.QueueInfluence(InfluenceEvent.CreateSetWage(Source, ArtisanId, (ushort)PendingWage));
            if (PendingShifts != Shifts) _context.Intervention.QueueInfluence(InfluenceEvent.CreateSetShifts(Source, ArtisanId, PendingShifts));
            Feedback = HasChanges ? new Feedback(Invariant($"{ArtisanName}'s work is set."), FeedbackTone.Ok) : Feedback.None;
        }

        public void Dismiss()
        {
            if (_context.Intervention == null || !HasArtisan) return;
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateDismiss(Source, ArtisanId));
            Feedback = new Feedback($"{ArtisanName} will be let go.", FeedbackTone.Ok);
        }

        public void LessCount(int recipe)
        {
            if ((uint)recipe < (uint)_recipes.Count) _recipes[recipe].PendingCount = SimMath.Clamp(_recipes[recipe].PendingCount - 1, 1, MaxOrder);
        }

        public void MoreCount(int recipe)
        {
            if ((uint)recipe < (uint)_recipes.Count) _recipes[recipe].PendingCount = SimMath.Clamp(_recipes[recipe].PendingCount + 1, 1, MaxOrder);
        }

        /// <summary>An order for the recipe's pending count, replacing the station's order (D32).</summary>
        public void Order(int recipe)
        {
            if (!CanOrder || (uint)recipe >= (uint)_recipes.Count) return;
            var row = _recipes[recipe];
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateOrderStation(Source, (byte)Index, row.Item, (ushort)row.PendingCount));
            Feedback = new Feedback(Invariant($"Ordered {row.PendingCount} × {row.Name.ToLowerInvariant()}."), FeedbackTone.Ok);
        }

        /// <summary>Mend: an order to repair every worn piece of the recipe's item waiting on the shelf (W4, D59).</summary>
        public void Mend(int recipe)
        {
            if (!CanOrder || (uint)recipe >= (uint)_recipes.Count || _recipes[recipe].Worn == 0) return;
            var row = _recipes[recipe];
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateOrderStation(Source, (byte)Index, row.Item, (ushort)row.Worn, repair: true));
            Feedback = new Feedback(Invariant($"Ordered {row.Worn} × {row.Name.ToLowerInvariant()} mended."), FeedbackTone.Ok);
        }

        public void CancelOrder()
        {
            if (!CanOrder || !HasOrder) return;
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateOrderStation(Source, (byte)Index, ItemType.None, 0));
            Feedback = new Feedback("The order is cancelled.", FeedbackTone.Ok);
        }

        private WorkshopService Workshops => _context.Simulation?.Workshops;
        private WorkshopTuning Tuning => _context.Simulation?.Economy?.Workshops ?? WorkshopTuning.Default;
        private uint Tick => (uint)(_context.Clock?.CurrentTick ?? 0);

        private void RefreshRecipes(StationKind kind)
        {
            if (_recipesOf == Index) return; // the counts the player set stay while the station is shown
            _recipesOf = Index;
            _recipes.Clear();
            var recipes = RecipeBook.At(kind);
            for (int i = 0; i < recipes.Count; i++)
            {
                var recipe = recipes[i];
                var inputs = new List<string>(recipe.InputCount);
                for (int k = 0; k < recipe.InputCount; k++)
                {
                    var input = recipe.GetInput(k);
                    inputs.Add(Invariant($"{input.Count} {ItemTexts.Of(input.Item).MerchantName.ToLowerInvariant()}"));
                }
                _recipes.Add(new StationRecipeRow
                {
                    Item = recipe.Output,
                    Name = ItemTexts.Of(recipe.Output).MerchantName,
                    Icon = ItemIcons.Of(recipe.Output),
                    Recipe = string.Join(", ", inputs) + " · " + DayClock.SpanText(recipe.WorkTicks) + " of work",
                });
            }
        }

        private void RefreshArtisan(WorkshopService workshops, StationState state, WorkshopTuning tuning)
        {
            var id = workshops.ArtisanAt(Index);
            HasArtisan = id != default && workshops.TryGetContract(id, out var contract) && contract.StationIndex == Index;
            if (!HasArtisan)
            {
                ArtisanId = default;
                _artisanShown = default;
                return;
            }
            workshops.TryGetContract(id, out contract);
            ArtisanId = id;
            Wage = contract.Wage;
            Shifts = contract.Shifts;
            ArtisanOnBreak = contract.OnBreak;
            if (_artisanShown != id)
            {
                _artisanShown = id; // a new artisan, or the window just opened: the steppers start from the contract
                PendingWage = contract.Wage;
                PendingShifts = contract.Shifts;
            }
            var node = _context.Registry?.GetEntity(id);
            var agent = node?.Agent;
            ArtisanName = node != null ? _context.FriendlyName(id, node.Name) : DossierModel.UnknownName;
            ArtisanHasPortrait = agent != null && agent.Look.IsComplete;
            ArtisanPortrait = ArtisanHasPortrait ? PortraitKey.Of(id, agent, _moods, Tick) : default;
            ArtisanDoing = agent != null ? PlanText.Action(agent.HasActivePlan ? agent.ActivePlan.CurrentAction : null) : string.Empty;
            if (contract.OnBreak) ArtisanState = Invariant($"Energy {agent?.Energy ?? 0:F0} · on a break until rested");
            else if (ShiftBlocks.Covers(contract.Shifts, Tick)) ArtisanState = Invariant($"Energy {agent?.Energy ?? 0:F0} · on shift");
            else ArtisanState = Invariant($"Energy {agent?.Energy ?? 0:F0} · {OffShiftText(contract.Shifts)}");
            // Its grade and its practice (W4, D55)
            ArtisanState += " · " + Grades.NameOf(WorkshopService.GradeOf(contract, tuning)) + Invariant($", {contract.Practice} made");
            DescribeShifts();
        }

        private void DescribeShifts()
        {
            int blocks = ShiftBlocks.CountOf(PendingShifts);
            // D35: a run of blocks reaching the break's hours tires an artisan into breaks
            ShiftsTire = ShiftBlocks.LongestRun(PendingShifts) * ShiftBlocks.TicksEach >= Tuning.BreakAfterTicks;
            string hours = blocks == 0 ? "No shifts: it works no block" : Invariant($"{blocks * ShiftBlocks.HoursEach} hours a day");
            ShiftsNote = ShiftsTire ? hours + " · that many in a row tires it: expect breaks" : hours;
        }

        private void RefreshCandidates(WorkshopService workshops, IWorkshopHost host, WorkshopTuning tuning)
        {
            _candidates.Clear();
            CandidatesNote = string.Empty;
            if (!Built || HasArtisan || host == null) return;
            _willing.Clear();
            var entities = _context.Registry?.ActiveEntities;
            uint tick = Tick;
            var guild = _context.Simulation.Guild;
            if (entities != null)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    var crawler = entities[i]?.Agent;
                    if (crawler == null || crawler.IsDrifter) continue;
                    ushort ask = WorkshopRules.ReservationWage(crawler, tuning);
                    if (WorkshopRules.CheckHire(workshops, guild, crawler, Index, ask, tick, tuning) != WorkshopRules.Check.Ok) continue;
                    var (trust, _) = GuildRules.Standing(crawler, tick);
                    _willing.Add((trust, crawler));
                }
            }
            // The most trusting first; the registry's order breaks a tie, so the list is the same every refresh
            _willing.Sort((a, b) => b.Trust.CompareTo(a.Trust));
            for (int i = 0; i < _willing.Count && _candidates.Count < MaxCandidates; i++)
            {
                var crawler = _willing[i].Crawler;
                var (trust, warmth) = GuildRules.Standing(crawler, tick);
                var id = crawler.AgentId;
                _candidates.Add(new StationCandidateRow
                {
                    CrawlerId = id,
                    Name = _context.FriendlyName(id, DossierModel.UnknownName),
                    HasPortrait = crawler.Look.IsComplete,
                    Portrait = crawler.Look.IsComplete ? PortraitKey.Of(id, crawler, _moods, tick) : default,
                    Standing = Invariant($"Trust {trust} · Warmth {warmth}"),
                    Ask = WorkshopRules.ReservationWage(crawler, tuning),
                });
            }
            CandidatesNote = _willing.Count == 0
                ? Invariant($"Nobody yet. A cautious resident takes station work once it trusts you (trust {tuning.HireTrust}, warmth {tuning.HireWarmth}): trade fairly and pay its bounties.")
                : _willing.Count == 1 ? "One resident would take the work." : Invariant($"{_willing.Count} residents would take the work.");
        }

        private void RefreshOrder(StationState state, string station)
        {
            HasOrder = state.HasOrder;
            CanOrder = Built && _context.Intervention != null && state.HeldOutput == 0;
            OrderNote = string.Empty;
            if (!HasOrder)
            {
                OrderText = _recipes.Count == 0 ? Invariant($"Nothing is made at the {station} yet.") : "No order.";
                return;
            }
            string made = ItemTexts.Of(state.Order).MerchantName.ToLowerInvariant();
            var recipe = RecipeBook.For(state.Order);
            int work = recipe != null ? Crafting.WorkTicks(recipe, state.Repair) : 0;
            int percent = work > 0 && state.InputsTaken ? state.Progress * 100 / work : 0;
            string doing = state.Repair ? "Mending" : "Making"; // a repair (W4, D59)
            OrderText = state.Remaining > 0 ? Invariant($"{doing} {made}: {state.Remaining} to go · {percent}%") : Invariant($"{doing} {made}: done");
            if (state.HeldOutput > 0) OrderNote = Invariant($"A finished {made} waits for room in the stall.");
            else if (!HasArtisan) OrderNote = Invariant($"Nobody works the {station}: hire an artisan.");
            else if (ArtisanOnBreak) OrderNote = Invariant($"{ArtisanName} is on a break.");
            else if (Shifts == 0) OrderNote = Invariant($"{ArtisanName} has no shifts: give it a block.");
            else if (!state.InputsTaken && recipe != null && LacksInputs(recipe, state.Repair, out string lacking)) OrderNote = lacking;
            else if (!ShiftBlocks.Covers(Shifts, Tick)) OrderNote = Capitalised(OffShiftText(Shifts)) + ".";
        }

        /// <summary>Whether the stall's stock lacks a make's inputs, and which: "Waiting for 3 iron ore in the stall (1 in stock)."</summary>
        private bool LacksInputs(Recipe recipe, bool repair, out string text)
        {
            text = string.Empty;
            if (!_context.TryGetStall(out var stall)) return false;
            if (repair && stall.WornOf(recipe.Output) < 1)
            {
                text = "Waiting for a worn piece to mend.";
                return true;
            }
            for (int i = 0; i < recipe.InputCount; i++)
            {
                var input = recipe.GetInput(i);
                int have = stall.StockOf(input.Item), need = Crafting.InputCount(input, repair);
                if (have >= need) continue;
                text = Invariant($"Waiting for {need} {ItemTexts.Of(input.Item).MerchantName.ToLowerInvariant()} in the stall ({have} in stock).");
                return true;
            }
            return false;
        }

        /// <summary>"off shift until 10:00": the start of the next block in <paramref name="shifts"/>.</summary>
        private string OffShiftText(byte shifts)
        {
            if (shifts == 0) return "no shifts";
            int now = ShiftBlocks.At(Tick);
            for (int step = 1; step <= ShiftBlocks.Count; step++)
            {
                int block = (now + step) % ShiftBlocks.Count;
                if ((shifts & (1 << block)) != 0) return Invariant($"off shift until {ShiftBlocks.StartHour(block):00}:00");
            }
            return "off shift";
        }

        private static string LongName(StationKind kind) => kind switch
        {
            StationKind.Forge => "Forge",
            StationKind.Still => "Alchemy Still",
            _ => "Station"
        };

        private static string DescriptionOf(StationKind kind) => kind switch
        {
            StationKind.Forge => "Iron ore into tools and gear, worked by an artisan you hire.",
            StationKind.Still => "Herbs into draughts, worked by an artisan you hire.",
            _ => "A station worked by an artisan you hire."
        };

        private static string Capitalised(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
