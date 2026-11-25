using Microsoft.Win32;

namespace GamingOS.Library.Steam;

public sealed record SteamGame(long AppId, string Name, string InstallPath, bool Installed);

/// <summary>
/// Finds installed Steam games from Steam's own local files (docs/PLAN.md 5.1). Reads only; no Steam account or network.
/// </summary>
public static class SteamLibrary
{
    // Entries Steam keeps next to games that are not games: 228980 = Steamworks Common Redistributables,
    // 431960 = Wallpaper Engine (runs all day, would count as endless play).
    // shortcut: a fixed list, replace with the app type from Steam's appinfo.vdf when more software shows up.
    private static readonly HashSet<long> NotGames = [228980, 431960];

    private const int StateFullyInstalled = 4;

    /// <summary>Steam's install folder from the registry, or null when Steam is not installed.</summary>
    public static string? FindSteamPath() =>
        Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
        ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;

    /// <summary>Library folders listed in steamapps/libraryfolders.vdf. Handles the current and the pre-2021 layout.</summary>
    public static IReadOnlyList<string> LibraryFolders(string libraryFoldersVdf)
    {
        if (!Vdf.Parse(libraryFoldersVdf).TryGetValue("libraryfolders", out var node) || node is not Dictionary<string, object> entries)
            return [];

        var folders = new List<string>();
        foreach (var (key, value) in entries)
        {
            if (!int.TryParse(key, out _)) continue; // other keys are statistics, not folders
            var path = value switch
            {
                string old => old,
                Dictionary<string, object> current when current.TryGetValue("path", out var p) => p as string,
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(path)) folders.Add(path);
        }
        return folders;
    }

    /// <summary>One appmanifest_*.acf file. Returns null for entries that are not games or lack an id or name.</summary>
    public static SteamGame? ParseManifest(string acf, string steamappsDir)
    {
        if (!Vdf.Parse(acf).TryGetValue("AppState", out var node) || node is not Dictionary<string, object> state)
            return null;

        string? Text(string key) => state.TryGetValue(key, out var value) ? value as string : null;

        if (!long.TryParse(Text("appid"), out var appId) || NotGames.Contains(appId)) return null;
        if (Text("name") is not { Length: > 0 } name || Text("installdir") is not { Length: > 0 } installDir) return null;

        _ = int.TryParse(Text("StateFlags"), out var flags);
        return new SteamGame(appId, name, Path.Combine(steamappsDir, "common", installDir), (flags & StateFullyInstalled) != 0);
    }

    /// <summary>All games in every library folder of the Steam installation at <paramref name="steamPath"/>.</summary>
    public static IReadOnlyList<SteamGame> Scan(string steamPath)
    {
        var folders = new List<string> { steamPath };
        try
        {
            folders.AddRange(LibraryFolders(File.ReadAllText(Path.Combine(steamPath, "steamapps", "libraryfolders.vdf"))));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException)
        {
            // Without the list we still have the main folder.
        }

        var games = new Dictionary<long, SteamGame>();
        foreach (var folder in folders.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var steamapps = Path.Combine(folder, "steamapps");
            if (!Directory.Exists(steamapps)) continue; // e.g. an unplugged external drive

            foreach (var manifest in Directory.EnumerateFiles(steamapps, "appmanifest_*.acf"))
            {
                try
                {
                    if (ParseManifest(File.ReadAllText(manifest), steamapps) is { } game) games.TryAdd(game.AppId, game);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException)
                {
                    // One unreadable manifest must not hide the rest of the library.
                }
            }
        }
        return [.. games.Values.OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)];
    }
}
