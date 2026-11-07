namespace Simpiens.Presentation
{
    /// <summary>
    /// The red coat a crawler wears while it is hurt (Epic 10 Part 2, S3, D71; <see cref="CrawlerFlags.Hurt"/>): its own
    /// sprite drawn again right over it as a flat red silhouette (<see cref="DrawItem.Silhouette"/>), blinking. A blink
    /// snaps on and fades out a step at a time, then is off for a step. Every hurt crawler blinks together, on the view's
    /// clock. Presentation only: the simulation never sees it.
    /// </summary>
    public static class HurtCoat
    {
        /// <summary>Blinks a second: under the 3 flashes a second that photosensitivity guidance sets as the limit.</summary>
        public const float BlinksPerSecond = 2f;

        /// <summary>The coat's strength through one blink, a step at a time: on, fading, and off for the last step.</summary>
        private static readonly float[] Steps = { 0.65f, 0.4f, 0.15f, 0f };

        /// <summary>The strongest the coat gets: never full red, so the crawler shows through it.</summary>
        public static float Peak => Steps[0];

        /// <summary>The coat's colour; its strength is the alpha.</summary>
        public static readonly Rgba32 Colour = new Rgba32(230, 38, 30, 255);

        /// <summary>The coat's strength, 0 to <see cref="Peak"/>, at <paramref name="viewSeconds"/> of the view.</summary>
        public static float Strength(float viewSeconds)
        {
            float phase = viewSeconds * BlinksPerSecond;
            phase -= System.MathF.Floor(phase);
            int step = (int)(phase * Steps.Length);
            return Steps[step < 0 ? 0 : step >= Steps.Length ? Steps.Length - 1 : step];
        }
    }
}
