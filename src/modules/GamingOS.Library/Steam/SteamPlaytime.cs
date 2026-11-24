using System.Globalization;
using Microsoft.Win32;

namespace GamingOS.Library.Steam;

/// <summary>
/// Lifetime playtime Steam keeps for the signed-in account in userdata/&lt;account&gt;/config/localconfig.vdf.
/// Only the per-game "Playtime" values are read; the rest of that file is never touched.
/// </summary>
public static class SteamPlaytime
{
    /// <summary>Minutes played per Steam app id. Missing or unreadable data yields fewer entries, never an exception.</summary>
    public static IReadOnlyDictionary<long, int> Parse(string localConfigVdf)
    {
        var minutes = new Dictionary<long, int>();
        Dictionary<string, object>? node;
        try
        {
            node = Vdf.Parse(localConfigVdf);
        }
        catch (FormatException)
        {
            return minutes;
        }

        foreach (var key in (string[])["UserLocalConfigStore", "Software", "Valve", "Steam", "apps"])
        {
            if (!node.TryGetValue(key, out var child) || child is not Dictionary<string, object> next) return minutes;
            node = next;
        }

        foreach (var (appId, value) in node)
        {
            if (long.TryParse(appId, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                && value is Dictionary<string, object> app
                && app.TryGetValue("Playtime", out var played)
                && int.TryParse(played as string, NumberStyles.None, CultureInfo.InvariantCulture, out var total))
            {
                minutes[id] = total;
            }
        }
        return minutes;
    }

    /// <summary>
    /// The localconfig.vdf of the account signed in to Steam right now; when Steam is not running,
    /// the one written most recently. Null when there is none.
    /// </summary>
    public static string? FindLocalConfig(string steamPath)
    {
        var userdata = Path.Combine(steamPath, "userdata");
        if (!Directory.Exists(userdata)) return null;

        if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam\ActiveProcess", "ActiveUser", null) is int active and not 0)
        {
            var current = Path.Combine(userdata, active.ToString(CultureInfo.InvariantCulture), "config", "localconfig.vdf");
            if (File.Exists(current)) return current;
        }

        return Directory.EnumerateDirectories(userdata)
            .Select(account => Path.Combine(account, "config", "localconfig.vdf"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    /// <summary>Playtime for the current account, or nothing when it cannot be read.</summary>
    public static IReadOnlyDictionary<long, int> Load(string steamPath)
    {
        try
        {
            return FindLocalConfig(steamPath) is { } file ? Parse(File.ReadAllText(file)) : new Dictionary<long, int>();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new Dictionary<long, int>(); // Steam may be writing the file; next refresh will have it
        }
    }
}
