using System.Net.Http.Json;
using Microsoft.Data.Sqlite;

public class ApiSync
{
    private static readonly HttpClient client = new HttpClient();
    private const string BaseUrl = "https://api.pokemontcg.io/v2";

    public static async Task SyncSet(string setId)
    {
        Console.WriteLine($"Fetching cards for set: {setId}");

        var response = await client.GetFromJsonAsync<PokemonApiResponse>(
            $"{BaseUrl}/cards?q=set.id:{setId}&pageSize=250"
        );

        if (response?.Data == null)
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
        setCommand.Parameters.AddWithValue("$name", response.Data[0].Set?.Name ?? setId);
        setCommand.Parameters.AddWithValue("$total", response.Data.Count);
        setCommand.Parameters.AddWithValue("$lastSynced", DateTime.UtcNow.ToString("o"));
        setCommand.ExecuteNonQuery();

        foreach (var card in response.Data)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO Cards (id, set_id, name, number, image_url, rarity)
                VALUES ($id, $setId, $name, $number, $imageUrl, $rarity)
            ";
            command.Parameters.AddWithValue("$id", card.Id);
            command.Parameters.AddWithValue("$setId", setId);
            command.Parameters.AddWithValue("$name", card.Name);
            command.Parameters.AddWithValue("$number", card.Number);
            command.Parameters.AddWithValue("$imageUrl", card.Images?.Small ?? "");
            command.Parameters.AddWithValue("$rarity", card.Rarity ?? "");
            command.ExecuteNonQuery();
        }

        Console.WriteLine($"Synced {response.Data.Count} cards for set {setId}");
    }
}

public class PokemonApiResponse
{
    public List<PokemonCard> Data { get; set; } = new();
}

public class PokemonCard
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Number { get; set; } = "";
    public string? Rarity { get; set; }
    public CardImages? Images { get; set; }
    public CardSet? Set { get; set; }    // <- added
}

public class CardImages
{
    public string? Small { get; set; }
    public string? Large { get; set; }
}

public class CardSet
{
    public string? Name { get; set; }
    public int? Total { get; set; }   // must be int, not string
}