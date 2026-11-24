using System.Diagnostics;
using GamingOS.Core;
using GamingOS.Library.Steam;

namespace GamingOS.Library;

/// <summary>The game library: answers the UI's library.* requests.</summary>
public static class LibraryModule
{
    public static void Register(IpcRouter router)
    {
        router.Register("library.list", _ => Task.FromResult<object?>(List()));
        router.Register("library.launch", parameters =>
        {
            var gameId = parameters?.GetProperty("gameId").GetInt64() ?? throw new ArgumentException("gameId is required.");
            var steamPath = SteamLibrary.FindSteamPath() ?? throw new InvalidOperationException("Steam is not installed.");
            Process.Start(new ProcessStartInfo(LaunchUri(gameId, SteamLibrary.Scan(steamPath))) { UseShellExecute = true });
            return Task.FromResult<object?>(null);
        });
    }

    /// <summary>
    /// Folders of images the host should serve to the UI, keyed by the host name used in artwork addresses.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ArtworkFolders() =>
        SteamLibrary.FindSteamPath() is { } steamPath && Directory.Exists(SteamArtwork.CacheFolder(steamPath))
            ? new Dictionary<string, string> { [SteamArtwork.Host] = SteamArtwork.CacheFolder(steamPath) }
            : [];

    // shortcut: rescans on every request, cache in the database once it exists and libraries get large.
    private static GameDto[] List()
    {
        if (SteamLibrary.FindSteamPath() is not { } steamPath) return [];
        var artwork = SteamArtwork.CacheFolder(steamPath);
        return [.. SteamLibrary.Scan(steamPath).Select(game => ToDto(game, artwork))];
    }

    // shortcut: the Steam app id doubles as the game id, replace with a database id when Epic and GOG arrive.
    public static GameDto ToDto(SteamGame game, string? artworkFolder = null) =>
        new(game.AppId, game.Name, "steam", game.Installed, PlaytimeMinutes: null, AccentColor(game.AppId))
        {
            // Steam renamed the portrait cover over time; both names are still found on disk.
            CoverUrl = artworkFolder is null ? null : SteamArtwork.Find(artworkFolder, game.AppId, "library_600x900.jpg", "library_capsule.jpg"),
            HeroUrl = artworkFolder is null ? null : SteamArtwork.Find(artworkFolder, game.AppId, "library_hero.jpg"),
            LogoUrl = artworkFolder is null ? null : SteamArtwork.Find(artworkFolder, game.AppId, "logo.png"),
        };

    /// <summary>A stable, distinct colour per game until real artwork provides one.</summary>
    public static string AccentColor(long appId) => $"hsl({appId * 137 % 360} 85% 62%)";

    /// <summary>
    /// The steam:// address that starts a game. Only ids of games found on this PC are accepted,
    /// so the UI can never make the host open an arbitrary address.
    /// </summary>
    public static string LaunchUri(long gameId, IEnumerable<SteamGame> knownGames)
    {
        var game = knownGames.FirstOrDefault(g => g.AppId == gameId) ?? throw new ArgumentException($"Unknown game {gameId}.");
        if (!game.Installed) throw new InvalidOperationException($"{game.Name} is not installed.");
        return $"steam://rungameid/{game.AppId}";
    }
}
