using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using System.IO;

public class ApiSync
{
    private static readonly HttpClient client = new HttpClient();

    public static async Task SyncSet(string setId)
    {
        Console.WriteLine($"Fetching cards for set: {setId}");

        var set = await client.GetFromJsonAsync<TcgdexSet>(
            $"https://api.tcgdex.net/v2/en/sets/{setId}"
        );

        if (set?.Cards == null)
        {
            Console.WriteLine("No data returned from API");
            return;
        }

        using var connection = Database.GetConnection();

        var setCommand = connection.CreateCommand();
        setCommand.CommandText = @"
            INSERT OR IGNORE INTO Sets (id, name, total, last_synced)
            VALUES ($id, $name, $total, $lastSynced)
        ";
        setCommand.Parameters.AddWithValue("$id", setId);
        setCommand.Parameters.AddWithValue("$name", set.Name ?? setId);
        setCommand.Parameters.AddWithValue("$total", set.Cards.Count);
        setCommand.Parameters.AddWithValue("$lastSynced", DateTime.UtcNow.ToString("o"));
        setCommand.ExecuteNonQuery();

        int count = 0;
        foreach (var cardBrief in set.Cards)
        {
            count++;
            string rarity = "";

            try
            {
                var fullCard = await client.GetFromJsonAsync<TcgdexCardFull>(
                    $"https://api.tcgdex.net/v2/en/cards/{cardBrief.Id}"
                );
                rarity = fullCard?.Rarity ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to fetch rarity for {cardBrief.Id}: {ex.Message}");
            }

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO Cards (id, set_id, name, number, image_url, rarity)
                VALUES ($id, $setId, $name, $number, $imageUrl, $rarity)
            ";
            command.Parameters.AddWithValue("$id", cardBrief.Id);
            command.Parameters.AddWithValue("$setId", setId);
            command.Parameters.AddWithValue("$name", cardBrief.Name);
            command.Parameters.AddWithValue("$number", cardBrief.LocalId);
            command.Parameters.AddWithValue("$imageUrl", cardBrief.Image != null ? $"{cardBrief.Image}/low.png" : "");
            command.Parameters.AddWithValue("$rarity", rarity);
            command.ExecuteNonQuery();

            if (count % 20 == 0)
                Console.WriteLine($"  ...{count}/{set.Cards.Count} cards processed");
        }

        Console.WriteLine($"Synced {set.Cards.Count} cards for set {setId}");
    }
}

public class TcgdexSet
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<TcgdexCardBrief>? Cards { get; set; }
}

public class TcgdexCardBrief
{
    public string Id { get; set; } = "";
    public string LocalId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Image { get; set; }
}

public class TcgdexCardFull
{
    public string Id { get; set; } = "";
    public string? Rarity { get; set; }
}

public class TcgdexSetBrief
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}