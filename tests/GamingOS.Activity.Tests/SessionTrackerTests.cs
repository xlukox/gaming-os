using GamingOS.Activity;
using GamingOS.Core;
using Microsoft.Data.Sqlite;

namespace GamingOS.Activity.Tests;

public sealed class SessionTrackerTests : IDisposable
{
    private static readonly InstalledGame Portal = new(400, "steam", @"C:\Games\Portal");
    private static readonly InstalledGame Portal2 = new(620, "steam", @"C:\Games\Portal 2\");
    private static readonly IReadOnlyList<InstalledGame> Games = [Portal, Portal2];
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 18, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Directory.CreateTempSubdirectory("gamingos-activity-").FullName;
    private readonly Database _db;

    public SessionTrackerTests()
    {
        _db = new Database(Path.Combine(_folder, "gamingos.db"));
        _db.Migrate("activity", SessionTracker.Migrations);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    private List<(long Game, TimeSpan Length)> Sessions()
    {
        using var connection = _db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT game_id, started_at, ended_at FROM act_sessions ORDER BY id";
        using var reader = command.ExecuteReader();
        var sessions = new List<(long, TimeSpan)>();
        while (reader.Read())
            sessions.Add((reader.GetInt64(0), DateTimeOffset.Parse(reader.GetString(2)) - DateTimeOffset.Parse(reader.GetString(1))));
        return sessions;
    }

    [Fact]
    public void Running_matches_programs_inside_the_install_folder_only()
    {
        var running = SessionTracker.Running(Games, [@"c:\games\portal 2\bin\portal2.exe", @"C:\Games\PortalHub\hub.exe", @"C:\Windows\explorer.exe"]);

        Assert.Equal([Portal2], running); // case-insensitive, and "Portal" does not match "Portal 2" or "PortalHub"
    }

    [Fact]
    public void A_session_lasts_from_first_to_last_check_that_saw_the_game()
    {
        var tracker = new SessionTracker(_db);
        string[] playing = [@"C:\Games\Portal 2\portal2.exe"];

        tracker.Tick(Games, [], Start);
        tracker.Tick(Games, playing, Start.AddSeconds(5));
        tracker.Tick(Games, playing, Start.AddMinutes(40));
        tracker.Tick(Games, [], Start.AddMinutes(40).AddSeconds(5));
        tracker.Tick(Games, [], Start.AddHours(2));

        Assert.Equal([(620L, TimeSpan.FromMinutes(40) - TimeSpan.FromSeconds(5))], Sessions());
    }

    [Fact]
    public void Playing_again_later_is_a_new_session_but_a_quick_restart_of_the_app_is_not()
    {
        string[] playing = [@"C:\Games\Portal\hl2.exe"];
        var tracker = new SessionTracker(_db);
        tracker.Tick(Games, playing, Start);
        tracker.Tick(Games, playing, Start.AddMinutes(10));

        // Gaming OS restarted while the game kept running: same session continues.
        var restarted = new SessionTracker(_db);
        restarted.Tick(Games, playing, Start.AddMinutes(10).AddSeconds(20));
        restarted.Tick(Games, [], Start.AddMinutes(11));

        // Played again in the evening: a new session.
        restarted.Tick(Games, playing, Start.AddHours(3));

        Assert.Equal([(400L, TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(20)), (400L, TimeSpan.Zero)], Sessions());
    }

    [Fact]
    public void Two_games_at_once_get_their_own_sessions()
    {
        new SessionTracker(_db).Tick(Games, [@"C:\Games\Portal\hl2.exe", @"C:\Games\Portal 2\portal2.exe"], Start);

        Assert.Equal([400L, 620L], Sessions().Select(s => s.Game));
    }

    [Fact]
    public void RunningPrograms_finds_this_test_process()
    {
        Assert.Equal(Environment.ProcessPath, RunningPrograms.PathOf(Environment.ProcessId), ignoreCase: true);
        Assert.Contains(RunningPrograms.Paths(), path => string.Equals(path, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase));
        Assert.Null(RunningPrograms.PathOf(0)); // the system idle process cannot be opened
    }
}
