using System;
using Simpiens.Entities;
using Simpiens.Simulation.Items;

namespace Simpiens.Presentation.Art
{
    /// <summary>An item's pictures (graphics V3.6): its menu icon, its map sprite, and the marker over a crawler carrying it.</summary>
    public enum ItemPicture : byte
    {
        Icon = 0,
        Map = 1,
        Carried = 2,
    }

    /// <summary>
    /// The art keys code names (graphics V1): stable strings, resolved to <see cref="ArtId"/>s once per catalogue. A
    /// resource node's key is <c>node/</c> and its item's art key (<c>node/iron-ore</c>); a crawler's, <c>crawler/</c> and its state.
    /// </summary>
    public static class ArtKeys
    {
        public const string TileFloor = "tile/floor";
        public const string TileRock = "tile/rock";
        public const string TileWall = "tile/wall";
        /// <summary>A wall cut down so it hides nothing behind it (M3 design D7); top-down's is its wall.</summary>
        public const string TileWallLow = "tile/wall-low";
        public const string PropHearth = "prop/hearth";
        public const string PropBunk = "prop/bunk";
        /// <summary>The trade stall, with its goods on display (graphics V5).</summary>
        public const string PropStall = "prop/stall";

        /// <summary>A body's cairn (Epic 10 Part 3, K5.8b).</summary>
        public const string PropCairn = "prop/cairn";
        /// <summary>The trade caravan walking the map (Epic 10 Part 2, the stretch, D70).</summary>
        public const string PropCaravan = "prop/caravan";

        /// <summary>The workshop row's stations (Epic 10 Part 2, W1.6a): each built, and a station's empty plot.</summary>
        public const string PropForge = "prop/forge";
        public const string PropStill = "prop/still";
        public const string PropStationPlot = "prop/station-plot";
        /// <summary>The guildhall's drill yard (Epic 11, S3.4): a straw post on its stone, a weapon stand beside it.</summary>
        public const string PropDrillYard = "prop/drill-yard";

        /// <summary>A station's art: its own once built, the plot before.</summary>
        public static string Station(StationKind kind, bool built) => !built ? PropStationPlot : kind switch
        {
            StationKind.Forge => PropForge,
            StationKind.Still => PropStill,
            StationKind.DrillYard => PropDrillYard,
            _ => PropStationPlot
        };

        public const string SelectionRing = "fx/selection-ring";

        /// <summary>A hazard's animation (Epic 10 Part 2, the stretch, D69): foul air's puffs, falling rock's pebbles, the flood's ripples.</summary>
        public const string FxFoulAir = "fx/hazard/foul-air";
        public const string FxFallingRock = "fx/hazard/falling-rock";
        public const string FxFlood = "fx/hazard/flood";

        /// <summary>The animation a hazard of <paramref name="kind"/> plays; none for no hazard.</summary>
        public static string Hazard(Simpiens.Simulation.World.HazardKind kind) => kind switch
        {
            Simpiens.Simulation.World.HazardKind.FoulAir => FxFoulAir,
            Simpiens.Simulation.World.HazardKind.FallingRock => FxFallingRock,
            Simpiens.Simulation.World.HazardKind.Flooding => FxFlood,
            _ => null
        };
        /// <summary>A shot in flight (Epic 10 Part 3, K8.4a): a crawler's stone or bolt, and a spinner's spit.</summary>
        public const string FxShot = "fx/shot";
        public const string FxSpit = "fx/spit";
        /// <summary>A boss's health bar (Epic 10 Part 3, K10.5b): the row's size is the bar's for a creature a crawler's size.</summary>
        public const string FxBar = "fx/bar";
        /// <summary>The descent gate, sealed and breached (Epic 10 Part 3, K10.5c).</summary>
        public const string PropGate = "prop/gate", PropGateOpen = "prop/gate-open";

        /// <summary>A soft shadow on the floor under a crawler; a catalogue without one draws none.</summary>
        public const string CrawlerShadow = "fx/crawler-shadow";
        public const string NodePrefix = "node/";
        /// <summary>What a thing without art of its own is drawn with (<see cref="MissingArt"/>); every catalogue has it.</summary>
        public const string Missing = "fx/missing";

        private static readonly string[] _crawlers = BuildCrawlers();

        /// <summary>A crawler in <paramref name="state"/>: <c>crawler/idle</c>, <c>crawler/walking</c>, …</summary>
        public static string Crawler(AgentVisualState state) => _crawlers[(int)state];

        /// <summary>A crawler in <paramref name="state"/> wearing the item <paramref name="itemArtKey"/>: <c>crawler/walking/hazard-gear</c> (setup only: builds a string).</summary>
        public static string CrawlerWearing(AgentVisualState state, string itemArtKey) => _crawlers[(int)state] + "/" + itemArtKey;

        /// <summary>The tile of <paramref name="kind"/>.</summary>
        public static string Tile(TileKind kind) => kind switch
        {
            TileKind.Floor => TileFloor,
            TileKind.Rock => TileRock,
            _ => TileWall,
        };

        public static string Node(string itemArtKey) => NodePrefix + itemArtKey;

        public const string CreaturePrefix = "creature/";

        /// <summary>A creature of a kind (Epic 10 Part 3, K2.5), by its kind's art key: <c>creature/burrow-rat</c> (setup only: builds a string).</summary>
        public static string Creature(string creatureArtKey) => CreaturePrefix + creatureArtKey;

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
