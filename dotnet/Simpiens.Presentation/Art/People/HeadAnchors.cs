using System.Collections.Generic;
using Simpiens.Entities;

namespace Simpiens.Presentation.Art.People
{
    /// <summary>
    /// Where a crawler's head sits in one pose frame (Epic UI-Beta B2), in rows of its 16x16 pattern
    /// (<see cref="ProceduralSprites.CrawlerKeys"/>). The crown's outline is the row above the hair's top, the head's
    /// outline the row below the chin, and the body starts under that. The head's inside is columns
    /// <see cref="Left"/> to <see cref="Right"/> in every frame.
    /// </summary>
    public readonly struct HeadAnchor
    {
        /// <summary>The head's inside columns, the same in every frame.</summary>
        public const int Left = 5, Right = 10;

        public HeadAnchor(int hairTop, int eyes, int chin, bool lying = false)
        {
            HairTop = hairTop;
            Eyes = eyes;
            Chin = chin;
            Lying = lying;
        }

        /// <summary>The hair's first row, full across the head.</summary>
        public int HairTop { get; }

        /// <summary>The eyes' row, open or not (a blink, closed eyes or a sleeper's are drawn there too).</summary>
        public int Eyes { get; }

        /// <summary>The face's last row: the mouth's.</summary>
        public int Chin { get; }

        /// <summary>The lying pose: one row of hair, the head on the pillow, the body under a blanket.</summary>
        public bool Lying { get; }

        /// <summary>The body's first row, under the head's outline.</summary>
        public int BodyTop => Chin + 2;
    }

    /// <summary>
    /// Every pose frame's head (Epic UI-Beta B2), held as data: B0 found that inferring the head from the hair's pixels
    /// fails on a blink and on the panicking eyes (design note, §9). A row per frame, in <see cref="AgentVisualState"/>
    /// order; a test checks each against its pattern. A new pose frame is a row here.
    /// </summary>
    public static class HeadAnchors
    {
        private static readonly HeadAnchor[] _rows =
        {
            new HeadAnchor(2, 4, 5), new HeadAnchor(3, 5, 6), // Idle: frame 1 bobs down a row, blinking
            new HeadAnchor(2, 4, 5), new HeadAnchor(2, 4, 5), // Walking
            new HeadAnchor(2, 4, 5), new HeadAnchor(3, 5, 6), // Harvesting: frame 1 swings the tool over the crown
            new HeadAnchor(6, 8, 9), new HeadAnchor(6, 8, 9), // Gossiping: under the speech bubble
            new HeadAnchor(6, 8, 9), new HeadAnchor(6, 8, 9), // Panicking: arms raised beside the hair, eyes wide, mouth open
            new HeadAnchor(5, 7, 8), new HeadAnchor(6, 8, 9), // Thinking: under the thought cloud
            new HeadAnchor(4, 6, 7), new HeadAnchor(4, 6, 7), // Resting: eyes closed, a hand to the face
            new HeadAnchor(3, 5, 6), new HeadAnchor(4, 6, 7), // Yielding: cowering
            new HeadAnchor(2, 4, 5), new HeadAnchor(2, 4, 5), // Sharing
            new HeadAnchor(6, 8, 9), new HeadAnchor(6, 8, 9), // Rallying: the banner's pole beside the hair
            new HeadAnchor(2, 4, 5), new HeadAnchor(2, 4, 5), // Defiant: brows down
            new HeadAnchor(2, 3, 4, lying: true), new HeadAnchor(2, 3, 4, lying: true), // Sleeping
        };

        /// <summary>The head in <paramref name="state"/>'s <paramref name="frame"/> (0 or 1).</summary>
        public static HeadAnchor Of(AgentVisualState state, int frame)
        {
            int index = (int)state * 2 + (frame & 1);
            return (uint)index < (uint)_rows.Length ? _rows[index] : _rows[0];
        }

        /// <summary>Frames held: two per state.</summary>
        public static int Count => _rows.Length;

        /// <summary>Every frame's head, in <see cref="AgentVisualState"/> order.</summary>
        public static IReadOnlyList<HeadAnchor> All => _rows;
    }
}
