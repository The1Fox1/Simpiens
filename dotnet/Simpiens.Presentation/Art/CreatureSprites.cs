using System.Collections.Generic;

namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// The creatures (Epic 10 Part 3, K2.5): each kind seen from the side, facing right, in two frames of a scuttle (its
    /// legs alternate), on a crawler's 16-pixel canvas with its feet on row 14, so it stands in isometric as a crawler does
    /// and is drawn at a crawler's scale in top-down; a boss on a bigger canvas, its feet at the same share of its height
    /// (K10.5a, <see cref="SizeOf"/>). Placeholders, generated on the CPU from patterns as
    /// <see cref="CaravanSprites"/> are; the scene mirrors a creature going left. Keyed by each kind's art key
    /// (<see cref="Simpiens.Simulation.Creatures.CreatureDefinition.ArtKey"/>), as <see cref="ItemArt"/> keys items; a
    /// generator, it reads no table and no sheet (<c>ArtInitialisationTests</c>).
    /// </summary>
    public static class CreatureSprites
    {
        public const int Size = 16;
        public const int Frames = 2;

        /// <summary>The kinds drawn, by art key: the burrow rat, the stone beetle, the cave spinner, and the bosses (K10): the Matriarch and the Gatekeeper.</summary>
        public static IReadOnlyList<string> Keys { get; } = new[] { "burrow-rat", "stone-beetle", "cave-spinner", "matriarch", "gatekeeper" };

        private static readonly List<PixelSprite> _all = new List<PixelSprite>(Frames * 5);

        public static IReadOnlyList<PixelSprite> All => _all;

        /// <summary>A kind's frame: <c>Creature_burrow-rat_0</c>.</summary>
        public static string Name(string key, int frame) => "Creature_" + key + "_" + frame;

        /// <summary>A kind's frames, in order (setup only: builds strings).</summary>
        public static string[] Names(string key)
        {
            var names = new string[Frames];
            for (int i = 0; i < Frames; i++) names[i] = Name(key, i);
            return names;
        }

        private static readonly Dictionary<char, Rgba32> Palette = new Dictionary<char, Rgba32>
        {
            { '.', Rgba32.Clear },
            // The burrow rat: dusty brown fur, a pale belly, pink ears, feet, nose and tail, a red eye
            { 'k', new Rgba32(34, 26, 30, 255) },
            { 'F', new Rgba32(128, 110, 98, 255) },
            { 'f', new Rgba32(92, 76, 68, 255) },
            { 'L', new Rgba32(180, 162, 144, 255) },
            { 'P', new Rgba32(224, 154, 152, 255) },
            { 'p', new Rgba32(178, 112, 116, 255) },
            { 'R', new Rgba32(236, 58, 50, 255) },
            // The stone beetle: a slate shell flecked with moss, a dark underside, an amber eye and a horn-coloured mandible
            { 'o', new Rgba32(26, 28, 34, 255) },
            { 'S', new Rgba32(106, 114, 126, 255) },
            { 's', new Rgba32(74, 80, 92, 255) },
            { 'H', new Rgba32(164, 174, 186, 255) },
            { 'D', new Rgba32(52, 54, 62, 255) },
            { 'M', new Rgba32(204, 176, 124, 255) },
            { 'Y', new Rgba32(244, 196, 72, 255) },
            { 'g', new Rgba32(110, 140, 84, 255) },
            // The cave spinner: a pale abdomen with violet spots, a shaded head, green eyes, long dark legs
            { 'q', new Rgba32(32, 24, 42, 255) },
            { 'B', new Rgba32(204, 196, 218, 255) },
            { 'b', new Rgba32(146, 136, 170, 255) },
            { 'V', new Rgba32(116, 84, 156, 255) },
            { 'G', new Rgba32(150, 244, 120, 255) },
            { 'l', new Rgba32(94, 80, 116, 255) },
            // The Matriarch (Epic 10 Part 3, K10.5a): a heavy rat in grizzled grey-violet with a silver ruff
            { 'X', new Rgba32(112, 104, 124, 255) },
            { 'x', new Rgba32(78, 72, 90, 255) },
            { 'W', new Rgba32(210, 206, 222, 255) },
            // The Gatekeeper (K10.5a): a stone warden, moss in its seams, embers for eyes and for a heart
            { 'O', new Rgba32(24, 22, 28, 255) },
            { 'U', new Rgba32(150, 144, 134, 255) },
            { 'T', new Rgba32(118, 112, 104, 255) },
            { 't', new Rgba32(82, 78, 74, 255) },
            { 'E', new Rgba32(255, 140, 60, 255) },
        };

        private static readonly string[] Rat0 =
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "..........kk....",
            ".....kkkkkPPk...",
            "...kkFFFFFkkkk..",
            "..kFFFFFFFFFRFk.",
            ".kfFFFFFFFFFFFPk",
            "pkfFFLLLLLFFFkk.",
            "p.kffkkkkkfkk...",
            ".p.kPk...kPk....",
            "................",
        };

        private static readonly string[] Rat1 =
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "..........kk....",
            ".....kkkkkPPk...",
            "...kkFFFFFkkkk..",
            "..kFFFFFFFFFRFk.",
            "pkfFFFFFFFFFFFPk",
            "p.kfFLLLLLFFFkk.",
            "p..kfkkkkkfkk...",
            "...kPk....kPk...",
            "................",
        };

        private static readonly string[] Beetle0 =
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "....oooooo......",
            "..ooHHHHSSoo....",
            ".oHHSSSSSgSSo...",
            "oHSSgSSSSSSSsoo.",
            "oSSSSSSSSSSssoYo",
            "osSSSSSSSSssoDDM",
            ".ossssssssssoDo.",
            "..oDDDDDDDDDoo..",
            "..o.o..o..o.o...",
            "................",
        };

        private static readonly string[] Beetle1 =
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "....oooooo......",
            "..ooHHHHSSoo....",
            ".oHHSSSSSgSSo...",
            "oHSSgSSSSSSSsoo.",
            "oSSSSSSSSSSssoYo",
            "osSSSSSSSSssoDDM",
            ".ossssssssssoDo.",
            "..oDDDDDDDDDoo..",
            "...o.o..o..o.o..",
            "................",
        };

        private static readonly string[] Spinner0 =
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            ".......ll..ll...",
            "......l..ll..l..",
            "..qqq.l.qqq...l.",
            ".qBBBqqqbbbq..l.",
            "qBVBBBBqbbGGq..l",
            "qBBBBVBqbbbbq..l",
            "qBBVBBbqqbbq...l",
            "qbBBBbbql.ql..l.",
            ".qbbbbqql..l..l.",
            "..qqqq.l...l.l..",
            "................",
        };

        private static readonly string[] Spinner1 =
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "......ll..ll....",
            ".....l..ll..l...",
            "..qqql..qqq..l..",
            ".qBBBqqqbbbq..l.",
            "qBVBBBBqbbGGq..l",
            "qBBBBVBqbbbbq..l",
            "qBBVBBbqqbbq..l.",
            "qbBBBbbql.ql..l.",
            ".qbbbbqql..l.l..",
            "..qqqq..l..l.l..",
            "................",
        };

        private static readonly string[] Matriarch0 =
        {
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
            "................kk......",
            "...............kPPk.....",
            "..........kkkkkkPPk.....",
            "......kkkkWWWWWkkkkk....",
            "....kkXXXWWWWWWXXXXkk...",
            "...kXXXXXXWWWXXXXXXRXk..",
            "..kxXXXXXXXXXXXXXXXXXXk.",
            ".kxXXXXXXXXXXXXXXXXXXXPk",
            ".kxXXXXXXXXXXXXXXXXXkkk.",
            "pkxxXXXLLLLLLLLXXXXk....",
            "p.kxxxXLLLLLLLLXXXk.....",
            "p..kxxxkkkkkkkkxxk......",
            ".p..kxk......kxk........",
            "..p.kxk......kxk........",
            "....kPk......kPk........",
            "....kkk......kkk........",
            "........................",
            "........................",
        };

        private static readonly string[] Matriarch1 =
        {
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
            "........................",
            "................kk......",
            "...............kPPk.....",
            "..........kkkkkkPPk.....",
            "......kkkkWWWWWkkkkk....",
            "....kkXXXWWWWWWXXXXkk...",
            "...kXXXXXXWWWXXXXXXRXk..",
            "..kxXXXXXXXXXXXXXXXXXXk.",
            "pkxXXXXXXXXXXXXXXXXXXXPk",
            "pkxXXXXXXXXXXXXXXXXXkkk.",
            "p.kxXXXLLLLLLLLXXXXk....",
            "p..kxxXLLLLLLLLXXXk.....",
            "....kxxkkkkkkkkxxk......",
            "...kxk........kxk.......",
            "...kxk........kxk.......",
            "...kPk........kPk.......",
            "...kkk........kkk.......",
            "........................",
            "........................",
        };

        private static readonly string[] Keeper0 =
        {
            "................................",
            "................................",
            "................................",
            "................................",
            "............OOOOOOOO............",
            "...........OUUUUUUUTO...........",
            "...........OUTTTTTTTO...........",
            "...........OTEETTEETO...........",
            "...........OTTTTTTTtO...........",
            "...........OTtTgTTttO...........",
            "......OOOOOOOTTTTTTOOOOOOO......",
            ".....OUUUUUUTTTTTTTTUUUUUUO.....",
            "....OUTTTTTTTTTTTTTTTTTTTTtO....",
            "....OTTTOTTTTTTTTTTTTTTOTTtO....",
            "....OTTTOTTTTTEETTTTTTTOTTtO....",
            "....OTTTOTTTTEEEETTTTTTOTTtO....",
            "....OTTTOTTTTTEETTTTTTTOTTtO....",
            "....OTTtOTTTTTTTTTTTTttOTTtO....",
            "....OTTtOOTTTTTTTTTTttOOTTtO....",
            "....OUUtO.OttttttttttO.OUUtO....",
            "....OTTTO.OTTTTTTTTTTO.OTTTO....",
            "....OOOOO.OTTTTOOTTTTO.OOOOO....",
            "..........OTTTTOOTTTTO..........",
            "..........OTTTO..OTTTO..........",
            "..........OTTTO..OTTTO..........",
            "..........OTTtO..OTTtO..........",
            "..........OTTtO..OTTtO..........",
            ".........OTTTTO..OTTTTO.........",
            ".........OttttO..OttttO.........",
            ".........OOOOOO..OOOOOO.........",
            "................................",
            "................................",
        };

        private static readonly string[] Keeper1 =
        {
            "................................",
            "................................",
            "................................",
            "................................",
            "............OOOOOOOO............",
            "...........OUUUUUUUTO...........",
            "...........OUTTTTTTTO...........",
            "...........OTEETTEETO...........",
            "...........OTTTTTTTtO...........",
            "...........OTtTgTTttO...........",
            "......OOOOOOOTTTTTTOOOOOOO......",
            ".....OUUUUUUTTTTTTTTUUUUUUO.....",
            "....OUTTTTTTTTTTTTTTTTTTTTtO....",
            "....OTTTOTTTTTTTTTTTTTTOTTtO....",
            "....OTTTOTTTTTEETTTTTTTOTTtO....",
            "....OTTTOTTTTEEEETTTTTTOTTtO....",
            "....OTTTOTTTTTEETTTTTTTOTTtO....",
            "....OTTtOTTTTTTTTTTTTttOTTtO....",
            "....OTTtOOTTTTTTTTTTttOOTTtO....",
            "....OUUtO.OttttttttttO.OUUtO....",
            "....OTTTO.OTTTTTTTTTTO.OTTTO....",
            "....OOOOO.OTTTTOOTTTTO.OOOOO....",
            "..........OTTTTOOTTTTO..........",
            "..........OTTTO..OTTTO..........",
            "..........OTTTO...OTTTO.........",
            "..........OTTtO...OTTtO.........",
            ".........OTTtO.....OTTtO........",
            "........OTTTTO.....OTTTTO.......",
            "........OttttO.....OttttO.......",
            "........OOOOOO.....OOOOOO.......",
            "................................",
            "................................",
        };

        static CreatureSprites()
        {
            Add("burrow-rat", Rat0, Rat1);
            Add("stone-beetle", Beetle0, Beetle1);
            Add("cave-spinner", Spinner0, Spinner1);
            Add("matriarch", Matriarch0, Matriarch1); // the bosses (K10.5a): bigger canvases, feet at the same share of their height
            Add("gatekeeper", Keeper0, Keeper1);
        }

        private static readonly Dictionary<string, int> _sizes = new Dictionary<string, int>();

        /// <summary>
        /// A kind's canvas in pixels, square (Epic 10 Part 3, K10.5a): <see cref="Size"/> for the vermin, more for a boss (the
        /// Matriarch 24, the Gatekeeper 32), its feet at the same share of its height, so it stands in isometric as they do.
        /// </summary>
        public static int SizeOf(string key) => _sizes.TryGetValue(key ?? string.Empty, out int size) ? size : Size;

        private static void Add(string key, params string[][] frames)
        {
            _sizes[key] = frames[0].Length;
            for (int i = 0; i < frames.Length; i++) _all.Add(Build(Name(key, i), frames[i]));
        }

        private static PixelSprite Build(string name, string[] pattern)
        {
            int size = pattern.Length;
            var pixels = new Rgba32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++) pixels[y * size + x] = Palette[pattern[y][x]];
            return new PixelSprite(name, size, size, pixels);
        }
    }
}
