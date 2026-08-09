using Microsoft.Data.Sqlite;

public class Database
{
    private const string ConnectionString = "Data Source=tcgrider.db";

    public static void Initialize()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Sets (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                total INTEGER NOT NULL,
                logo_url TEXT,
                last_synced TEXT
            );

            CREATE TABLE IF NOT EXISTS Cards (
                id TEXT PRIMARY KEY,
                set_id TEXT NOT NULL,
                name TEXT NOT NULL,
                number TEXT NOT NULL,
                image_url TEXT,
                rarity TEXT,
                FOREIGN KEY (set_id) REFERENCES Sets(id)
            );

            CREATE TABLE IF NOT EXISTS Collection (
                card_id TEXT NOT NULL,
                collected INTEGER DEFAULT 0,
                date_collected TEXT,
                PRIMARY KEY (card_id),
                FOREIGN KEY (card_id) REFERENCES Cards(id)
            );
        ";
        command.ExecuteNonQuery();
        Console.WriteLine("Database initialized successfully");
    }

    public static SqliteConnection GetConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        return connection;
    }
}