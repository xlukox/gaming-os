using System.Globalization;
using GamingOS.Core;

namespace GamingOS.Activity;

/// <summary>
/// Records play sessions (docs/PLAN.md 5.4): a game is being played while any program from its install folder runs.
/// Works for every platform and for games started outside Gaming OS.
/// </summary>
public sealed class SessionTracker(Database db)
{
    public static readonly string[] Migrations =
    [
        """
        CREATE TABLE act_sessions (
            id         INTEGER PRIMARY KEY,
            platform   TEXT NOT NULL,
            game_id    INTEGER NOT NULL,
            started_at TEXT NOT NULL,
            ended_at   TEXT NOT NULL -- last time the game was seen running, updated on every check
        );
        CREATE INDEX act_sessions_game ON act_sessions (platform, game_id, ended_at);
        """,
    ];

    /// <summary>A game seen again within this time continues its session instead of starting a new one.</summary>
    public static readonly TimeSpan ResumeWithin = TimeSpan.FromSeconds(30);

    private readonly Dictionary<InstalledGame, long> _open = [];

    /// <summary>
    /// One check. Updates the end of sessions still running and opens sessions for games that started.
    /// The end time is written on every check, so a crash or power loss costs at most one interval.
    /// </summary>
    public void Tick(IReadOnlyList<InstalledGame> games, IEnumerable<string> runningExecutables, DateTimeOffset now)
    {
        var running = Running(games, runningExecutables);
        var at = Timestamp(now);

        using var connection = db.Open();
        using var transaction = connection.BeginTransaction();
        foreach (var game in running)
        {
            if (!_open.TryGetValue(game, out var sessionId))
            {
                using var resume = connection.CreateCommand();
                resume.Transaction = transaction;
                resume.CommandText = "SELECT id FROM act_sessions WHERE platform = $p AND game_id = $g AND ended_at >= $since ORDER BY id DESC LIMIT 1";
                resume.Parameters.AddWithValue("$p", game.Platform);
                resume.Parameters.AddWithValue("$g", game.Id);
                resume.Parameters.AddWithValue("$since", Timestamp(now - ResumeWithin));

                if (resume.ExecuteScalar() is long previous)
                {
                    sessionId = previous;
                }
                else
                {
                    using var insert = connection.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText = "INSERT INTO act_sessions (platform, game_id, started_at, ended_at) VALUES ($p, $g, $at, $at) RETURNING id";
                    insert.Parameters.AddWithValue("$p", game.Platform);
                    insert.Parameters.AddWithValue("$g", game.Id);
                    insert.Parameters.AddWithValue("$at", at);
                    sessionId = (long)insert.ExecuteScalar()!;
                }
                _open[game] = sessionId;
            }

            using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE act_sessions SET ended_at = $at WHERE id = $id";
            update.Parameters.AddWithValue("$at", at);
            update.Parameters.AddWithValue("$id", sessionId);
            update.ExecuteNonQuery();
        }
        transaction.Commit();

        // A game no longer running keeps the end time of the last check that saw it.
        foreach (var stopped in _open.Keys.Except(running).ToList()) _open.Remove(stopped);
    }

    /// <summary>Games with at least one program running from inside their install folder.</summary>
    public static HashSet<InstalledGame> Running(IReadOnlyList<InstalledGame> games, IEnumerable<string> runningExecutables)
    {
        // The trailing separator keeps "C:\Games\Portal" from matching "C:\Games\Portal 2\portal2.exe".
        var folders = games
            .Where(game => !string.IsNullOrWhiteSpace(game.InstallPath))
            .Select(game => (game, folder: Path.TrimEndingDirectorySeparator(Path.GetFullPath(game.InstallPath)) + Path.DirectorySeparatorChar))
            .ToList();

        var running = new HashSet<InstalledGame>();
        foreach (var exe in runningExecutables)
            foreach (var (game, folder) in folders)
                if (exe.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) running.Add(game);
        return running;
    }

    // Fixed-length UTC text, so comparing the stored strings compares the times.
    private static string Timestamp(DateTimeOffset time) => time.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
