using Microsoft.Data.Sqlite;

namespace TherapyNotesDemo.Notifications.Worker;

public sealed class IdempotencyStore
{
    private readonly string _connectionString;

    public IdempotencyStore(IHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "data");
        Directory.CreateDirectory(dir);

        var dbPath = Path.Combine(dir, "worker.db");
        _connectionString = $"Data Source={dbPath}";

        EnsureCreated();
    }

    public async Task<bool> IsProcessedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM processed_messages WHERE id = $id LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", messageId.ToString("D"));

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is not null && result is not DBNull;
    }

    public async Task MarkProcessedAsync(Guid messageId, string type, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT OR IGNORE INTO processed_messages (id, processed_at, type, occurred_at)
VALUES ($id, $processed_at, $type, $occurred_at);";

        cmd.Parameters.AddWithValue("$id", messageId.ToString("D"));
        cmd.Parameters.AddWithValue("$processed_at", DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$type", type);
        cmd.Parameters.AddWithValue("$occurred_at", occurredAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private void EnsureCreated()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS processed_messages (
  id TEXT PRIMARY KEY,
  processed_at TEXT NOT NULL,
  type TEXT NOT NULL,
  occurred_at TEXT NOT NULL
);";
        cmd.ExecuteNonQuery();
    }
}

