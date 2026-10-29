using System.Collections.Generic;
using Simpiens.Intervention;
using Simpiens.Simulation;
using Simpiens.Simulation.Economy;
using static System.FormattableString;

namespace Simpiens.Presentation.Dashboard
{
    /// <summary>A contract on the board, as Unity's bounty card showed it.</summary>
    public readonly struct BountyRow
    {
        public readonly ushort BountyId;
        public readonly string Title;
        public readonly BountyStatus Status;
        public readonly string StatusText;
        public readonly string Details;
        public readonly string Reward;
        public readonly bool CanCancel;

        public BountyRow(ushort bountyId, string title, BountyStatus status, string details, string reward, bool canCancel)
        {
            BountyId = bountyId;
            Title = title;
            Status = status;
            StatusText = status.ToString().ToUpperInvariant();
            Details = details;
            Reward = reward;
            CanCancel = canCancel;
        }
    }

    /// <summary>
    /// The public bounty board (Unity's bounty window): the active contracts, and the form that posts one. Scout offers
    /// only uncharted sectors; Gather offers every sector, marking uncharted ones. The board checks what the simulation
    /// would refuse (<see cref="BountyRules"/>, the treasury) so the player gets the reason. Post and Cancel queue typed
    /// commands; nothing changes here until the simulation applies them.
    /// </summary>
    public sealed class BountyBoardModel
    {
        public const string AllChartedText = "Every sector is charted: there is nothing left to scout.";
        public const string AwaitsChartSuffix = " (claimable once charted)";
        public const int MinQuantity = 1, MaxQuantity = 10, DefaultQuantity = 1;
        public const int MinReward = 10, MaxReward = 100, DefaultReward = 25;
        public const int MinDuration = 500, MaxDuration = 3000, DefaultDuration = 1000;

        private readonly DashboardContext _context;
        private readonly List<int> _sectorIds = new List<int>(16);
        private readonly List<string> _sectorLabels = new List<string>(16);
        private readonly List<BountyRow> _rows = new List<BountyRow>(16);
        private BountyType _choicesType = BountyType.None;
        private ulong _choicesMask;
        private int _choicesZoneCount = -1;
        private BountyType _type = BountyType.GatherResource;

        internal BountyBoardModel(DashboardContext context) => _context = context;

        public bool IsOpen { get; private set; }

        public BountyType Type => _type;

        /// <summary>
        /// Picks the contract's type: Scout, or Gather for anything else. The sector choices are rebuilt for it (Scout
        /// offers only uncharted sectors), which can change the picked sector and the feedback (review S9: the side
        /// effect is a call, not a setter).
        /// </summary>
        public void SetType(BountyType type)
        {
            _type = type == BountyType.ScoutSector ? BountyType.ScoutSector : BountyType.GatherResource;
            RebuildSectorChoices();
        }

        /// <summary>Item and quantity only mean something for Gather (Unity hid them for Scout).</summary>
        public bool ShowsItem => _type == BountyType.GatherResource;

        public ItemType Item { get; set; } = GatherItems[0];

        /// <summary>
        /// What a Gather bounty can ask for: the resources that aren't hazardous (iron ore, medicinal herbs), in id order
        /// (item system I6). Hazardous salvage (glowstone) is left to the crawlers who venture.
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<ItemType> GatherItems => _gatherItems;

        private static readonly ItemType[] _gatherItems = BuildGatherItems();

        private static ItemType[] BuildGatherItems()
        {
            var items = new System.Collections.Generic.List<ItemType>();
            foreach (var item in Simpiens.Simulation.Items.ItemCatalog.InCategory(Simpiens.Simulation.Items.ItemCategory.Resource))
            {
                if (!Simpiens.Simulation.Items.ItemCatalog.Is(item, Simpiens.Simulation.Items.ItemCategory.Hazardous)) items.Add(item);
            }
            return items.ToArray();
        }
        public int Quantity { get; set; } = DefaultQuantity;
        public int Reward { get; set; } = DefaultReward;
        public int Duration { get; set; } = DefaultDuration;

        /// <summary>The sector picker's ids and labels, in map order, for the current type.</summary>
        public IReadOnlyList<int> SectorChoiceIds => _sectorIds;
        public IReadOnlyList<string> SectorChoiceLabels => _sectorLabels;

        /// <summary>The picked sector's index in the choices, or -1.</summary>
        public int SelectedSectorIndex { get; set; } = -1;

        /// <summary>"Charted" or "Uncharted" for the picked sector.</summary>
        public string SectorState
        {
            get
            {
                if (SelectedSectorIndex < 0 || SelectedSectorIndex >= _sectorIds.Count || !_context.Zones.TryGetZone(_sectorIds[SelectedSectorIndex], out var zone)) return string.Empty;
                return zone.IsExplored ? "Charted" : "Uncharted";
            }
        }

        public bool CanPost => _sectorIds.Count > 0;

        public IReadOnlyList<BountyRow> Contracts => _rows;
        public string CountBadge { get; private set; } = "0 ACTIVE";

        public Feedback Feedback { get; private set; } = Feedback.None;

        public static string QuantityText(int quantity) => Invariant($"{quantity} Unit{(quantity > 1 ? "s" : "")}");
        public static string RewardText(int gold) => Invariant($"{gold} Gold");
        public static string DurationText(int ticks) => Invariant($"{ticks} Ticks");

        public static string FormatType(BountyType type) => type switch
        {
            BountyType.GatherResource => "Gather Salvage",
            BountyType.ScoutSector => "Scout Sector",
            _ => "Directive"
        };

        public void Open()
        {
            IsOpen = true;
            RebuildSectorChoices(force: true);
            Refresh();
        }

        public void Close() => IsOpen = false;

        /// <summary>Selects a sector in the picker; false when the current type doesn't offer it.</summary>
        public bool SelectSector(int sectorId)
        {
            RebuildSectorChoices();
            int index = _sectorIds.IndexOf(sectorId);
            if (index < 0) return false;
            SelectedSectorIndex = index;
            return true;
        }

        public void Refresh()
        {
            int active = _context.Simulation?.ActiveBounties?.Count ?? 0;
            CountBadge = Invariant($"{active} ACTIVE");
            if (!IsOpen) return;

            RebuildSectorChoices(); // a scout's report may have charted a sector while the board is open
            _rows.Clear();
            var bounties = _context.Simulation?.ActiveBounties;
            if (bounties == null) return;
            for (int i = 0; i < bounties.Count; i++) _rows.Add(ToRow(bounties[i]));
        }

        private BountyRow ToRow(in BountyContract bounty)
        {
            string sectorName = _context.Zones.TryGetZone(bounty.TargetSectorId, out var zone) ? zone.Name : Invariant($"Sector {bounty.TargetSectorId}");
            string task = bounty.Type == BountyType.GatherResource
                ? Invariant($"{bounty.TargetQuantity}x {bounty.TargetItemType} from {sectorName}")
                : $"Explore & Scout {sectorName}";
            bool canCancel = bounty.Status == BountyStatus.Open || bounty.Status == BountyStatus.Claimed;
            return new BountyRow(bounty.BountyId, Invariant($"#{bounty.BountyId} - {FormatType(bounty.Type)}"), bounty.Status,
                Invariant($"{task} | Expires: {bounty.ExpirationTick} ticks"), RewardText(bounty.RewardGold), canCancel);
        }

        /// <summary>
        /// Fills the sector picker from the loaded map, only when the map, the type or the charted sectors changed,
        /// keeping the current choice while it is still offered.
        /// </summary>
        private void RebuildSectorChoices(bool force = false)
        {
            var zones = _context.Zones.GetAllZones();
            ulong mask = 0UL;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].IsExplored && zones[i].ZoneId < 64) mask |= 1UL << zones[i].ZoneId;
            }
            if (!force && _type == _choicesType && mask == _choicesMask && zones.Count == _choicesZoneCount) return;
            _choicesType = _type;
            _choicesMask = mask;
            _choicesZoneCount = zones.Count;

            int previous = SelectedSectorIndex >= 0 && SelectedSectorIndex < _sectorIds.Count ? _sectorIds[SelectedSectorIndex] : -1;
            _sectorIds.Clear();
            _sectorLabels.Clear();
            for (int i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                if (_type == BountyType.ScoutSector && zone.IsExplored) continue;
                _sectorIds.Add(zone.ZoneId);
                _sectorLabels.Add(zone.IsExplored || _type == BountyType.ScoutSector ? zone.Name : zone.Name + AwaitsChartSuffix);
            }
            int keep = previous >= 0 ? _sectorIds.IndexOf(previous) : -1;
            SelectedSectorIndex = _sectorIds.Count == 0 ? -1 : keep >= 0 ? keep : 0;
            if (!CanPost && _type == BountyType.ScoutSector) Feedback = new Feedback(AllChartedText, FeedbackTone.Error);
        }

        /// <summary>Posts the form's contract: checked here first, then queued as a <see cref="PostBountyCommand"/>.</summary>
        public void Post()
        {
            RebuildSectorChoices();
            int choice = SelectedSectorIndex;
            if (choice < 0 || choice >= _sectorIds.Count)
            {
                Feedback = new Feedback(_type == BountyType.ScoutSector && _sectorIds.Count == 0 ? AllChartedText : "Pick a target sector.", FeedbackTone.Error);
                return;
            }
            byte sectorId = (byte)_sectorIds[choice];

            // The simulation enforces the same rule; checking here gives the player the reason
            var target = BountyRules.CheckTarget(_type, sectorId, _context.Sectors);
            if (target != BountyRules.TargetCheck.Ok)
            {
                Feedback = new Feedback(BountyRules.Describe(target, sectorId, _context.Sectors), FeedbackTone.Error);
                return;
            }

            var item = _type == BountyType.ScoutSector ? ItemType.None : Item;
            byte quantity = (byte)Clamp(Quantity, MinQuantity, MaxQuantity);
            ushort reward = (ushort)Clamp(Reward, MinReward, MaxReward);
            uint duration = (uint)Clamp(Duration, MinDuration, MaxDuration);

            EntityId stallId = default;
            if (_context.TryGetStall(out var stall))
            {
                stallId = stall.StallEntityId;
                if (stall.TreasuryGold < reward)
                {
                    Feedback = new Feedback(Invariant($"Insufficient treasury gold ({stall.TreasuryGold}g available, need {reward}g)."), FeedbackTone.Error);
                    return;
                }
            }

            if (_context.Intervention == null)
            {
                Feedback = new Feedback("No intervention queue available.", FeedbackTone.Error);
                return;
            }
            var command = new PostBountyCommand(0, _type, sectorId, item, quantity, reward, duration, Vec2.Zero, stallId);
            _context.Intervention.QueueInfluence(InfluenceEvent.CreatePostBounty("Player", command));
            Feedback = new Feedback(Invariant($"Posted {FormatType(_type)} contract for {reward}g reward."), FeedbackTone.Ok);
        }

        public void Cancel(ushort bountyId)
        {
            if (_context.Intervention == null) return;
            _context.Intervention.QueueInfluence(InfluenceEvent.CreateCancelBounty("Player", bountyId));
            Feedback = new Feedback(Invariant($"Dispatched cancellation for contract #{bountyId}."), FeedbackTone.Notice);
        }

        /// <summary>A new session: the old map's choices mean nothing.</summary>
        internal void Reset()
        {
            Feedback = Feedback.None;
            SelectedSectorIndex = -1;
            RebuildSectorChoices(force: true);
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
