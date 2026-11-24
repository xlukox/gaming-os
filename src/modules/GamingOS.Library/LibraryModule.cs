using System.Diagnostics;
using GamingOS.Core;
using GamingOS.Library.Steam;

namespace GamingOS.Library;

/// <summary>The game library: answers the UI's library.* requests.</summary>
public static class LibraryModule
{
    public static void Register(IpcRouter router)
    {
        router.Register("library.list", _ => Task.FromResult<object?>(SteamGames().Select(ToDto).ToArray()));
        router.Register("library.launch", parameters =>
        {
            var gameId = parameters?.GetProperty("gameId").GetInt64() ?? throw new ArgumentException("gameId is required.");
            Process.Start(new ProcessStartInfo(LaunchUri(gameId, SteamGames())) { UseShellExecute = true });
            return Task.FromResult<object?>(null);
        });
    }

    // shortcut: rescans on every request, cache in the database once it exists and libraries get large.
    private static IReadOnlyList<SteamGame> SteamGames() =>
        SteamLibrary.FindSteamPath() is { } steamPath ? SteamLibrary.Scan(steamPath) : [];

    // shortcut: the Steam app id doubles as the game id, replace with a database id when Epic and GOG arrive.
    public static GameDto ToDto(SteamGame game) =>
        new(game.AppId, game.Name, "steam", game.Installed, PlaytimeMinutes: null, AccentColor(game.AppId));

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
