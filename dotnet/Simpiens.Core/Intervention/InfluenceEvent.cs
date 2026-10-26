using System;
using Simpiens.Simulation.Economy;
namespace Simpiens.Intervention
{
    public enum InfluenceCommandType : byte
    {
        None = 0,
        PostBounty = 1,
        CancelBounty = 2,
        UpdateStallPrices = 3,
        /// <summary>The player calms a crawler: its frustration drops to zero and a tantrum or collapse ends (Epic CLIENT M2.2).</summary>
        CalmCrawler = 4
    }

    public readonly struct PostBountyCommand
    {
        public readonly ushort BountyId;
        public readonly BountyType Type;
        public readonly byte SectorId;
        public readonly ItemType ItemType;
        public readonly byte Quantity;
        public readonly ushort RewardGold;
        public readonly uint DurationTicks;
        public readonly Vec2 TargetLocation;
        public readonly EntityId CreatorStallId;

        public PostBountyCommand(
            ushort bountyId,
            BountyType type,
            byte sectorId,
            ItemType itemType,
            byte quantity,
            ushort rewardGold,
            uint durationTicks = 1000,
            Vec2 targetLocation = default,
            EntityId creatorStallId = default)
        {
            BountyId = bountyId;
            Type = type;
            SectorId = sectorId;
            ItemType = itemType;
            Quantity = quantity;
            RewardGold = rewardGold;
            DurationTicks = durationTicks > 0 ? durationTicks : 1000;
            TargetLocation = targetLocation;
            CreatorStallId = creatorStallId;
        }
    }

    public readonly struct CancelBountyCommand
    {
        public readonly ushort BountyId;

        public CancelBountyCommand(ushort bountyId)
        {
            BountyId = bountyId;
        }
    }

    /// <summary>One price in a price command: the item and its new price (0 keeps the current one).</summary>
    public readonly struct StallPrice
    {
        public readonly ItemType Item;
        public readonly ushort Price;

        public StallPrice(ItemType item, ushort price)
        {
            Item = item;
            Price = price;
        }
    }

    [System.Runtime.CompilerServices.InlineArray(UpdateStallPricesCommand.Capacity)]
    public struct StallPrices
    {
        private StallPrice _entry;
    }

    /// <summary>
    /// The player sets a stall's prices (item system I5): up to <see cref="Capacity"/> (item, price) pairs, applied
    /// together on the next tick in their order. A price of 0 keeps the current one, and an item the stall doesn't trade
    /// is ignored. Zero-allocation: the pairs are a fixed table.
    /// </summary>
    public readonly struct UpdateStallPricesCommand
    {
        public const int Capacity = 8;

        public readonly EntityId StallId;
        public readonly int Count;
        private readonly StallPrices _prices;

        public UpdateStallPricesCommand(EntityId stallId) : this(stallId, 0, default)
        {
        }

        private UpdateStallPricesCommand(EntityId stallId, int count, in StallPrices prices)
        {
            StallId = stallId;
            Count = count;
            _prices = prices;
        }

        public StallPrice this[int index] => _prices[index];

        /// <summary>The same command setting one more price (replacing an earlier price for the same item).</summary>
        public UpdateStallPricesCommand With(ItemType item, ushort price)
        {
            var prices = _prices;
            for (int i = 0; i < Count; i++)
            {
                if (prices[i].Item == item)
                {
                    prices[i] = new StallPrice(item, price);
                    return new UpdateStallPricesCommand(StallId, Count, prices);
                }
            }
            if (Count >= Capacity) throw new System.InvalidOperationException($"A price command sets at most {Capacity} prices.");
            prices[Count] = new StallPrice(item, price);
            return new UpdateStallPricesCommand(StallId, Count + 1, prices);
        }
    }

    /// <summary>The player calms one crawler (the dossier's Calm Frustration).</summary>
    public readonly struct CalmCrawlerCommand
    {
        public readonly EntityId CrawlerId;

        public CalmCrawlerCommand(EntityId crawlerId)
        {
            CrawlerId = crawlerId;
        }
    }

    /// <summary>
    /// A strongly-typed data object representing a player input or external intervention.
    /// Operates as a zero-allocation command struct applied at fixed simulation tick boundaries.
    /// </summary>
    public readonly struct InfluenceEvent
    {
        public readonly string SourceId;
        public readonly InfluenceCommandType CommandType;
        public readonly PostBountyCommand PostBounty;
        public readonly CancelBountyCommand CancelBounty;
        public readonly UpdateStallPricesCommand UpdateStallPrices;
        public readonly CalmCrawlerCommand CalmCrawler;

        public string LegacyCommandName => CommandType switch
        {
            InfluenceCommandType.PostBounty => "PostBounty",
            InfluenceCommandType.CancelBounty => "CancelBounty",
            InfluenceCommandType.UpdateStallPrices => "UpdateStallPrices",
            InfluenceCommandType.CalmCrawler => "CalmCrawler",
            _ => string.Empty
        };

        public InfluenceEvent(
            string sourceId,
            InfluenceCommandType commandType,
            in PostBountyCommand postBounty = default,
            in CancelBountyCommand cancelBounty = default,
            in UpdateStallPricesCommand updateStallPrices = default,
            in CalmCrawlerCommand calmCrawler = default)
        {
            SourceId = sourceId;
            CommandType = commandType;
            PostBounty = postBounty;
            CancelBounty = cancelBounty;
            UpdateStallPrices = updateStallPrices;
            CalmCrawler = calmCrawler;
        }

        public static InfluenceEvent CreatePostBounty(string sourceId, in PostBountyCommand cmd)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.PostBounty, postBounty: cmd);
        }

        public static InfluenceEvent CreateCancelBounty(string sourceId, ushort bountyId)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.CancelBounty, cancelBounty: new CancelBountyCommand(bountyId));
        }

        public static InfluenceEvent CreateUpdateStallPrices(string sourceId, in UpdateStallPricesCommand cmd)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.UpdateStallPrices, updateStallPrices: cmd);
        }

        public static InfluenceEvent CreateCalmCrawler(string sourceId, EntityId crawlerId)
        {
            return new InfluenceEvent(sourceId, InfluenceCommandType.CalmCrawler, calmCrawler: new CalmCrawlerCommand(crawlerId));
        }

        /// <summary>
        /// Legacy constructor for backward compatibility with string-based tests and tools.
        /// Parses CSV payloads at instantiation time rather than inside the simulation hot loop.
        /// </summary>
        public InfluenceEvent(string sourceId, string commandType, string payload)
        {
            SourceId = sourceId;
            PostBounty = default;
            CancelBounty = default;
            UpdateStallPrices = default;
            CalmCrawler = default;

            if (string.Equals(commandType, "PostBounty", StringComparison.OrdinalIgnoreCase))
            {
                CommandType = InfluenceCommandType.PostBounty;
                if (!string.IsNullOrEmpty(payload))
                {
                    string[] parts = payload.Split(new char[] { ',', ':', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 6)
                    {
                        ushort.TryParse(parts[0].Trim(), out ushort bId);
                        Enum.TryParse<BountyType>(parts[1].Trim(), true, out var bType);
                        byte.TryParse(parts[2].Trim(), out byte sId);
                        Enum.TryParse<ItemType>(parts[3].Trim(), true, out var iType);
                        byte.TryParse(parts[4].Trim(), out byte qty);
                        ushort.TryParse(parts[5].Trim(), out ushort reward);

                        uint duration = 1000;
                        if (parts.Length > 6) uint.TryParse(parts[6].Trim(), out duration);

                        Vec2 loc = Vec2.Zero;
                        if (parts.Length > 8)
                        {
                            float.TryParse(parts[7].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
                            float.TryParse(parts[8].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
                            loc = new Vec2(x, y);
                        }

                        PostBounty = new PostBountyCommand(bId, bType, sId, iType, qty, reward, duration, loc);
                    }
                }
            }
            else if (string.Equals(commandType, "CancelBounty", StringComparison.OrdinalIgnoreCase))
            {
                CommandType = InfluenceCommandType.CancelBounty;
                if (!string.IsNullOrEmpty(payload) && ushort.TryParse(payload.Trim(), out ushort bId))
                {
                    CancelBounty = new CancelBountyCommand(bId);
                }
            }
            else if (string.Equals(commandType, "CalmCrawler", StringComparison.OrdinalIgnoreCase))
            {
                CommandType = InfluenceCommandType.CalmCrawler;
                if (!string.IsNullOrEmpty(payload) && EntityId.TryParse(payload.Trim(), out var crawlerId))
                {
                    CalmCrawler = new CalmCrawlerCommand(crawlerId);
                }
            }
            else
            {
                CommandType = InfluenceCommandType.None;
            }
        }
    }
}
