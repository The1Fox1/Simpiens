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

    public readonly struct UpdateStallPricesCommand
    {
        public readonly EntityId StallId;
        public readonly ushort RationSellPrice;
        public readonly ushort OreBuyPrice;
        public readonly ushort HerbBuyPrice;

        public UpdateStallPricesCommand(EntityId stallId, ushort rationSellPrice, ushort oreBuyPrice, ushort herbBuyPrice)
        {
            StallId = stallId;
            RationSellPrice = rationSellPrice;
            OreBuyPrice = oreBuyPrice;
            HerbBuyPrice = herbBuyPrice;
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
            else if (string.Equals(commandType, "UpdateStallPrices", StringComparison.OrdinalIgnoreCase))
            {
                CommandType = InfluenceCommandType.UpdateStallPrices;
                if (!string.IsNullOrEmpty(payload))
                {
                    string[] parts = payload.Split(new char[] { ',', ':', ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        EntityId sId = default;
                        int offset = 0;
                        if (parts.Length >= 4)
                        {
                            EntityId.TryParse(parts[0].Trim(), out sId);
                            offset = 1;
                        }

                        ushort.TryParse(parts[offset].Trim(), out ushort ration);
                        ushort.TryParse(parts[offset + 1].Trim(), out ushort ore);
                        ushort.TryParse(parts[offset + 2].Trim(), out ushort herb);

                        UpdateStallPrices = new UpdateStallPricesCommand(sId, ration, ore, herb);
                    }
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
