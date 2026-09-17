using Microsoft.Data.Sqlite;

public static class SyncLimiter
{
    private const int SyncLimit = 10;
    private static readonly TimeSpan WindowDuration = TimeSpan.FromHours(4);

    public record LimitStatus(bool Allowed, int RemainingToday, DateTime ResetsAt);

    // Call this ONLY when a sync is actually about to hit a live third-party
    // API — not for a set someone else already synced, since re-adding an
    // already-cached set is a free local read with zero API impact.
    public static LimitStatus CheckAndIncrement(string clientId)
    {
        using var connection = Database.GetConnection();
        var now = DateTime.UtcNow;
        var (windowStart, currentCount) = GetCurrentWindow(connection, clientId, now);

        if (currentCount >= SyncLimit)
        {
            return new LimitStatus(false, 0, windowStart.Add(WindowDuration));
        }

        currentCount++;

        var upsertCommand = connection.CreateCommand();
        upsertCommand.CommandText = @"
            INSERT INTO SyncLimitTracker (client_id, window_start, sync_count)
            VALUES ($clientId, $windowStart, $count)
            ON CONFLICT(client_id) DO UPDATE SET window_start = $windowStart, sync_count = $count
        ";
        upsertCommand.Parameters.AddWithValue("$clientId", clientId);
        upsertCommand.Parameters.AddWithValue("$windowStart", windowStart.ToString("o"));
        upsertCommand.Parameters.AddWithValue("$count", currentCount);
        upsertCommand.ExecuteNonQuery();

        return new LimitStatus(true, SyncLimit - currentCount, windowStart.Add(WindowDuration));
    }

    // Read-only check, for displaying "X syncs left" without consuming one.
    public static LimitStatus GetStatus(string clientId)
    {
        using var connection = Database.GetConnection();
        var now = DateTime.UtcNow;
        var (windowStart, currentCount) = GetCurrentWindow(connection, clientId, now);

        return new LimitStatus(currentCount < SyncLimit, Math.Max(0, SyncLimit - currentCount), windowStart.Add(WindowDuration));
    }

    // Shared logic: figures out whether the client's existing window is
    // still active or has expired (24 hours since IT started, not
    // midnight), without writing anything — used by both methods above.
    private static (DateTime windowStart, int count) GetCurrentWindow(SqliteConnection connection, string clientId, DateTime now)
    {
        var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = "SELECT window_start, sync_count FROM SyncLimitTracker WHERE client_id = $clientId";
        selectCommand.Parameters.AddWithValue("$clientId", clientId);

        DateTime? windowStart = null;
        int count = 0;

        using (var reader = selectCommand.ExecuteReader())
        {
            if (reader.Read())
            {
                windowStart = DateTime.Parse(reader.GetString(0)).ToUniversalTime();
                count = reader.GetInt32(1);
            }
        }

        bool expired = windowStart == null || (now - windowStart.Value) >= WindowDuration;
        return expired ? (now, 0) : (windowStart!.Value, count);
    }
}