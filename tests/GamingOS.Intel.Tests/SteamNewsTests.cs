using GamingOS.Intel;

namespace GamingOS.Intel.Tests;

public class SteamNewsTests
{
    // Same shape as a real ISteamNews/GetNewsForApp response.
    private const string Response = """
        {"appnews":{"appid":730,"newsitems":[
          {"gid":"1","title":" Counter-Strike 2 Update ","url":"https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/1","contents":"...","feedname":"steam_community_announcements","feed_type":1,"date":1795474927,"appid":730},
          {"gid":"2","title":"Broken link","url":"javascript:alert(1)","date":1795049879},
          {"gid":"3","title":"","url":"https://example.com/x","date":1795049879},
          {"gid":"4","title":"No date","url":"https://example.com/y"}
        ],"count":120}}
        """;

    [Fact]
    public void Parse_keeps_complete_items_and_drops_unsafe_or_incomplete_ones()
    {
        var item = Assert.Single(SteamNews.Parse(Response));

        Assert.Equal("Counter-Strike 2 Update", item.Title);
        Assert.StartsWith("https://steamstore-a.akamaihd.net/", item.Url);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1795474927), item.PublishedAt);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"appnews":{"newsitems":{}}}""")]
    [InlineData("""{"appnews":{"newsitems":[{"title":5,"url":"https://x.y","date":1}]}}""")]
    public void Parse_returns_nothing_for_unexpected_responses(string json) => Assert.Empty(SteamNews.Parse(json));

    [Fact]
    public void RequestUri_asks_only_for_official_announcements_of_that_game()
    {
        var uri = SteamNews.RequestUri(730, 3).ToString();

        Assert.StartsWith("https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/", uri);
        Assert.Contains("appid=730", uri);
        Assert.Contains("count=3", uri);
        Assert.Contains("feeds=steam_community_announcements", uri);
    }

    [Fact]
    public async Task News_is_cached_and_being_offline_is_not_an_error()
    {
        var now = DateTimeOffset.UnixEpoch;
        var downloads = 0;
        Task<string> Download(Uri _)
        {
            downloads++;
            return Task.FromResult(Response);
        }

        // Unique ids: the cache is shared by the whole test run.
        Assert.Single(await IntelModule.NewsAsync(900001, Download, now));
        Assert.Single(await IntelModule.NewsAsync(900001, Download, now.AddMinutes(29)));
        Assert.Equal(1, downloads);
        Assert.Single(await IntelModule.NewsAsync(900001, Download, now.AddMinutes(31)));
        Assert.Equal(2, downloads);

        Assert.Empty(await IntelModule.NewsAsync(900002, _ => throw new HttpRequestException("offline"), now));
        Assert.Single(await IntelModule.NewsAsync(900002, Download, now)); // a failure is not remembered
    }
}
