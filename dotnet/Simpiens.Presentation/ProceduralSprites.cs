using System;
using System.Collections.Generic;
using Simpiens.Entities;
using Simpiens.Simulation.Economy;

namespace Simpiens.Presentation
{
    /// <summary>
    /// The game's generated 16x16 pixel art (Epic CLIENT M0): every crawler state, the props and the map tiles, built
    /// once from the ASCII patterns and palettes below. A port of Unity's <c>AgentSpriteLibrary</c>, pixel for pixel,
    /// with pixels as plain data (<see cref="PixelSprite"/>) instead of Unity sprites.
    /// </summary>
    public static class ProceduralSprites
    {
        private static readonly List<PixelSprite> _all = new List<PixelSprite>(48);
        private static readonly Dictionary<string, PixelSprite> _byName = new Dictionary<string, PixelSprite>(48);
        private static readonly PixelSprite[,] _agentSprites = new PixelSprite[12, 2];
        private static readonly PixelSprite _resourceSprite;
        private static readonly PixelSprite _oreVeinSprite;
        private static readonly PixelSprite _herbPatchSprite;
        private static readonly PixelSprite _glowstoneSprite;
        private static readonly PixelSprite _wallSprite;
        private static readonly PixelSprite[] _campfireSprites = new PixelSprite[2];

        /// <summary>Every generated sprite, in creation order; a sprite's <see cref="PixelSprite.Index"/> is its place here.</summary>
        public static IReadOnlyList<PixelSprite> All => _all;

        public static PixelSprite ResourceSprite => _resourceSprite;
        public static PixelSprite OreVeinSprite => _oreVeinSprite;
        public static PixelSprite HerbPatchSprite => _herbPatchSprite;
        public static PixelSprite WallSprite => _wallSprite;
        public static PixelSprite GetCampfireSprite(int frame) => _campfireSprites[frame & 1];
        private static readonly PixelSprite _bunkSprite;
        public static PixelSprite BunkSprite => _bunkSprite;
        private static readonly PixelSprite _floorSprite;
        private static readonly PixelSprite _rockSprite;
        /// <summary>Light grey flagstone, tinted per sector by the map view's floor tiles.</summary>
        public static PixelSprite FloorSprite => _floorSprite;
        /// <summary>Solid rock outside the floor.</summary>
        public static PixelSprite RockSprite => _rockSprite;
        private static readonly PixelSprite _selectionRing;
        /// <summary>A thin gold circle under the selected crawler (Unity's <c>CameraController</c> ring), one unit across.</summary>
        public static PixelSprite SelectionRing => _selectionRing;

        static ProceduralSprites()
        {
            // Base colors
            var clear = Rgba32.Clear;
            var outline = new Rgba32(20, 24, 33, 255);
            var skin = new Rgba32(245, 205, 175, 255);
            var white = new Rgba32(255, 255, 255, 255);
            var black = new Rgba32(20, 24, 33, 255);
            var hair = new Rgba32(90, 55, 35, 255);
            var tool = new Rgba32(148, 163, 184, 255);
            var exclamation = new Rgba32(254, 240, 138, 255);
            var question = new Rgba32(192, 132, 252, 255);

            // 1. Idle Sprites (Cyan Outfit)
            var idlePalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Rgba32(56, 189, 248, 255),
                shadow: new Rgba32(14, 116, 144, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Idle, 0] = CreateSprite(PatternIdle0, idlePalette, "Agent_Idle_0");
            _agentSprites[(int)AgentVisualState.Idle, 1] = CreateSprite(PatternIdle1, idlePalette, "Agent_Idle_1");

            // 2. Walking Sprites (Emerald Outfit)
            var walkPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Rgba32(34, 197, 94, 255),
                shadow: new Rgba32(21, 128, 61, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Walking, 0] = CreateSprite(PatternWalk0, walkPalette, "Agent_Walk_0");
            _agentSprites[(int)AgentVisualState.Walking, 1] = CreateSprite(PatternWalk1, walkPalette, "Agent_Walk_1");

            // 3. Harvesting Sprites (Amber Outfit + Tool)
            var harvestPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Rgba32(245, 158, 11, 255),
                shadow: new Rgba32(180, 83, 9, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Harvesting, 0] = CreateSprite(PatternHarvest0, harvestPalette, "Agent_Harvest_0");
            _agentSprites[(int)AgentVisualState.Harvesting, 1] = CreateSprite(PatternHarvest1, harvestPalette, "Agent_Harvest_1");

            // 4. Gossiping Sprites (Magenta Outfit + Speech Bubble)
            var gossipPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Rgba32(192, 38, 211, 255),
                shadow: new Rgba32(134, 25, 143, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Gossiping, 0] = CreateSprite(PatternGossip0, gossipPalette, "Agent_Gossip_0");
            _agentSprites[(int)AgentVisualState.Gossiping, 1] = CreateSprite(PatternGossip1, gossipPalette, "Agent_Gossip_1");

            // 5. Panicking Sprites (Crimson Outfit + Exclamation)
            var panicPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Rgba32(239, 68, 68, 255),
                shadow: new Rgba32(153, 27, 27, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Panicking, 0] = CreateSprite(PatternPanic0, panicPalette, "Agent_Panic_0");
            _agentSprites[(int)AgentVisualState.Panicking, 1] = CreateSprite(PatternPanic1, panicPalette, "Agent_Panic_1");

            // 6. Thinking Sprites (Indigo Outfit + Thought Cloud)
            var thinkPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Rgba32(99, 102, 241, 255),
                shadow: new Rgba32(67, 56, 202, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Thinking, 0] = CreateSprite(PatternThink0, thinkPalette, "Agent_Think_0");
            _agentSprites[(int)AgentVisualState.Thinking, 1] = CreateSprite(PatternThink1, thinkPalette, "Agent_Think_1");

            // 7. Resting Sprites (Soft Blue / Nightcap Outfit + Zzz)
            var restPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Rgba32(59, 130, 246, 255),
                shadow: new Rgba32(29, 78, 216, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Resting, 0] = CreateSprite(PatternRest0, restPalette, "Agent_Rest_0");
            _agentSprites[(int)AgentVisualState.Resting, 1] = CreateSprite(PatternRest1, restPalette, "Agent_Rest_1");

            // 8. Yielding Sprites (Warm Amber / Submissive Cower)
            var yieldPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Rgba32(251, 191, 36, 255),
                shadow: new Rgba32(217, 119, 6, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Yielding, 0] = CreateSprite(PatternYield0, yieldPalette, "Agent_Yield_0");
            _agentSprites[(int)AgentVisualState.Yielding, 1] = CreateSprite(PatternYield1, yieldPalette, "Agent_Yield_1");

            // 9. Sharing Sprites (Compassionate Emerald/Mint Outfit + Extended Hands)
            var sharePalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Rgba32(52, 211, 153, 255),
                shadow: new Rgba32(16, 185, 129, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Sharing, 0] = CreateSprite(PatternShare0, sharePalette, "Agent_Share_0");
            _agentSprites[(int)AgentVisualState.Sharing, 1] = CreateSprite(PatternShare1, sharePalette, "Agent_Share_1");

            // 10. Rallying Sprites (Gold / Amber livery + raised banner)
            var rallyPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Rgba32(234, 179, 8, 255),
                shadow: new Rgba32(161, 98, 7, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Rallying, 0] = CreateSprite(PatternRally0, rallyPalette, "Agent_Rally_0");
            _agentSprites[(int)AgentVisualState.Rallying, 1] = CreateSprite(PatternRally1, rallyPalette, "Agent_Rally_1");

            // 11. Defiant Sprites (Crimson / Dark Red Outfit + crossed arms)
            var defiantPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Rgba32(185, 28, 28, 255),
                shadow: new Rgba32(127, 29, 29, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Defiant, 0] = CreateSprite(PatternDefiant0, defiantPalette, "Agent_Defiant_0");
            _agentSprites[(int)AgentVisualState.Defiant, 1] = CreateSprite(PatternDefiant1, defiantPalette, "Agent_Defiant_1");

            // 11b. Sleeping Sprites (head on the bunk's pillow, under a blanket in the bunk's colours). Drawn over the
            // bunk sprite (agent scale 0.7, bunk 0.9): the blanket covers the bunk's blanket and leaves its frame showing.
            var sleepPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Rgba32(79, 110, 196, 255),
                shadow: new Rgba32(55, 79, 150, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Sleeping, 0] = CreateSprite(PatternSleep0, sleepPalette, "Agent_Sleep_0");
            _agentSprites[(int)AgentVisualState.Sleeping, 1] = CreateSprite(PatternSleep1, sleepPalette, "Agent_Sleep_1");

            // 12. Resource (Berry Bush)
            var bushPalette = new Dictionary<char, Rgba32>
            {
                { '.', Rgba32.Clear },
                { '#', new Rgba32(20, 50, 25, 255) },
                { 'G', new Rgba32(34, 139, 34, 255) },
                { 'R', new Rgba32(220, 38, 28, 255) }
            };
            _resourceSprite = CreateSprite(PatternBerryBush, bushPalette, "Resource_BerryBush");

            // 12b. Salvage nodes: tinted variants of the bush pattern so ore/herb nodes read distinctly
            var orePalette = new Dictionary<char, Rgba32>
            {
                { '.', Rgba32.Clear },
                { '#', new Rgba32(30, 41, 59, 255) },
                { 'G', new Rgba32(100, 116, 139, 255) },
                { 'R', new Rgba32(234, 88, 12, 255) }
            };
            _oreVeinSprite = CreateSprite(PatternBerryBush, orePalette, "Resource_OreVein");

            var herbPalette = new Dictionary<char, Rgba32>
            {
                { '.', Rgba32.Clear },
                { '#', new Rgba32(20, 60, 50, 255) },
                { 'G', new Rgba32(45, 212, 191, 255) },
                { 'R', new Rgba32(216, 180, 254, 255) }
            };
            _herbPatchSprite = CreateSprite(PatternBerryBush, herbPalette, "Resource_HerbPatch");

            // 11. Stone Wall (impassable map wall)
            var wallPalette = new Dictionary<char, Rgba32>
            {
                { '.', Rgba32.Clear },
                { '#', new Rgba32(30, 41, 59, 255) },   // Slate 800 mortar/border
                { 'S', new Rgba32(148, 163, 184, 255) }, // Slate 400 stone bevel
                { 'B', new Rgba32(71, 85, 105, 255) }   // Slate 600 stone brick body
            };
            _wallSprite = CreateSprite(PatternStoneWall, wallPalette, "Obstacle_StoneWall");

            // 12. Campfire (Gathering Anchor with animated embers & flames)
            var campfirePalette = new Dictionary<char, Rgba32>
            {
                { '.', Rgba32.Clear },
                { 'S', new Rgba32(100, 116, 139, 255) }, // Slate ring stones
                { 's', new Rgba32(71, 85, 105, 255) },   // Shadow stones
                { 'W', new Rgba32(120, 53, 15, 255) },   // Charred wood logs
                { 'w', new Rgba32(69, 26, 3, 255) },     // Dark wood bark
                { 'F', new Rgba32(239, 68, 68, 255) },   // Flame red outer
                { 'O', new Rgba32(249, 115, 22, 255) },  // Flame orange core
                { 'Y', new Rgba32(253, 224, 71, 255) }   // Embers bright yellow
            };
            _campfireSprites[0] = CreateSprite(PatternCampfire0, campfirePalette, "Anchor_Campfire_0");
            _campfireSprites[1] = CreateSprite(PatternCampfire1, campfirePalette, "Anchor_Campfire_1");

            // 13. Bunk (rest anchor: wooden frame, pillow, blanket)
            var bunkPalette = new Dictionary<char, Rgba32>
            {
                { '.', Rgba32.Clear },
                { 'W', new Rgba32(120, 72, 32, 255) },   // Wooden frame
                { 'w', new Rgba32(78, 45, 18, 255) },    // Frame shadow
                { 'P', new Rgba32(241, 245, 249, 255) }, // Pillow
                { 'B', new Rgba32(79, 110, 196, 255) },  // Blanket
                { 'b', new Rgba32(55, 79, 150, 255) }    // Blanket fold
            };
            _bunkSprite = CreateSprite(PatternBunk, bunkPalette, "Anchor_Bunk");

            // 14. Map tiles (Epic MAP Phase 4): a neutral floor the map view tints per sector, and rough rock
            var floorPalette = new Dictionary<char, Rgba32>
            {
                { 'F', new Rgba32(232, 232, 232, 255) }, // Flagstone
                { 'f', new Rgba32(212, 212, 212, 255) }, // Speckle
                { 'g', new Rgba32(190, 190, 190, 255) }  // Joint
            };
            _floorSprite = CreateSprite(PatternFloor, floorPalette, "Map_Floor");
            var rockPalette = new Dictionary<char, Rgba32>
            {
                { 'R', new Rgba32(58, 56, 64, 255) },
                { 'r', new Rgba32(46, 44, 52, 255) },
                { 'k', new Rgba32(74, 72, 82, 255) }
            };
            _rockSprite = CreateSprite(PatternRock, rockPalette, "Map_Rock");

            // 15. Selection ring (Epic CLIENT M1.5): generated, not a pattern, as Unity's CameraController made it
            _selectionRing = CreateRing();

            // 15. Glowstone (Epic 10 G2): the hazard sectors' rare salvage, a tinted bush glowing amber and cyan, made last so
            // every earlier sprite keeps its index
            var glowPalette = new Dictionary<char, Rgba32>
            {
                { '.', Rgba32.Clear },
                { '#', new Rgba32(46, 16, 101, 255) },
                { 'G', new Rgba32(251, 191, 36, 255) },
                { 'R', new Rgba32(103, 232, 249, 255) }
            };
            _glowstoneSprite = CreateSprite(PatternBerryBush, glowPalette, "Resource_Glowstone");
        }

        private static PixelSprite CreateRing()
        {
            const int size = 16;
            var gold = new Rgba32(250, 204, 21, 255);
            var pixels = new Rgba32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f, dy = y + 0.5f - size * 0.5f;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    pixels[y * size + x] = d >= 6.2f && d <= 7.6f ? gold : Rgba32.Clear;
                }
            }
            var sprite = new PixelSprite(_all.Count, "Selection_Ring", size, size, pixels);
            _all.Add(sprite);
            return sprite;
        }

        /// <summary>
        /// Returns the node sprite for a resource yielding the given item type.
        /// </summary>
        public static PixelSprite GetResourceSprite(ItemType yieldItem)
        {
            // By the item's sprite key in the catalog (item system I6); untyped yields and items without a node sprite
            // draw the berry bush
            string key = Simpiens.Simulation.Items.ItemCatalog.Get(yieldItem)?.SpriteKey;
            return key != null && _byName.TryGetValue(key, out var sprite) ? sprite : _resourceSprite;
        }

        public static PixelSprite GetAgentSprite(AgentVisualState state, int frame)
        {
            int stateIdx = (int)state;
            if (stateIdx < 0 || stateIdx >= _agentSprites.GetLength(0)) stateIdx = 0;
            return _agentSprites[stateIdx, frame & 1];
        }

        private static Dictionary<char, Rgba32> CreatePalette(
            Rgba32 clear, Rgba32 outline, Rgba32 skin, Rgba32 white, Rgba32 black, Rgba32 hair,
            Rgba32 primary, Rgba32 shadow, Rgba32 tool, Rgba32 ex, Rgba32 q)
        {
            return new Dictionary<char, Rgba32>
            {
                { '.', clear },
                { '#', outline },
                { 'K', skin },
                { 'W', white },
                { 'B', black },
                { 'H', hair },
                { 'C', primary },
                { 'S', shadow },
                { 'T', tool },
                { '!', ex },
                { '?', q },
                { 'Z', new Rgba32(147, 197, 253, 255) }
            };
        }

        private static PixelSprite CreateSprite(string[] lines, Dictionary<char, Rgba32> palette, string name)
        {
            const int width = 16;
            const int height = 16;
            // Rows from the top, as XNA textures store them (lines[0] is the sprite's top row); unknown characters are clear
            var pixels = new Rgba32[width * height];
            for (int y = 0; y < height; y++)
            {
                string line = lines[y];
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = palette.TryGetValue(line[x], out var color) ? color : Rgba32.Clear;
                }
            }
            var sprite = new PixelSprite(_all.Count, name, width, height, pixels);
            _all.Add(sprite);
            _byName[name] = sprite;
            return sprite;
        }

        #region ASCII Pattern Definitions (16x16)

        private static readonly string[] PatternIdle0 =
        {
            "................",
            ".....######.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#KBKKBK#....",
            "....#KKKKKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "...#CCCCCCCC#...",
            "...#KC####CK#...",
            "....#SSSSSS#....",
            "....#SS##SS#....",
            "....#SS##SS#....",
            "....###..###....",
            "................"
        };

        private static readonly string[] PatternIdle1 =
        {
            "................",
            "................",
            ".....######.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#KKKKKK#....",
            "....#KKKKKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "...#KC####CK#...",
            "....#SSSSSS#....",
            "....#SS##SS#....",
            "....#SS##SS#....",
            "....###..###....",
            "................"
        };

        private static readonly string[] PatternWalk0 =
        {
            "................",
            ".....######.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#KBKKBK#....",
            "....#KKKKKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "..#K#CCCCCC#....",
            "....#KC##CK#....",
            "....#SS##SS#....",
            "...#SS#...#S#...",
            "...#SS#...###...",
            "...###..........",
            "................"
        };

        private static readonly string[] PatternWalk1 =
        {
            "................",
            ".....######.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#KBKKBK#....",
            "....#KKKKKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "....#CCCCCC#K#..",
            "....#KC##CK#....",
            "....#SS##SS#....",
            "...#S#...#SS#...",
            "...###...#SS#...",
            "..........###...",
            "................"
        };

        private static readonly string[] PatternHarvest0 =
        {
            "................",
            ".....######.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#KBKKBK#....",
            "....#KKKKKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "...#CCCCCCCC#T#.",
            "...#KC####CK#TT#",
            "....#SSSSSS#.TT#",
            "....#SS##SS#..T.",
            "....#SS##SS#....",
            "....###..###....",
            "................"
        };

        private static readonly string[] PatternHarvest1 =
        {
            "..........TT#...",
            ".........TT#....",
            ".....#####T.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#KBKKBK#....",
            "....#KKKKKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "...#KC####CK#...",
            "....#SSSSSS#....",
            "....#SS##SS#....",
            "....#SS##SS#....",
            "....###..###....",
            "................"
        };

        private static readonly string[] PatternGossip0 =
        {
            "........####....",
            ".......#WWWW#...",
            ".......#WBBW#...",
            ".......#WWWW#...",
            "........#W#.....",
            ".....######.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#KBKKBK#....",
            "....#KKKKKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "....#SSSSSS#....",
            "....#SS##SS#....",
            "....###..###...."
        };

        private static readonly string[] PatternGossip1 =
        {
            ".......######...",
            "......#WWWWWW#..",
            "......#WBBBBW#..",
            "......#WWWWWW#..",
            ".......##W##....",
            ".....######.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#KBKKBK#....",
            "....#KKKKKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "....#SSSSSS#....",
            "....#SS##SS#....",
            "....###..###...."
        };

        private static readonly string[] PatternPanic0 =
        {
            ".......#!!#.....",
            ".......#!!#.....",
            ".......#..#.....",
            ".......#!!#.....",
            "..#K#...##...#K#",
            "..#CK########KC#",
            "...#CHHHHHHHC#..",
            "....#HKKKKH#....",
            "....#KWKKWK#....",
            "....#KKBBKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "....#SSSSSS#....",
            "....#SS##SS#....",
            "....###..###...."
        };

        private static readonly string[] PatternPanic1 =
        {
            "........##......",
            ".......#!!#.....",
            ".......#!!#.....",
            ".......#..#.....",
            ".......#!!#.....",
            "..#K#.######.#K#",
            "...#K#HHHHHH#K#.",
            "....#HKKKKH#....",
            "....#KWKKWK#....",
            "....#KKBBKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "....#SSSSSS#....",
            "...#SS#..#SS#...",
            "...###....###..."
        };

        private static readonly string[] PatternThink0 =
        {
            ".........##.....",
            "........#WW#....",
            ".........##.....",
            ".......#W#......",
            ".....######.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#KBKKBK#....",
            "....#KKKKKK#....",
            ".....######K#...",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "...#KC####C#....",
            "....#SSSSSS#....",
            "....#SS##SS#....",
            "....###..###...."
        };

        private static readonly string[] PatternThink1 =
        {
            ".......####.....",
            "......#WWWW#....",
            "......#W??W#....",
            ".......#WW#.....",
            "........##......",
            ".....######.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#KBKKBK#....",
            "....#KKKKKK#....",
            ".....######K#...",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "...#KC####C#....",
            "....#SSSSSS#....",
            "....###..###...."
        };

        private static readonly string[] PatternRest0 =
        {
            "..........ZZ....",
            "...........Z....",
            "..........ZZ....",
            ".....######.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#K##KK##K#..",
            "....#KKKKKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "...#CCCCCCCC#...",
            "...#KC####CK#...",
            "....#SSSSSS#....",
            "....#SS##SS#....",
            "....###..###...."
        };

        private static readonly string[] PatternRest1 =
        {
            "........ZZ......",
            ".........Z..ZZ..",
            "........ZZ...Z..",
            ".....######.ZZ..",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#K##KK##K#..",
            "....#KKKKKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "...#CCCCCCCC#...",
            "...#KC####CK#...",
            "....#SSSSSS#....",
            "....#SS##SS#....",
            "....###..###...."
        };

        private static readonly string[] PatternYield0 =
        {
            "................",
            "................",
            ".....######.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#K##KK##K#..",
            "....#KKKKKK#....",
            ".....######.....",
            "...#KCCCCCCK#...",
            "...#KKCCCCKK#...",
            "....#CCCCCC#....",
            "....#CCCCCC#....",
            "....#KC##CK#....",
            "....#SSSSSS#....",
            "....#SS##SS#....",
            "....###..###...."
        };

        private static readonly string[] PatternYield1 =
        {
            "................",
            "................",
            "................",
            ".....######.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#K##KK##K#..",
            "....#KKKKKK#....",
            "...#K######K#...",
            "...#KKCCCCKK#...",
            "....#CCCCCC#....",
            "....#CCCCCC#....",
            "....#KC##CK#....",
            "....#SSSSSS#....",
            "....#SS##SS#....",
            "....###..###...."
        };

        private static readonly string[] PatternShare0 =
        {
            "................",
            ".....######.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#KBKKBK#....",
            "....#KKKKKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "...#CCCCCC#KK#..",
            "...#CCCCCC#KK#..",
            "....#CCCCCC#....",
            "....#KC##CK#....",
            "....#SSSSSS#....",
            "....#SS##SS#....",
            "....###..###...."
        };

        private static readonly string[] PatternShare1 =
        {
            "................",
            ".....######.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#KBKKBK#....",
            "....#KKKKKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "...#CCCCCC##KK#.",
            "...#CCCCCC##KK#.",
            "....#CCCCCC#....",
            "....#KC##CK#....",
            "....#SSSSSS#....",
            "....#SS##SS#....",
            "....###..###...."
        };

        private static readonly string[] PatternBerryBush =
        {
            "................",
            ".....######.....",
            "...##GGGGGG##...",
            "..#GGGRRGGGGG#..",
            ".#GGGRRRRGGGG#..",
            ".#GGGGRRGGGRR#..",
            "#GGGGGGGGGRRRR#.",
            "#GRRGGGGGGGRRGG#",
            "#RRRRGGGRRGGGGG#",
            ".#RRGGGRRRRGGGG#",
            ".#GGGGGGGRRGGG#.",
            "..#GGGGGGGGGG#..",
            "...##GG##GG##...",
            ".....##..##.....",
            "................",
            "................"
        };

        private static readonly string[] PatternStoneWall =
        {
            "################",
            "#SSSSSSSSSSSSSS#",
            "#SBBBBBB#SBBBBB#",
            "#SBBBBBB#SBBBBB#",
            "#SSSSSSSSSSSSSS#",
            "################",
            "#SBBBB#SBBBBBBB#",
            "#SBBBB#SBBBBBBB#",
            "#SSSSSSSSSSSSSS#",
            "################",
            "#SBBBBBBB#SBBBB#",
            "#SBBBBBBB#SBBBB#",
            "#SSSSSSSSSSSSSS#",
            "################",
            "#SBBBB#SBBBBBBB#",
            "################"
        };

        private static readonly string[] PatternSleep0 =
        {
            "................",
            ".....######.ZZ..",
            "....#HHHHHH#.Z..",
            "....#K#KK#K#ZZ..",
            "....#KKKKKK#....",
            "..############..",
            "..#CCCCCCCCCC#..",
            "..#CCSSSSSSCC#..",
            "..#CCCCCCCCCC#..",
            "..#CCCCCCCCCC#..",
            "..#CCCCCCCCCC#..",
            "..#CCSSSSSSCC#..",
            "..#CCCCCCCCCC#..",
            "..############..",
            "................",
            "................"
        };

        private static readonly string[] PatternSleep1 =
        {
            ".............ZZ.",
            ".....######...Z.",
            "....#HHHHHH#.ZZ.",
            "....#K#KK#K#....",
            "....#KKKKKK#....",
            "..############..",
            "..#CCCCCCCCCC#..",
            "..#CCCSSSSCCC#..",
            "..#CCCCCCCCCC#..",
            "..#CCCCCCCCCC#..",
            "..#CCCCCCCCCC#..",
            "..#CCCSSSSCCC#..",
            "..#CCCCCCCCCC#..",
            "..############..",
            "................",
            "................"
        };

        private static readonly string[] PatternFloor =
        {
            "fFFFFFFFFFFFFFFg",
            "FFFFFFFFfFFFFFFg",
            "FFFFFFFFFFFFFFFg",
            "FfFFFFFFFFFFFFFg",
            "FFFFFFFFFfFFFFFg",
            "FFFFFFFFFFFFFFFg",
            "FFfFFFFFFFFFFFFg",
            "FFFFFFFFFFfFFFFg",
            "FFFFFFFFFFFFFFFg",
            "FFFfFFFFFFFFFFFg",
            "FFFFFFFFFFFfFFFg",
            "FFFFFFFFFFFFFFFg",
            "FFFFfFFFFFFFFFFg",
            "FFFFFFFFFFFFfFFg",
            "FFFFFFFFFFFFFFFg",
            "gggggggggggggggg"
        };

        private static readonly string[] PatternRock =
        {
            "kRRRRrRkRRrRRRkr",
            "RrkRRRrRRkRrRRRR",
            "RRrRkRRrRRRkrRRR",
            "RRRrRRkRrRRRRkRR",
            "RkRRrRRRkrRRRRrk",
            "rRRkRrRRRRkRRRRr",
            "RrRRRkrRRRRrkRRR",
            "kRrRRRRkRRRRrRkR",
            "RRkrRRRRrkRRRrRR",
            "RRRRkRRRRrRkRRrR",
            "rRRRRrkRRRrRRkRr",
            "RkRRRRrRkRRrRRRk",
            "RRrkRRRrRRkRrRRR",
            "RRRrRkRRrRRRkrRR",
            "kRRRrRRkRrRRRRkR",
            "rRkRRrRRRkrRRRRr"
        };

        private static readonly string[] PatternBunk =
        {
            "................",
            "..WWWWWWWWWWWW..",
            "..WPPPPPPPPPPW..",
            "..WPPPPPPPPPPW..",
            "..WWWWWWWWWWWW..",
            "..WBBBBBBBBBBW..",
            "..WBBBBBBBBBBW..",
            "..WbbbbbbbbbbW..",
            "..WBBBBBBBBBBW..",
            "..WBBBBBBBBBBW..",
            "..WBBBBBBBBBBW..",
            "..WBBBBBBBBBBW..",
            "..WbbbbbbbbbbW..",
            "..WWWWWWWWWWWW..",
            "..ww........ww..",
            "................"
        };

        private static readonly string[] PatternCampfire0 =
        {
            "................",
            ".....SSSSSS.....",
            "...SS......SS...",
            "..S...F..F...S..",
            ".S...FO..OF...S.",
            ".S..FOYYYYOF..S.",
            "S...W.OYYO.W...S",
            "S..W...YY...W..S",
            "S..w..OYYO..w..S",
            "S...W.OYYO.W...S",
            ".S..FOYYYYOF..S.",
            ".S...FO..OF...S.",
            "..S...F..F...S..",
            "...ss......ss...",
            ".....ssssss.....",
            "................"
        };

        private static readonly string[] PatternCampfire1 =
        {
            "................",
            ".....SSSSSS.....",
            "...SS......SS...",
            "..S....FF....S..",
            ".S...FFYYFF...S.",
            ".S..FOOYYOOF..S.",
            "S...W.YYYY.W...S",
            "S..W.OYYYYO.W..S",
            "S..w..YYYY..w..S",
            "S...W.OYYO.W...S",
            ".S..FOOYYOOF..S.",
            ".S...FF..FF...S.",
            "..S....FF....S..",
            "...ss......ss...",
            ".....ssssss.....",
            "................"
        };

        private static readonly string[] PatternRally0 =
        {
            ".......#CC#.....",
            "......#CCCC#....",
            "......#CCCC#....",
            ".......#TT#.....",
            "..#T#...##......",
            "..#TT#.######...",
            "...#THHHHHHH#...",
            "....#HKKKKH#....",
            "....#KBKKBK#....",
            "....#KKKKKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "....#SSSSSS#....",
            "....#SS##SS#....",
            "....###..###...."
        };

        private static readonly string[] PatternRally1 =
        {
            "......#CCCC#....",
            ".....#CCCCCC#...",
            "......#CCCC#....",
            ".......#TT#.....",
            "..#T#...##......",
            "..#TT#.######...",
            "...#THHHHHHH#...",
            "....#HKKKKH#....",
            "....#KBKKBK#....",
            "....#KKKKKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#CCCCCCCC#...",
            "....#SSSSSS#....",
            "...#SS#..#SS#...",
            "...###....###..."
        };

        private static readonly string[] PatternDefiant0 =
        {
            "................",
            ".....######.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#BBKKBB#....",
            "....#KKKKKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#KKKKKKKK#...",
            "...#K######K#...",
            "....#CCCCCC#....",
            "....#SSSSSS#....",
            "....#SS##SS#....",
            "....#SS##SS#....",
            "....###..###....",
            "................"
        };

        private static readonly string[] PatternDefiant1 =
        {
            "................",
            ".....######.....",
            "....#HHHHHH#....",
            "....#HKKKKH#....",
            "....#BBKKBB#....",
            "....#KKKKKK#....",
            ".....######.....",
            "....#CCCCCC#....",
            "...#KKKKKKKK#...",
            "...#K######K#...",
            "....#SSSSSS#....",
            "....#SSSSSS#....",
            "...#SS#..#SS#...",
            "...#SS#..#SS#...",
            "...###....###...",
            "................"
        };

        #endregion
    }
}
