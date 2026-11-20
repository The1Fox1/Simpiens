using System;
using Simpiens.Simulation.Skills;

namespace Simpiens.Presentation.Art.People
{
    /// <summary>
    /// The scars a crawler bears, as the art draws them (Epic 11, S6.7, D36): a shape at each of its four places on the face
    /// (<see cref="ScarPlace"/>: left cheek, brow, right cheek, chin), or none. From its traits record into the frames, the body's
    /// key and the portrait's key, so a scar taken bakes a new body and face, and none changes nothing.
    /// </summary>
    public readonly struct ScarMarks : IEquatable<ScarMarks>
    {
        // A shape + 1 for each place; 0 for none
        private readonly byte _p0, _p1, _p2, _p3;

        private ScarMarks(byte p0, byte p1, byte p2, byte p3)
        {
            _p0 = p0;
            _p1 = p1;
            _p2 = p2;
            _p3 = p3;
        }

        /// <summary>The scars <paramref name="traits"/> holds, each by its row's shape.</summary>
        public static ScarMarks Of(in CrawlerTraits traits)
        {
            Span<byte> marks = stackalloc byte[CrawlerTraits.ScarSlots];
            for (int i = 0; i < CrawlerTraits.ScarSlots; i++)
            {
                var row = ScarTable.Get(traits.ScarAt(i));
                marks[i] = row == null ? (byte)0 : (byte)(1 + (byte)row.Shape);
            }
            return new ScarMarks(marks[0], marks[1], marks[2], marks[3]);
        }

        /// <summary>Marks with <paramref name="shape"/> at each of <paramref name="places"/> (tests, and the review sheet).</summary>
        public static ScarMarks With(ScarShape shape, params ScarPlace[] places)
        {
            Span<byte> marks = stackalloc byte[CrawlerTraits.ScarSlots];
            foreach (var place in places) marks[(int)place] = (byte)(1 + (byte)shape);
            return new ScarMarks(marks[0], marks[1], marks[2], marks[3]);
        }

        /// <summary>True when there is a scar at <paramref name="place"/>, with its shape.</summary>
        public bool At(ScarPlace place, out ScarShape shape)
        {
            byte mark = (int)place switch { 0 => _p0, 1 => _p1, 2 => _p2, 3 => _p3, _ => (byte)0 };
            shape = mark == 0 ? default : (ScarShape)(mark - 1);
            return mark != 0;
        }

        /// <summary>No scar anywhere.</summary>
        public bool IsEmpty => (_p0 | _p1 | _p2 | _p3) == 0;

        public bool Equals(ScarMarks other) => _p0 == other._p0 && _p1 == other._p1 && _p2 == other._p2 && _p3 == other._p3;

        public override bool Equals(object obj) => obj is ScarMarks other && Equals(other);

        public override int GetHashCode() => _p0 | (_p1 << 8) | (_p2 << 16) | (_p3 << 24);
    }
}
