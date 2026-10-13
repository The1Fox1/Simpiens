using Simpiens.Simulation.Economy;
using Simpiens.Simulation.World;
using UnityEngine;

namespace Simpiens.Testing.Sessions
{
    /// <summary>
    /// The Dev Map: the 10 demonstration play pens (Epics 7, Beta, 8), test-only since Epic MAP Phase 2. Walls,
    /// sectors and props are transcribed cell for cell from the former live pen arena (parity proven in Phase 1);
    /// the Outpost Commons yard became the open-yard test fixture CommonsYardFixture.
    /// </summary>
    public static class DevMapLayout
    {
        // Regrowth durations in logical ticks
        public const uint FoodRegrowTicks = 450;

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

        // Pen grid: dividers between and around the 5 pens of each row
        private static readonly float[] DividerX = { -10f, -6f, -2f, 2f, 6f, 10f };

        private static MapDefinition _definition;

        /// <summary>Built once; definitions are immutable.</summary>
        public static MapDefinition Definition => _definition ??= Build();

        private static MapDefinition Build()
        {
            // The two pen rows (walls span x -10.55..10.55, y -13.05..4.05) plus a margin. The pens are closed, so the
            // bounds only frame the camera and size the partition.
            var arena = new Rect(-12f, -14.5f, 24f, 20f);
            var b = new MapDefinitionBuilder("DevMap", arena).CameraFrame(arena);

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
            // Every pen starts charted: the Dev Map has no scouts, and under the fog of war (Epic MAP Phase 3) an
            // uncharted pen would hide its own props from the agents inside it
            b.Sector(6, "Directive Sector", "Collective rallying point, military standard, and discipline drills.",
                new Rect(-10f, -12.5f, 4f, 7f), new Vector2(-8f, -9f), 2.0f);
            b.Sector(7, "Defiance Sector", "Starvation defiance, passive desertion, and grievance post.",
                new Rect(-6f, -12.5f, 4f, 7f), new Vector2(-4f, -9f), 2.0f);
            b.Sector(8, "Emergence Hearth", "Organic hearth emergence, campfire consensus, and settlement founding.",
                new Rect(-2f, -12.5f, 4f, 7f), new Vector2(0f, -9f), 2.0f);
            b.Sector(9, "Succession Arena", "Leadership succession contest, dominance trial, and power transition.",
                new Rect(2f, -12.5f, 4f, 7f), new Vector2(4f, -9f), 2.0f);
            b.Sector(10, "Schism Ward", "Crisis of legitimacy, factional fracture, and splinter bands.",
                new Rect(6f, -12.5f, 4f, 7f), new Vector2(8f, -9f), 2.0f);

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

            return b.Build();
        }
    }
}
