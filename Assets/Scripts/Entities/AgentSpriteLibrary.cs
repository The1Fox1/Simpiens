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
        private static readonly Sprite[,] _agentSprites = new Sprite[7, 2];
        private static readonly Sprite _resourceSprite;

        public static Sprite ResourceSprite => _resourceSprite;

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

            // 8. Resource (Berry Bush)
            var bushPalette = new Dictionary<char, Color32>
            {
                { '.', Color.clear },
                { '#', new Color32(20, 50, 25, 255) },
                { 'G', new Color32(34, 139, 34, 255) },
                { 'R', new Color32(220, 38, 38, 255) }
            };
            _resourceSprite = CreateSprite(PatternBerryBush, bushPalette, "Resource_BerryBush");
        }

        public static Sprite GetAgentSprite(AgentVisualState state, int frame)
        {
            int stateIdx = (int)state;
            if (stateIdx < 0 || stateIdx >= 7) stateIdx = 0;
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

            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 16f);
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

        #endregion
    }
}
