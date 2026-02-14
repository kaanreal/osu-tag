namespace Osutag.Services
{
    /// <summary>
    /// Compile-time embedded Spotify API credentials.
    /// These are replaced at build time via MSBuild properties.
    /// </summary>
    internal static class SpotifyCredentials
    {
#if SPOTIFY_CLIENT_ID
        public const string ClientId = SPOTIFY_CLIENT_ID;
#else
        public const string ClientId = "";
#endif

#if SPOTIFY_CLIENT_SECRET
        public const string ClientSecret = SPOTIFY_CLIENT_SECRET;
#else
        public const string ClientSecret = "";
#endif
    }
}
