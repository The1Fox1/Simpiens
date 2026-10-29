namespace Simpiens.Presentation.Art
{
    /// <summary>
    /// What a thing without art of its own is drawn with (graphics V3.3): magenta and black in 4-texel checks, so nobody
    /// mistakes it for art. Every catalogue has it (<see cref="ArtKeys.Missing"/>), and a test fails when shipped content
    /// reaches it.
    /// </summary>
    public static class MissingArt
    {
        public const string Name = "Fx_Missing";
        public const int Size = 16;

        public static PixelSprite Sprite { get; } = Build();

        private static PixelSprite Build()
        {
            var magenta = new Rgba32(255, 0, 255);
            var black = new Rgba32(0, 0, 0);
            var pixels = new Rgba32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    pixels[y * Size + x] = ((x / 4 + y / 4) & 1) == 0 ? magenta : black;
            return new PixelSprite(Name, Size, Size, pixels);
        }
    }
}
