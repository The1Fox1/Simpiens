using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;
using UnityEngine;

namespace Simpiens.Core.Session.Layouts
{
    /// <summary>
    /// The legacy live geography as data: the 10 demonstration play pens (Epics 7, Beta, 8) plus the Outpost Commons
    /// supply-loop yard. Transcribed exactly from the former SwarmSpawner / FacilityZoneService code (Epic MAP Phase 1).
    /// Becomes the "Dev Map" canary session in Phase 2.
    /// </summary>
    public static class DevMapLayout
    {
        // Sector ids (bounty targets and scenarios refer to these)
        public const byte OutpostMarketSectorId = 11;
        public const byte OreRidgeSectorId = 12;
        public const byte HerbHollowSectorId = 13;

        // Regrowth durations in logical ticks
        public const uint FoodRegrowTicks = 450;
        public const uint HerbRegrowTicks = 600;
        public const uint OreRegrowTicks = 900;

        // Entity keys the pen scripts look up
        public const string Pen1ContestedBush = "pen1.contested";
        public const string Pen2ContestedBush = "pen2.contested";
        public const string Pen3ContestedBush = "pen3.contested";
        public const string Pen4Campfire = "pen4.campfire";
        public const string Pen5Campfire = "pen5.campfire";
        public const string Pen7Bush = "pen7.bush";
        public const string Pen8Campfire = "pen8.campfire";
        public const string Pen9Bush = "pen9.bush";
        public const string Pen10Bush = "pen10.bush";
        public const string OutpostStall = "outpost.stall";

        public static readonly Vector2 OutpostStallPosition = new Vector2(0f, 9f);

        // Pen grid: dividers between and around the 5 pens of each row
        private static readonly float[] DividerX = { -10f, -6f, -2f, 2f, 6f, 10f };

        private static MapDefinition _definition;

        /// <summary>Built once; definitions are immutable.</summary>
        public static MapDefinition Definition => _definition ??= Build();

        private static MapDefinition Build()
        {
            // The former 44x36 world with the ±20 play-area border: walkable x in [-20, 20], y in [-18, 18].
            // The camera frame keeps the former orthographic size 14.5 at the origin.
            var b = new MapDefinitionBuilder("DevMap", new Rect(-20f, -18f, 40f, 36f))
                .CameraFrame(new Rect(-12f, -14.5f, 24f, 29f));

            // --- Row 1 (Pens 1-5: Epics 7 & Beta), walls on y = ±3.5 ---
            b.WallRun(new Vector2(-10f, 3.5f), new Vector2(10f, 3.5f));
            b.WallRun(new Vector2(-10f, -3.5f), new Vector2(10f, -3.5f));
            for (int d = 0; d < DividerX.Length; d++)
            {
                b.WallRun(new Vector2(DividerX[d], -2.5f), new Vector2(DividerX[d], 2.5f));
            }

            // --- Row 2 (Pens 6-10: Epic 8), walls on y = -5.5 and -12.5 ---
            b.WallRun(new Vector2(-10f, -5.5f), new Vector2(10f, -5.5f));
            b.WallRun(new Vector2(-10f, -12.5f), new Vector2(10f, -12.5f));
            for (int d = 0; d < DividerX.Length; d++)
            {
                b.WallRun(new Vector2(DividerX[d], -11.5f), new Vector2(DividerX[d], -6.5f));
            }

            // --- Sectors (former facility zones; first match wins, so order matters) ---
            b.Sector(1, "Perimeter West", "Security perimeter, outer observation post, and quarantine gate.",
                new Rect(-10f, -3.5f, 4f, 7f), new Vector2(-8f, 0f), 2.5f);
            b.Sector(2, "Berry Orchard", "Hydroponics foraging rows and biological resource cultivation.",
                new Rect(-6f, -3.5f, 4f, 7f), new Vector2(-4f, 0f), 2.5f);
            b.Sector(3, "Central Corridor", "High-traffic central nexus connecting east and west wings.",
                new Rect(-2f, -3.5f, 4f, 7f), new Vector2(0f, 0f), 2.5f);
            b.Sector(4, "Common Hearth", "Campfire circle, relaxation lounge, and communal gossip site.",
                new Rect(2f, -3.5f, 4f, 7f), new Vector2(4f, 0f), 3.0f);
            b.Sector(5, "East Quarters", "Living bunks, private retreat chambers, and resting quarters.",
                new Rect(6f, -3.5f, 4f, 7f), new Vector2(8f, 0f), 3.0f);
            b.Sector(6, "Directive Sector", "Collective rallying point, military standard, and discipline drills.",
                new Rect(-10f, -12.5f, 4f, 7f), new Vector2(-8f, -9f), 2.0f, startsExplored: false);
            b.Sector(7, "Defiance Sector", "Starvation defiance, passive desertion, and grievance post.",
                new Rect(-6f, -12.5f, 4f, 7f), new Vector2(-4f, -9f), 2.0f, startsExplored: false);
            b.Sector(8, "Emergence Hearth", "Organic hearth emergence, campfire consensus, and settlement founding.",
                new Rect(-2f, -12.5f, 4f, 7f), new Vector2(0f, -9f), 2.0f, startsExplored: false);
            b.Sector(9, "Succession Arena", "Leadership succession contest, dominance trial, and power transition.",
                new Rect(2f, -12.5f, 4f, 7f), new Vector2(4f, -9f), 2.0f, startsExplored: false);
            b.Sector(10, "Schism Ward", "Crisis of legitimacy, factional fracture, and splinter bands.",
                new Rect(6f, -12.5f, 4f, 7f), new Vector2(8f, -9f), 2.0f, startsExplored: false);
            b.Sector(OutpostMarketSectorId, "Outpost Market", "Trade stall, bounty board, and the commons where crawlers gather between runs.",
                new Rect(-3f, 5f, 6f, 10f), new Vector2(0f, 9f), 3.0f);
            b.Sector(OreRidgeSectorId, "Ore Ridge", "Exposed iron veins along the western ridge; regrow slowly after mining.",
                new Rect(-12f, 5f, 7f, 10f), new Vector2(-8.5f, 10f), 2.5f, startsExplored: false);
            b.Sector(HerbHollowSectorId, "Herb Hollow", "Sheltered eastern hollow where medicinal herbs grow back between harvests.",
                new Rect(5f, 5f, 7f, 10f), new Vector2(8.5f, 10f), 2.5f, startsExplored: false);

            // --- Pen demonstration props ---
            b.Node(Pen1ContestedBush, new Vector2(-8f, 0f), ItemType.Ration, 1, FoodRegrowTicks);
            b.Node("pen1.forage", new Vector2(-8f, -2.2f), ItemType.Ration, 3, FoodRegrowTicks);
            b.Node(Pen2ContestedBush, new Vector2(-4f, 0f), ItemType.Ration, 1, FoodRegrowTicks);
            b.Node("pen2.forage", new Vector2(-4f, -2.2f), ItemType.Ration, 3, FoodRegrowTicks);
            b.Node(Pen3ContestedBush, new Vector2(0f, 0f), ItemType.Ration, 1, FoodRegrowTicks);
            b.Node(Pen7Bush, new Vector2(-4f, -11.0f), ItemType.Ration, 3, FoodRegrowTicks);
            b.Node(Pen9Bush, new Vector2(4f, -11.0f), ItemType.Ration, 5, FoodRegrowTicks);
            b.Node(Pen10Bush, new Vector2(8f, -11.0f), ItemType.Ration, 5, FoodRegrowTicks);
            b.Poi(Pen4Campfire, PoiKind.Hearth, new Vector2(4f, 0f), 3.0f);
            b.Poi(Pen5Campfire, PoiKind.Hearth, new Vector2(8f, 0f), 3.0f);
            b.Poi(Pen8Campfire, PoiKind.Hearth, new Vector2(0f, -9.0f), 3.0f);

            // --- Outpost Commons (open yard north of the pens) ---
            b.Node("ore.1", new Vector2(-9f, 12f), ItemType.IronOre, 3, OreRegrowTicks);
            b.Node("ore.2", new Vector2(-8f, 9.5f), ItemType.IronOre, 3, OreRegrowTicks);
            b.Node("ore.3", new Vector2(-10f, 8f), ItemType.IronOre, 3, OreRegrowTicks);
            b.Node("herb.1", new Vector2(9f, 12f), ItemType.MedicinalHerb, 3, HerbRegrowTicks);
            b.Node("herb.2", new Vector2(8f, 9.5f), ItemType.MedicinalHerb, 3, HerbRegrowTicks);
            b.Node("herb.3", new Vector2(10f, 8f), ItemType.MedicinalHerb, 3, HerbRegrowTicks);
            b.Node("food.1", new Vector2(-2f, 6.5f), ItemType.Ration, 3, FoodRegrowTicks);
            b.Node("food.2", new Vector2(2f, 6.5f), ItemType.Ration, 3, FoodRegrowTicks);
            b.Poi(OutpostStall, PoiKind.Stall, OutpostStallPosition, 3.0f);

            // --- Labels ---
            b.Label("1. Fear Yield", new Vector2(-8.0f, 4.2f), new Color(1f, 0.75f, 0.2f));
            b.Label("2. Respect Defer", new Vector2(-4.0f, 4.2f), new Color(0.35f, 0.8f, 1f));
            b.Label("3. Rivalry Contest", new Vector2(0.0f, 4.2f), new Color(1f, 0.4f, 0.4f));
            b.Label("4. Altruism & Hearth", new Vector2(4.0f, 4.2f), new Color(0.3f, 0.95f, 0.5f));
            b.Label("5. Selfish Peer", new Vector2(8.0f, 4.2f), new Color(0.75f, 0.8f, 0.85f));
            b.Label("6. Directive Rally", new Vector2(-8.0f, -4.8f), new Color(1f, 0.85f, 0.25f));
            b.Label("7. Passive Defiance", new Vector2(-4.0f, -4.8f), new Color(0.95f, 0.3f, 0.3f));
            b.Label("8. Hearth Emergence", new Vector2(0.0f, -4.8f), new Color(1f, 0.6f, 0.2f));
            b.Label("9. Dominance Trial", new Vector2(4.0f, -4.8f), new Color(0.8f, 0.4f, 0.9f));
            b.Label("10. Factional Schism", new Vector2(8.0f, -4.8f), new Color(0.2f, 0.85f, 0.85f));
            b.Label("Ore Ridge", new Vector2(-8.5f, 14.5f), new Color(0.95f, 0.55f, 0.2f));
            b.Label("Outpost Market", new Vector2(0f, 14.5f), new Color(0.95f, 0.85f, 0.4f));
            b.Label("Herb Hollow", new Vector2(8.5f, 14.5f), new Color(0.45f, 0.9f, 0.8f));

            return b.Build();
        }
    }
}
