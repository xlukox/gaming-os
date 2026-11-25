using Microsoft.Data.Sqlite;

namespace GamingOS.Core;

/// <summary>
/// The local SQLite database (docs/PLAN.md 4). Each module owns its tables and its numbered migrations,
/// so two people never compete for the same migration number.
/// </summary>
public sealed class Database
{
    private readonly string _connectionString;

    public Database(string file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = file, Pooling = true }.ToString();

        using var connection = Open();
        // WAL: the UI can read while the activity tracker writes. Stored in the file, so set once.
        Execute(connection, "PRAGMA journal_mode = WAL");
        Execute(connection, "CREATE TABLE IF NOT EXISTS schema_versions (module TEXT PRIMARY KEY, version INTEGER NOT NULL)");
    }

    /// <summary>A new open connection. Dispose it; connections are pooled, so this is cheap.</summary>
    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Applies the module's migrations it has not run yet, in order. Step N is the SQL for version N.
    /// Never edit or reorder a step once it is merged: add a new one instead.
    /// </summary>
    public void Migrate(string module, params string[] steps)
    {
        using var connection = Open();
        using var read = connection.CreateCommand();
        read.CommandText = "SELECT version FROM schema_versions WHERE module = $module";
        read.Parameters.AddWithValue("$module", module);
        var current = Convert.ToInt32(read.ExecuteScalar() ?? 0L);

        for (var version = current + 1; version <= steps.Length; version++)
        {
            // Each step and its version number land together or not at all.
            using var transaction = connection.BeginTransaction();
            Execute(connection, steps[version - 1], transaction);
            using var write = connection.CreateCommand();
            write.Transaction = transaction;
            write.CommandText = "INSERT INTO schema_versions (module, version) VALUES ($module, $version) " +
                                "ON CONFLICT (module) DO UPDATE SET version = excluded.version";
            write.Parameters.AddWithValue("$module", module);
            write.Parameters.AddWithValue("$version", version);
            write.ExecuteNonQuery();
            transaction.Commit();
        }
    }

    private static void Execute(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
