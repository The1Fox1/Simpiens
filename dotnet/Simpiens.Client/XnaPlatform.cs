namespace Simpiens.Client
{
    /// <summary>
    /// The one place where KNI and MonoGame differ (Epic CLIENT). The rest of the client uses only the XNA 4 API both
    /// implement, and CI compiles it against both (<c>-p:XnaFramework=MonoGame</c>), so a switch stays a package swap.
    /// </summary>
    public static class XnaPlatform
    {
#if KNI
        public const string Name = "KNI";
#elif MONOGAME
        public const string Name = "MonoGame";
#else
        public const string Name = "unknown";
#endif
    }
}
