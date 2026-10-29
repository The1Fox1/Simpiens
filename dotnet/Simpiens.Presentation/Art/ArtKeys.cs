using System;
using Simpiens.Entities;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// The art keys code names (graphics V1): stable strings, resolved to <see cref="ArtId"/>s once per catalogue. A
    /// resource node's key is <c>node/</c> and its item's art key (<c>node/iron-ore</c>); a crawler's, <c>crawler/</c> and its state.
    /// </summary>
    /// <summary>An item's pictures (graphics V3.6): its menu icon, its map sprite, and the marker over a crawler carrying it.</summary>
    public enum ItemPicture : byte
    {
        Icon = 0,
        Map = 1,
        Carried = 2,
    }

    public static class ArtKeys
    {
        public const string TileFloor = "tile/floor";
        public const string TileRock = "tile/rock";
        public const string TileWall = "tile/wall";
        /// <summary>A wall cut down so it hides nothing behind it (M3 design D7); top-down's is its wall.</summary>
        public const string TileWallLow = "tile/wall-low";
        public const string PropHearth = "prop/hearth";
        public const string PropBunk = "prop/bunk";
        public const string SelectionRing = "fx/selection-ring";
        /// <summary>A soft shadow on the floor under a crawler; a catalogue without one draws none.</summary>
        public const string CrawlerShadow = "fx/crawler-shadow";
        public const string NodePrefix = "node/";
        /// <summary>What a thing without art of its own is drawn with (<see cref="MissingArt"/>); every catalogue has it.</summary>
        public const string Missing = "fx/missing";

        private static readonly string[] _crawlers = BuildCrawlers();

        /// <summary>A crawler in <paramref name="state"/>: <c>crawler/idle</c>, <c>crawler/walking</c>, …</summary>
        public static string Crawler(AgentVisualState state) => _crawlers[(int)state];

        /// <summary>The tile of <paramref name="kind"/>.</summary>
        public static string Tile(TileKind kind) => kind switch
        {
            TileKind.Floor => TileFloor,
            TileKind.Rock => TileRock,
            _ => TileWall,
        };

        public static string Node(string itemArtKey) => NodePrefix + itemArtKey;

        public const string ItemPrefix = "item/";

        /// <summary>An item's picture: <c>item/iron-ore/icon</c>, <c>item/iron-ore/map</c>, <c>item/iron-ore/carried</c>.</summary>
        public static string Item(string itemArtKey, ItemPicture picture) => ItemPrefix + itemArtKey + "/" + PictureName(picture);

        public static string PictureName(ItemPicture picture) => picture switch
        {
            ItemPicture.Icon => "icon",
            ItemPicture.Map => "map",
            _ => "carried",
        };

        private static string[] BuildCrawlers()
        {
            var states = (AgentVisualState[])Enum.GetValues(typeof(AgentVisualState));
            var keys = new string[states.Length];
            foreach (var state in states) keys[(int)state] = "crawler/" + state.ToString().ToLowerInvariant();
            return keys;
        }
    }
}
