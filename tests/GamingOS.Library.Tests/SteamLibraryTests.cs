using GamingOS.Library;
using GamingOS.Library.Steam;

namespace GamingOS.Library.Tests;

public class SteamLibraryTests
{
    // Same shape as a real appmanifest_*.acf written by Steam.
    private static string Manifest(long appId, string name, string installDir, int stateFlags = 4) => $$"""
        "AppState"
        {
        	"appid"		"{{appId}}"
        	"universe"		"1"
        	"name"		"{{name}}"
        	"StateFlags"		"{{stateFlags}}"
        	"installdir"		"{{installDir}}"
        	"SizeOnDisk"		"14964354280"
        	"InstalledDepots"
        	{
        		"551"
        		{
        			"manifest"		"123"
        		}
        	}
        }
        """;

    [Fact]
    public void Vdf_reads_nested_blocks_escapes_and_comments()
    {
        var root = Vdf.Parse("""
            // header comment
            "Root"
            {
                "path"   "C:\\Games\\Steam"
                "quote"  "say \"hi\""
                bare     value
                "inner" { "Key" "1" }
            }
            """);

        var block = Assert.IsType<Dictionary<string, object>>(root["root"]); // keys are case-insensitive
        Assert.Equal(@"C:\Games\Steam", block["path"]);
        Assert.Equal("say \"hi\"", block["quote"]);
        Assert.Equal("value", block["bare"]);
        Assert.Equal("1", Assert.IsType<Dictionary<string, object>>(block["inner"])["key"]);
    }

    [Theory]
    [InlineData("\"a\" {")]
    [InlineData("\"a\" \"b\" }")]
    [InlineData("\"a\"")]
    [InlineData("\"a\" \"unterminated")]
    public void Vdf_rejects_broken_text(string text) => Assert.Throws<FormatException>(() => Vdf.Parse(text));

    [Fact]
    public void LibraryFolders_reads_the_current_layout()
    {
        var folders = SteamLibrary.LibraryFolders("""
            "libraryfolders"
            {
            	"0"
            	{
            		"path"		"C:\\Program Files (x86)\\Steam"
            		"apps" { "550" "14964354280" }
            	}
            	"1"
            	{
            		"path"		"D:\\SteamLibrary"
            	}
            }
            """);

        Assert.Equal([@"C:\Program Files (x86)\Steam", @"D:\SteamLibrary"], folders);
    }

    [Fact]
    public void LibraryFolders_reads_the_old_layout_and_skips_statistics()
    {
        var folders = SteamLibrary.LibraryFolders("""
            "LibraryFolders"
            {
            	"TimeNextStatsReport"		"1600000000"
            	"ContentStatsID"		"123"
            	"1"		"D:\\SteamLibrary"
            }
            """);

        Assert.Equal([@"D:\SteamLibrary"], folders);
    }

    [Fact]
    public void ParseManifest_reads_a_game()
    {
        var game = SteamLibrary.ParseManifest(Manifest(550, "Left 4 Dead 2", "Left 4 Dead 2"), @"C:\Steam\steamapps");

        Assert.Equal(new SteamGame(550, "Left 4 Dead 2", @"C:\Steam\steamapps\common\Left 4 Dead 2", Installed: true), game);
    }

    [Fact]
    public void ParseManifest_marks_a_game_that_is_still_downloading_as_not_installed()
    {
        Assert.False(SteamLibrary.ParseManifest(Manifest(620, "Portal 2", "Portal 2", stateFlags: 1026), "x")!.Installed);
    }

    [Fact]
    public void ParseManifest_skips_redistributables_and_incomplete_entries()
    {
        Assert.Null(SteamLibrary.ParseManifest(Manifest(228980, "Steamworks Common Redistributables", "Steamworks Shared"), "x"));
        Assert.Null(SteamLibrary.ParseManifest("\"AppState\" { \"appid\" \"10\" }", "x"));
        Assert.Null(SteamLibrary.ParseManifest("\"Other\" { }", "x"));
    }

    [Fact]
    public void Scan_collects_games_from_every_folder_and_survives_a_broken_manifest()
    {
        var root = Directory.CreateTempSubdirectory("gamingos-steam-").FullName;
        try
        {
            var main = Path.Combine(root, "Steam");
            var second = Path.Combine(root, "Second");
            Directory.CreateDirectory(Path.Combine(main, "steamapps"));
            Directory.CreateDirectory(Path.Combine(second, "steamapps"));
            var missing = Path.Combine(root, "Unplugged");

            File.WriteAllText(Path.Combine(main, "steamapps", "libraryfolders.vdf"), $$"""
                "libraryfolders"
                {
                	"0" { "path" "{{main.Replace(@"\", @"\\")}}" }
                	"1" { "path" "{{second.Replace(@"\", @"\\")}}" }
                	"2" { "path" "{{missing.Replace(@"\", @"\\")}}" }
                }
                """);
            File.WriteAllText(Path.Combine(main, "steamapps", "appmanifest_620.acf"), Manifest(620, "Portal 2", "Portal 2"));
            File.WriteAllText(Path.Combine(main, "steamapps", "appmanifest_1.acf"), "\"AppState\" {");
            File.WriteAllText(Path.Combine(second, "steamapps", "appmanifest_550.acf"), Manifest(550, "Left 4 Dead 2", "Left 4 Dead 2"));

            var games = SteamLibrary.Scan(main);

            Assert.Equal(["Left 4 Dead 2", "Portal 2"], games.Select(g => g.Name));
            Assert.StartsWith(second, games[0].InstallPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LaunchUri_accepts_only_installed_games_found_on_this_pc()
    {
        SteamGame[] known = [new(550, "Left 4 Dead 2", "x", Installed: true), new(620, "Portal 2", "y", Installed: false)];

        Assert.Equal("steam://rungameid/550", LibraryModule.LaunchUri(550, known));
        Assert.Throws<ArgumentException>(() => LibraryModule.LaunchUri(999, known));
        Assert.Throws<InvalidOperationException>(() => LibraryModule.LaunchUri(620, known));
    }

    [Fact]
    public void Artwork_is_found_directly_or_in_a_hashed_subfolder_and_missing_art_is_null()
    {
        var cache = Directory.CreateTempSubdirectory("gamingos-art-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(cache, "550"));
            File.WriteAllText(Path.Combine(cache, "550", "library_600x900.jpg"), "");
            Directory.CreateDirectory(Path.Combine(cache, "730", "abc123"));
            File.WriteAllText(Path.Combine(cache, "730", "abc123", "library_capsule.jpg"), "");
            File.WriteAllText(Path.Combine(cache, "730", "abc123", "library_hero.jpg"), "");

            var direct = LibraryModule.ToDto(new SteamGame(550, "A", "x", true), cache);
            var hashed = LibraryModule.ToDto(new SteamGame(730, "B", "x", true), cache);
            var none = LibraryModule.ToDto(new SteamGame(999, "C", "x", true), cache);

            Assert.Equal("https://steam-art.gamingos.local/550/library_600x900.jpg", direct.CoverUrl);
            Assert.Null(direct.HeroUrl);
            Assert.Equal("https://steam-art.gamingos.local/730/abc123/library_capsule.jpg", hashed.CoverUrl);
            Assert.Equal("https://steam-art.gamingos.local/730/abc123/library_hero.jpg", hashed.HeroUrl);
            Assert.Null(hashed.LogoUrl);
            Assert.Null(none.CoverUrl);
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public void Playtime_reads_minutes_per_game_and_ignores_everything_else()
    {
        var minutes = SteamPlaytime.Parse("""
            "UserLocalConfigStore"
            {
            	"friends" { "123" { "name" "someone" } }
            	"Software"
            	{
            		"Valve"
            		{
            			"Steam"
            			{
            				"apps"
            				{
            					"730" { "LastPlayed" "1795474927" "Playtime" "6123" "cloud" { "last_sync_state" "synchronized" } }
            					"550" { "LastPlayed" "1700000000" }
            					"620" { "Playtime" "abc" }
            					"notanumber" { "Playtime" "5" }
            				}
            			}
            		}
            	}
            }
            """);

        Assert.Equal(new Dictionary<long, int> { [730] = 6123 }, minutes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\"UserLocalConfigStore\" { \"Software\" { } }")]
    [InlineData("\"UserLocalConfigStore\" {")]
    public void Playtime_is_empty_when_the_file_has_no_usable_data(string text) => Assert.Empty(SteamPlaytime.Parse(text));

    [Fact]
    public void ToDto_gives_the_ui_a_steam_game_with_a_stable_colour()
    {
        var dto = LibraryModule.ToDto(new SteamGame(550, "Left 4 Dead 2", "x", Installed: true));

        Assert.Equal((550, "Left 4 Dead 2", "steam", true, null), (dto.Id, dto.Title, dto.Platform, dto.Installed, dto.PlaytimeMinutes));
        Assert.Equal("hsl(110 85% 62%)", dto.AccentColor);
        Assert.Equal(90, LibraryModule.ToDto(new SteamGame(550, "Left 4 Dead 2", "x", Installed: true), playtimeMinutes: 90).PlaytimeMinutes);
    }
}
