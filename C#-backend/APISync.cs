using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using System.IO;

public class ApiSync
{

    private static readonly HttpClient client = new HttpClient();
    private const string BaseUrl = "https://api.pokemontcg.io/v2";

    static ApiSync()
    {
        client.DefaultRequestHeaders.Add("X-Api-Key", Environment.GetEnvironmentVariable("POKEMON_TCG_API_KEY"));

    }

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

    foreach (var card in set.Cards)
    {
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR IGNORE INTO Cards (id, set_id, name, number, image_url, rarity)
            VALUES ($id, $setId, $name, $number, $imageUrl, $rarity)
        ";
        command.Parameters.AddWithValue("$id", card.Id);
        command.Parameters.AddWithValue("$setId", setId);
        command.Parameters.AddWithValue("$name", card.Name);
        command.Parameters.AddWithValue("$number", card.LocalId);
        command.Parameters.AddWithValue("$imageUrl", card.Image != null ? $"{card.Image}/low.png" : "");
        command.Parameters.AddWithValue("$rarity", ""); // not available without a per-card fetch — see note above
        command.ExecuteNonQuery();
    }

    Console.WriteLine($"Synced {set.Cards.Count} cards for set {setId}");
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

public class TcgdexSetBrief
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}