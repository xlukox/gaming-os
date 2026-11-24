using System.Globalization;

namespace GamingOS.Library.Steam;

/// <summary>
/// Game artwork Steam has already downloaded to appcache/librarycache/&lt;appid&gt;/, sometimes one hashed folder deeper.
/// Nothing is downloaded or copied: the host serves that folder to the UI under <see cref="Host"/>.
/// </summary>
public static class SteamArtwork
{
    /// <summary>Name the host maps to the cache folder, so the UI can load files from it.</summary>
    public const string Host = "steam-art.gamingos.local";

    // GetFullPath: the registry stores the Steam path with forward slashes.
    public static string CacheFolder(string steamPath) => Path.GetFullPath(Path.Combine(steamPath, "appcache", "librarycache"));

    /// <summary>Address of the first of <paramref name="fileNames"/> that exists for the game, or null.</summary>
    public static string? Find(string cacheFolder, long appId, params string[] fileNames)
    {
        var gameFolder = Path.Combine(cacheFolder, appId.ToString(CultureInfo.InvariantCulture));
        try
        {
            if (!Directory.Exists(gameFolder)) return null;
            foreach (var name in fileNames)
            {
                var file = Directory.EnumerateFiles(gameFolder, name, SearchOption.AllDirectories).FirstOrDefault();
                if (file is not null)
                    return $"https://{Host}/{Path.GetRelativePath(cacheFolder, file).Replace('\\', '/')}";
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Missing artwork is cosmetic; the UI has a fallback.
        }
        return null;
    }
}
