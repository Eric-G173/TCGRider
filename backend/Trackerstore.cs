using Microsoft.Data.Sqlite;

public static class TrackerStore
{
    public static List<object> GetTrackers(string clientId)
    {
        using var connection = Database.GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT set_id, name, game
            FROM UserTrackers
            WHERE client_id = $clientId
            ORDER BY position
        ";
        command.Parameters.AddWithValue("$clientId", clientId);

        var trackers = new List<object>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            trackers.Add(new
            {
                setID = reader.GetString(0),
                name = reader.GetString(1),
                game = reader.GetString(2)
            });
        }
        return trackers;
    }

    public static void AddTracker(string clientId, string setId, string name, string game)
    {
        using var connection = Database.GetConnection();

        // New tracker goes at the end of whatever this client already has
        var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM UserTrackers WHERE client_id = $clientId";
        countCommand.Parameters.AddWithValue("$clientId", clientId);
        int nextPosition = Convert.ToInt32(countCommand.ExecuteScalar());

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR IGNORE INTO UserTrackers (client_id, set_id, name, game, position)
            VALUES ($clientId, $setId, $name, $game, $position)
        ";
        command.Parameters.AddWithValue("$clientId", clientId);
        command.Parameters.AddWithValue("$setId", setId);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$game", game);
        command.Parameters.AddWithValue("$position", nextPosition);
        command.ExecuteNonQuery();
    }

    public static void RemoveTracker(string clientId, string setId)
    {
        using var connection = Database.GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM UserTrackers WHERE client_id = $clientId AND set_id = $setId";
        command.Parameters.AddWithValue("$clientId", clientId);
        command.Parameters.AddWithValue("$setId", setId);
        command.ExecuteNonQuery();
    }

    public static void ReorderTrackers(string clientId, List<string> orderedSetIds)
    {
        using var connection = Database.GetConnection();
        using var transaction = connection.BeginTransaction();

        for (int i = 0; i < orderedSetIds.Count; i++)
        {
            var command = connection.CreateCommand();
            command.Transaction = transaction; // required, or this runs outside the transaction
            command.CommandText = @"
                UPDATE UserTrackers SET position = $position
                WHERE client_id = $clientId AND set_id = $setId
            ";
            command.Parameters.AddWithValue("$position", i);
            command.Parameters.AddWithValue("$clientId", clientId);
            command.Parameters.AddWithValue("$setId", orderedSetIds[i]);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}

public class TrackerRequest
{
    public string ClientId { get; set; } = "";
    public string SetId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Game { get; set; } = "";
}

public class ReorderRequest
{
    public string ClientId { get; set; } = "";
    public List<string> OrderedSetIds { get; set; } = new();
}