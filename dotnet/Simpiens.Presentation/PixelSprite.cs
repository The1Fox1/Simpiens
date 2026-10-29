using System;

namespace Simpiens.Presentation
{
    /// <summary>A colour as four bytes, as image files and textures store it (Unity's <c>Color32</c>).</summary>
    public readonly struct Rgba32 : IEquatable<Rgba32>
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;
        public readonly byte A;

        public Rgba32(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static readonly Rgba32 Clear = new Rgba32(0, 0, 0, 0);

        /// <summary>A float colour in bytes, rounded as Unity converts a <c>Color</c> to a <c>Color32</c>.</summary>
        public static Rgba32 From(Rgba color) => new Rgba32(ToByte(color.R), ToByte(color.G), ToByte(color.B), ToByte(color.A));

        private static byte ToByte(float v) => (byte)MathF.Round(Math.Clamp(v, 0f, 1f) * 255f);

        public bool Equals(Rgba32 other) => R == other.R && G == other.G && B == other.B && A == other.A;
        public override bool Equals(object obj) => obj is Rgba32 other && Equals(other);
        public override int GetHashCode() => R | (G << 8) | (B << 16) | (A << 24);
        public static bool operator ==(Rgba32 a, Rgba32 b) => a.Equals(b);
        public static bool operator !=(Rgba32 a, Rgba32 b) => !a.Equals(b);
        public override string ToString() => $"({R}, {G}, {B}, {A})";
    }

    /// <summary>
    /// A generated image: a name, and pixels row by row from the top (as XNA textures store them). Where it sits in a
    /// texture is its sheet's business (<see cref="Art.SpriteSheet"/>, graphics V1); nothing here knows about textures.
    /// </summary>
    public sealed class PixelSprite
    {
        public PixelSprite(string name, int width, int height, Rgba32[] pixels)
        {
            if (pixels == null || pixels.Length != width * height) throw new ArgumentException($"{name}: expected {width * height} pixels.", nameof(pixels));
            Name = name;
            Width = width;
            Height = height;
            Pixels = pixels;
        }

        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public Rgba32[] Pixels { get; }

        public override string ToString() => $"{Name} ({Width}x{Height})";
    }
}
