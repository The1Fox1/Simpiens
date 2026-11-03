namespace Simpiens.Client.Ui
{
    /// <summary>
    /// The interface's motion (the UI polish plan, U7): short, eased out, on real time so a paused world still answers,
    /// and only where the host asks for it (<see cref="ClientOptions.UiMotion"/>: the desktop game). Tests and the pinned
    /// renders run without it, so nothing they capture is caught between two frames of a tween.
    /// Durations follow NN/g's guidance for UI animation: a modal 200–300 ms, feedback about 100 ms, nothing past 500 ms;
    /// shorter here, because a management game opens its windows hundreds of times a session.
    /// </summary>
    public static class Motion
    {
        /// <summary>A window arriving: its veil fades in and its frame rises <see cref="WindowRise"/> pixels.</summary>
        public const float WindowIn = 0.16f;

        /// <summary>How far a window rises as it arrives, in pixels.</summary>
        public const float WindowRise = 8f;

        /// <summary>A figure in the header rolling to its new value.</summary>
        public const float NumberRoll = 0.25f;

        /// <summary>Ease-out (cubic): fast at first, settling at the end. <paramref name="t"/> runs 0 to 1.</summary>
        public static float EaseOut(float t)
        {
            t = System.Math.Clamp(t, 0f, 1f);
            float u = 1f - t;
            return 1f - u * u * u;
        }
    }
}
