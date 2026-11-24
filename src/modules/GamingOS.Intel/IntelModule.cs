using System.Collections.Concurrent;
using GamingOS.Core;

namespace GamingOS.Intel;

/// <summary>News about games: answers the UI's intel.* requests.</summary>
public static class IntelModule
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(30);
    private static readonly ConcurrentDictionary<long, (DateTimeOffset At, IReadOnlyList<NewsItemDto> Items)> Cache = new();

    public static void Register(IpcRouter router) =>
        router.Register("intel.news", async parameters =>
        {
            // shortcut: the game id is the Steam app id, look the platform up once games have database ids.
            var gameId = parameters?.GetProperty("gameId").GetInt64() ?? throw new ArgumentException("gameId is required.");
            return await NewsAsync(gameId, async uri => await Http.GetStringAsync(uri), DateTimeOffset.UtcNow);
        });

    /// <summary>News for a game, remembered for half an hour so browsing the library does not hit the network each time.</summary>
    public static async Task<IReadOnlyList<NewsItemDto>> NewsAsync(long appId, Func<Uri, Task<string>> download, DateTimeOffset now)
    {
        if (Cache.TryGetValue(appId, out var cached) && now - cached.At < CacheFor) return cached.Items;

        IReadOnlyList<NewsItemDto> items;
        try
        {
            items = SteamNews.Parse(await download(SteamNews.RequestUri(appId, count: 3)));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // Offline or Steam is slow: the panel stays empty and the next request tries again.
            return [];
        }
        Cache[appId] = (now, items);
        return items;
    }
}
