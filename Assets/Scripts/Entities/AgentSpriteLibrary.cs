using System;
using System.Collections.Generic;
using UnityEngine;

namespace Simpiens.Entities
{
    /// <summary>
    /// Generates and caches procedural 16x16 pixel-art action-state sprites and icons.
    /// Operates with zero runtime GC allocations by pre-generating all frames once at startup.
    /// </summary>
    public static class AgentSpriteLibrary
    {
        private static readonly Sprite[,] _agentSprites = new Sprite[11, 2];
        private static readonly Sprite _resourceSprite;
        private static readonly Sprite _oreVeinSprite;
        private static readonly Sprite _herbPatchSprite;
        private static readonly Sprite _wallSprite;
        private static readonly Sprite[] _campfireSprites = new Sprite[2];

        public static Sprite ResourceSprite => _resourceSprite;
        public static Sprite OreVeinSprite => _oreVeinSprite;
        public static Sprite HerbPatchSprite => _herbPatchSprite;
        public static Sprite WallSprite => _wallSprite;
        public static Sprite GetCampfireSprite(int frame) => _campfireSprites[frame & 1];

        static AgentSpriteLibrary()
        {
            // Base colors
            var clear = Color.clear;
            var outline = new Color32(20, 24, 33, 255);
            var skin = new Color32(245, 205, 175, 255);
            var white = new Color32(255, 255, 255, 255);
            var black = new Color32(20, 24, 33, 255);
            var hair = new Color32(90, 55, 35, 255);
            var tool = new Color32(148, 163, 184, 255);
            var exclamation = new Color32(254, 240, 138, 255);
            var question = new Color32(192, 132, 252, 255);

            // 1. Idle Sprites (Cyan Outfit)
            var idlePalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Color32(56, 189, 248, 255),
                shadow: new Color32(14, 116, 144, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Idle, 0] = CreateSprite(PatternIdle0, idlePalette, "Agent_Idle_0");
            _agentSprites[(int)AgentVisualState.Idle, 1] = CreateSprite(PatternIdle1, idlePalette, "Agent_Idle_1");

            // 2. Walking Sprites (Emerald Outfit)
            var walkPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Color32(34, 197, 94, 255),
                shadow: new Color32(21, 128, 61, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Walking, 0] = CreateSprite(PatternWalk0, walkPalette, "Agent_Walk_0");
            _agentSprites[(int)AgentVisualState.Walking, 1] = CreateSprite(PatternWalk1, walkPalette, "Agent_Walk_1");

            // 3. Harvesting Sprites (Amber Outfit + Tool)
            var harvestPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Color32(245, 158, 11, 255),
                shadow: new Color32(180, 83, 9, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Harvesting, 0] = CreateSprite(PatternHarvest0, harvestPalette, "Agent_Harvest_0");
            _agentSprites[(int)AgentVisualState.Harvesting, 1] = CreateSprite(PatternHarvest1, harvestPalette, "Agent_Harvest_1");

            // 4. Gossiping Sprites (Magenta Outfit + Speech Bubble)
            var gossipPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Color32(192, 38, 211, 255),
                shadow: new Color32(134, 25, 143, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Gossiping, 0] = CreateSprite(PatternGossip0, gossipPalette, "Agent_Gossip_0");
            _agentSprites[(int)AgentVisualState.Gossiping, 1] = CreateSprite(PatternGossip1, gossipPalette, "Agent_Gossip_1");

            // 5. Panicking Sprites (Crimson Outfit + Exclamation)
            var panicPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Color32(239, 68, 68, 255),
                shadow: new Color32(153, 27, 27, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Panicking, 0] = CreateSprite(PatternPanic0, panicPalette, "Agent_Panic_0");
            _agentSprites[(int)AgentVisualState.Panicking, 1] = CreateSprite(PatternPanic1, panicPalette, "Agent_Panic_1");

            // 6. Thinking Sprites (Indigo Outfit + Thought Cloud)
            var thinkPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Color32(99, 102, 241, 255),
                shadow: new Color32(67, 56, 202, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Thinking, 0] = CreateSprite(PatternThink0, thinkPalette, "Agent_Think_0");
            _agentSprites[(int)AgentVisualState.Thinking, 1] = CreateSprite(PatternThink1, thinkPalette, "Agent_Think_1");

            // 7. Resting Sprites (Soft Blue / Nightcap Outfit + Zzz)
            var restPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Color32(59, 130, 246, 255),
                shadow: new Color32(29, 78, 216, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Resting, 0] = CreateSprite(PatternRest0, restPalette, "Agent_Rest_0");
            _agentSprites[(int)AgentVisualState.Resting, 1] = CreateSprite(PatternRest1, restPalette, "Agent_Rest_1");

            // 8. Yielding Sprites (Warm Amber / Submissive Cower)
            var yieldPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Color32(251, 191, 36, 255),
                shadow: new Color32(217, 119, 6, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Yielding, 0] = CreateSprite(PatternYield0, yieldPalette, "Agent_Yield_0");
            _agentSprites[(int)AgentVisualState.Yielding, 1] = CreateSprite(PatternYield1, yieldPalette, "Agent_Yield_1");

            // 9. Sharing Sprites (Compassionate Emerald/Mint Outfit + Extended Hands)
            var sharePalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Color32(52, 211, 153, 255),
                shadow: new Color32(16, 185, 129, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Sharing, 0] = CreateSprite(PatternShare0, sharePalette, "Agent_Share_0");
            _agentSprites[(int)AgentVisualState.Sharing, 1] = CreateSprite(PatternShare1, sharePalette, "Agent_Share_1");

            // 10. Rallying Sprites (Gold / Amber livery + raised banner)
            var rallyPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Color32(234, 179, 8, 255),
                shadow: new Color32(161, 98, 7, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Rallying, 0] = CreateSprite(PatternRally0, rallyPalette, "Agent_Rally_0");
            _agentSprites[(int)AgentVisualState.Rallying, 1] = CreateSprite(PatternRally1, rallyPalette, "Agent_Rally_1");

            // 11. Defiant Sprites (Crimson / Dark Red Outfit + crossed arms)
            var defiantPalette = CreatePalette(clear, outline, skin, white, black, hair,
                primary: new Color32(185, 28, 28, 255),
                shadow: new Color32(127, 29, 29, 255),
                tool: tool, ex: exclamation, q: question);

            _agentSprites[(int)AgentVisualState.Defiant, 0] = CreateSprite(PatternDefiant0, defiantPalette, "Agent_Defiant_0");
            _agentSprites[(int)AgentVisualState.Defiant, 1] = CreateSprite(PatternDefiant1, defiantPalette, "Agent_Defiant_1");

            // 12. Resource (Berry Bush)
            var bushPalette = new Dictionary<char, Color32>
            {
                { '.', Color.clear },
                { '#', new Color32(20, 50, 25, 255) },
                { 'G', new Color32(34, 139, 34, 255) },
                { 'R', new Color32(220, 38, 28, 255) }
            };
            _resourceSprite = CreateSprite(PatternBerryBush, bushPalette, "Resource_BerryBush");

            // 12b. Salvage nodes: tinted variants of the bush pattern so ore/herb nodes read distinctly
            var orePalette = new Dictionary<char, Color32>
            {
                { '.', Color.clear },
                { '#', new Color32(30, 41, 59, 255) },
                { 'G', new Color32(100, 116, 139, 255) },
                { 'R', new Color32(234, 88, 12, 255) }
            };
            _oreVeinSprite = CreateSprite(PatternBerryBush, orePalette, "Resource_OreVein");

            var herbPalette = new Dictionary<char, Color32>
            {
                { '.', Color.clear },
                { '#', new Color32(20, 60, 50, 255) },
                { 'G', new Color32(45, 212, 191, 255) },
                { 'R', new Color32(216, 180, 254, 255) }
            };
            _herbPatchSprite = CreateSprite(PatternBerryBush, herbPalette, "Resource_HerbPatch");

            // 11. Stone Wall (Impassable play pen barrier)
            var wallPalette = new Dictionary<char, Color32>
            {
                { '.', Color.clear },
                { '#', new Color32(30, 41, 59, 255) },   // Slate 800 mortar/border
                { 'S', new Color32(148, 163, 184, 255) }, // Slate 400 stone bevel
                { 'B', new Color32(71, 85, 105, 255) }   // Slate 600 stone brick body
            };
            _wallSprite = CreateSprite(PatternStoneWall, wallPalette, "Obstacle_StoneWall");

            // 12. Campfire (Gathering Anchor with animated embers & flames)
            var campfirePalette = new Dictionary<char, Color32>
            {
                { '.', Color.clear },
                { 'S', new Color32(100, 116, 139, 255) }, // Slate ring stones
                { 's', new Color32(71, 85, 105, 255) },   // Shadow stones
                { 'W', new Color32(120, 53, 15, 255) },   // Charred wood logs
                { 'w', new Color32(69, 26, 3, 255) },     // Dark wood bark
                { 'F', new Color32(239, 68, 68, 255) },   // Flame red outer
                { 'O', new Color32(249, 115, 22, 255) },  // Flame orange core
                { 'Y', new Color32(253, 224, 71, 255) }   // Embers bright yellow
            };
            _campfireSprites[0] = CreateSprite(PatternCampfire0, campfirePalette, "Anchor_Campfire_0");
            _campfireSprites[1] = CreateSprite(PatternCampfire1, campfirePalette, "Anchor_Campfire_1");
        }

        /// <summary>
        /// Returns the node sprite for a resource yielding the given item type.
        /// </summary>
        public static Sprite GetResourceSprite(Simpiens.Simulation.Economy.ItemType yieldItem)
        {
            switch (yieldItem)
            {
                case Simpiens.Simulation.Economy.ItemType.IronOre: return _oreVeinSprite;
                case Simpiens.Simulation.Economy.ItemType.MedicinalHerb: return _herbPatchSprite;
                default: return _resourceSprite;
            }
        }

        public static Sprite GetAgentSprite(AgentVisualState state, int frame)
        {
            int stateIdx = (int)state;
            if (stateIdx < 0 || stateIdx >= 11) stateIdx = 0;
            return _agentSprites[stateIdx, frame & 1];
        }

        private static Dictionary<char, Color32> CreatePalette(
            Color32 clear, Color32 outline, Color32 skin, Color32 white, Color32 black, Color32 hair,
            Color32 primary, Color32 shadow, Color32 tool, Color32 ex, Color32 q)
        {
            return new Dictionary<char, Color32>
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
                { 'Z', new Color32(147, 197, 253, 255) }
            };
        }

        private static Sprite CreateSprite(string[] lines, Dictionary<char, Color32> palette, string name)
        {
            const int width = 16;
            const int height = 16;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                // lines[0] is top of the sprite (y = 15)
                string line = lines[height - 1 - y];
                for (int x = 0; x < width; x++)
                {
                    char c = line[x];
                    if (palette.TryGetValue(c, out var color))
                    {
                        pixels[y * width + x] = color;
                    }
                    else
                    {
                        pixels[y * width + x] = Color.clear;
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 16f);
            sprite.name = name;
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
