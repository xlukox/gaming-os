using GamingOS.Core;
using Microsoft.Data.Sqlite;

namespace GamingOS.Core.Tests;

public sealed class DatabaseTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("gamingos-db-").FullName;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools(); // release the file so the folder can be deleted
        Directory.Delete(_folder, recursive: true);
    }

    private long Count(Database db, string table)
    {
        using var connection = db.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)command.ExecuteScalar()!;
    }

    [Fact]
    public void Migrations_run_once_and_later_steps_are_added_on_the_next_start()
    {
        var file = Path.Combine(_folder, "nested", "gamingos.db");
        string[] v1 = ["CREATE TABLE x_items (id INTEGER PRIMARY KEY)"];

        var db = new Database(file);
        db.Migrate("x", v1);
        db.Migrate("x", v1); // running again must not fail on the existing table

        using (var connection = db.Open())
        using (var insert = connection.CreateCommand())
        {
            insert.CommandText = "INSERT INTO x_items DEFAULT VALUES";
            insert.ExecuteNonQuery();
        }

        new Database(file).Migrate("x", [.. v1, "ALTER TABLE x_items ADD COLUMN name TEXT"]);

        Assert.Equal(1, Count(db, "x_items")); // data survived the upgrade
        using var check = db.Open();
        using var columns = check.CreateCommand();
        columns.CommandText = "SELECT COUNT(*) FROM pragma_table_info('x_items') WHERE name = 'name'";
        Assert.Equal(1L, columns.ExecuteScalar());
    }

    [Fact]
    public void Each_module_counts_its_own_versions()
    {
        var db = new Database(Path.Combine(_folder, "gamingos.db"));
        db.Migrate("a", "CREATE TABLE a_one (id INTEGER)");
        db.Migrate("b", "CREATE TABLE b_one (id INTEGER)", "CREATE TABLE b_two (id INTEGER)");

        Assert.Equal(2, Count(db, "schema_versions"));
        Assert.Equal(0, Count(db, "a_one"));
        Assert.Equal(0, Count(db, "b_two"));
    }

    [Fact]
    public void A_failing_step_leaves_the_version_unchanged_so_it_is_retried()
    {
        var db = new Database(Path.Combine(_folder, "gamingos.db"));

        Assert.Throws<SqliteException>(() => db.Migrate("x", "CREATE TABLE x_ok (id INTEGER)", "NOT VALID SQL"));
        db.Migrate("x", "CREATE TABLE x_ok (id INTEGER)", "CREATE TABLE x_fixed (id INTEGER)");

        Assert.Equal(0, Count(db, "x_fixed"));
    }
}
