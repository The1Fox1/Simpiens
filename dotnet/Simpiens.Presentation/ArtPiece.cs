namespace Simpiens.Presentation
{
    /// <summary>
    /// One picture of something drawn (M3, design D4): a sprite in its sheet, its size in art pixels at zoom 1, and
    /// where its anchor sits in it (0 to 1 from its top-left: 0.5, 0.5 its centre; 0.5, 1 its feet). No sprite: nothing
    /// is drawn. An art catalogue makes them (<see cref="Art.ArtCatalog.Piece(Art.ArtId, float)"/>).
    /// </summary>
    public readonly struct ArtPiece
    {
        public ArtPiece(int sprite, float width, float height, float pivotX = 0.5f, float pivotY = 0.5f, DrawSheet sheet = DrawSheet.World)
        {
            Sheet = sheet;
            Sprite = sprite;
            Width = width;
            Height = height;
            PivotX = pivotX;
            PivotY = pivotY;
        }

        public static ArtPiece None => new ArtPiece(-1, 0f, 0f);

        /// <summary>The sheet the sprite is drawn from.</summary>
        public DrawSheet Sheet { get; }
        public int Sprite { get; }
        public float Width { get; }
        public float Height { get; }
        public float PivotX { get; }
        public float PivotY { get; }
        public bool IsNone => Sprite < 0;
    }
}
