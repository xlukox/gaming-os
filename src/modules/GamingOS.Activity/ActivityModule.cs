using GamingOS.Core;

namespace GamingOS.Activity;

/// <summary>Watches which games are running and records play sessions in the background.</summary>
public static class ActivityModule
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RefreshGamesEvery = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Starts the background checks. <paramref name="installedGames"/> comes from the library; it is asked
    /// once a minute, so a newly installed game is noticed within a minute.
    /// </summary>
    public static void Start(Database db, Func<IReadOnlyList<InstalledGame>> installedGames)
    {
        db.Migrate("activity", SessionTracker.Migrations);
        var tracker = new SessionTracker(db);

        _ = Task.Run(async () =>
        {
            IReadOnlyList<InstalledGame> games = [];
            var gamesAt = DateTimeOffset.MinValue;
            using var timer = new PeriodicTimer(CheckEvery);
            do
            {
                try
                {
                    var now = DateTimeOffset.UtcNow;
                    if (now - gamesAt >= RefreshGamesEvery)
                    {
                        games = installedGames();
                        gamesAt = now;
                    }
                    tracker.Tick(games, RunningPrograms.Paths(), now);
                }
                catch (Exception)
                {
                    // One failed check (a locked file, a game uninstalled mid-check) must not stop tracking for good.
                }
            }
            while (await timer.WaitForNextTickAsync());
        });
    }
}
