using System.Globalization;
using System.Text.Json;
using GamingOS.Core;

namespace GamingOS.Intel;

/// <summary>
/// Official announcements for a Steam game from Steam's public news API (ISteamNews/GetNewsForApp).
/// Needs no key or account; the only thing sent is the game's Steam app id.
/// </summary>
public static class SteamNews
{
    // steam_community_announcements = posts by the game's own developers, not press articles.
    public static Uri RequestUri(long appId, int count) => new(
        "https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?format=json&maxlength=1&feeds=steam_community_announcements" +
        $"&appid={appId.ToString(CultureInfo.InvariantCulture)}&count={count.ToString(CultureInfo.InvariantCulture)}");

    /// <summary>Reads the API response. Anything unexpected yields fewer items, never an exception.</summary>
    public static IReadOnlyList<NewsItemDto> Parse(string json)
    {
        var items = new List<NewsItemDto>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("appnews", out var news) || !news.TryGetProperty("newsitems", out var list))
                return items;

            foreach (var item in list.EnumerateArray())
            {
                if (item.TryGetProperty("title", out var title) && title.GetString() is { Length: > 0 } text
                    && item.TryGetProperty("url", out var url) && IsWebAddress(url.GetString())
                    && item.TryGetProperty("date", out var date) && date.TryGetInt64(out var seconds))
                {
                    items.Add(new NewsItemDto(text.Trim(), url.GetString()!, DateTimeOffset.FromUnixTimeSeconds(seconds)));
                }
            }
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            // A response we cannot read means no news, not a broken screen.
        }
        return items;
    }

    // The address comes from the network and may later be opened; only real web links pass.
    private static bool IsWebAddress(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
