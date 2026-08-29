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

            CREATE TABLE IF NOT EXISTS UserTrackers (
                client_id TEXT NOT NULL,
                set_id TEXT NOT NULL,
                name TEXT NOT NULL,
                game TEXT NOT NULL,
                position INTEGER NOT NULL,
                PRIMARY KEY (client_id, set_id)
            );

            -- The full browsable list of sets per game — populated once via
            -- POST /api/admin/seed-catalog, then read from locally instead
            -- of hitting TCGdex/OPTCG live on every browse. Distinct from
            -- Sets, which only holds sets a user has actually synced.
            CREATE TABLE IF NOT EXISTS SetCatalog (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                game TEXT NOT NULL,
                sort_order INTEGER NOT NULL
            );

            -- Collection previously had no client_id, meaning every visitor
            -- to the shared web deployment would collide on the same rows.
            -- Safe to drop and recreate: this table has never actually been
            -- written to anywhere in the app, so there's no real data here.
            DROP TABLE IF EXISTS Collection;
            CREATE TABLE IF NOT EXISTS Collection (
                client_id TEXT NOT NULL,
                card_id TEXT NOT NULL,
                collected INTEGER DEFAULT 0,
                date_collected TEXT,
                PRIMARY KEY (client_id, card_id),
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