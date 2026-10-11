namespace Simpiens.Cognition.Groups
{
    /// <summary>
    /// Unmanaged 4-byte struct for categorical identity affiliation (Layer 2 of sociological taxonomy).
    /// Injects baseline dyadic bias when agents first interact.
    /// </summary>
    public readonly struct IdentityTag
    {
        public readonly byte CultureId;
        public readonly byte KinshipId;
        public readonly byte FaithId;
        public readonly byte CasteRank;

        public IdentityTag(byte cultureId, byte kinshipId, byte faithId, byte casteRank)
        {
            CultureId = cultureId;
            KinshipId = kinshipId;
            FaithId = faithId;
            CasteRank = casteRank;
        }

        public static readonly IdentityTag Default = new IdentityTag(0, 0, 0, 0);
    }
}
