using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Linq;

public class ApiSync
{
    private static readonly HttpClient client = new HttpClient();

    // Pulls the release-order number out of a TCGdex set id (base1 -> 1,
    // neo4 -> 4). Ids with no number (promos, one-off specials) sort last.
    // Pulled out as its own method specifically so it's unit-testable —
    // it used to be an inline lambda inside the /api/sets/pokemon route.
    public static int ExtractSetNumber(string setId)
    {
        var match = System.Text.RegularExpressions.Regex.Match(setId, @"\d+");
        return match.Success ? int.Parse(match.Value) : int.MaxValue;
    }

    // Checked before either sync method makes any network calls at all —
    // if the set's already in the DB with real cards, there's no reason to
    // hit the API. A set with total = 0 is treated as NOT synced, so a
    // manual retry is still possible if upstream data ever gets fixed.
    private static bool SetAlreadySynced(string setId)
    {
        using var connection = Database.GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT total FROM Sets WHERE id = $id LIMIT 1";
        command.Parameters.AddWithValue("$id", setId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return false;
        return reader.GetInt32(0) > 0;
    }

    // Records that a set was actually attempted and confirmed to have no
    // cards — this is what lets /api/sets/* permanently filter it out of
    // the browse list going forward, since TCGdex's own cardCount metadata
    // isn't reliable enough on its own to catch cases like this.
    private static void MarkSetAsEmpty(string setId, string name)
    {
        using var connection = Database.GetConnection();
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR IGNORE INTO Sets (id, name, total, last_synced)
            VALUES ($id, $name, 0, $lastSynced)
        ";
        command.Parameters.AddWithValue("$id", setId);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$lastSynced", DateTime.UtcNow.ToString("o"));
        command.ExecuteNonQuery();
    }

    // ─────────────────────────────────────────
    // POKÉMON (TCGdex)
    // ─────────────────────────────────────────

    // Returns true if the set has real card data available (whether it was
    // just freshly synced or already in the DB), false if TCGdex has no
    // cards for it — the frontend uses this to decide whether to add it as
    // a tracker at all.
    public static async Task<bool> SyncPokemonSet(string setId)
    {
        if (SetAlreadySynced(setId))
        {
            Console.WriteLine($"Set {setId} already synced — skipping API fetch");
            return true;
        }

        Console.WriteLine($"Fetching cards for set: {setId}");

        var set = await client.GetFromJsonAsync<TcgdexSet>(
            $"https://api.eu1.tcgdex.net/v2/en/sets/{setId}"
        );

        if (set?.Cards == null || set.Cards.Count == 0)
        {
            Console.WriteLine($"Set {setId} has no card data available — marking as empty");
            MarkSetAsEmpty(setId, set?.Name ?? setId);
            return false;
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

        // TCGdex's brief card list has no rarity — only the full single-card
        // endpoint does. Fetching these one at a time (the old approach) meant
        // a 100+ card set did 100+ sequential network round-trips before a
        // single row got written. Fetching in parallel, capped at 15 at once
        // so we're not hammering TCGdex's servers, turns that into a handful
        // of concurrent waves instead.
        var raritySemaphore = new SemaphoreSlim(15);
        var rarityTasks = set.Cards.Select(async cardBrief =>
        {
            await raritySemaphore.WaitAsync();
            try
            {
                var fullCard = await client.GetFromJsonAsync<TcgdexCardFull>(
                    $"https://api.eu1.tcgdex.net/v2/en/cards/{cardBrief.Id}"
                );
                return (cardBrief.Id, Rarity: fullCard?.Rarity ?? "");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to fetch rarity for {cardBrief.Id}: {ex.Message}");
                return (cardBrief.Id, Rarity: "");
            }
            finally
            {
                raritySemaphore.Release();
            }
        });

        var rarityResults = await Task.WhenAll(rarityTasks);
        var rarityById = rarityResults.ToDictionary(r => r.Id, r => r.Rarity);

        int count = 0;
        foreach (var cardBrief in set.Cards)
        {
            count++;
            string rarity = rarityById.TryGetValue(cardBrief.Id, out var r) ? r : "";

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
        return true;
    }

    // ─────────────────────────────────────────
    // ONE PIECE (OPTCG API)
    // ─────────────────────────────────────────

    public static async Task<bool> SyncOnePieceSet(string setId)
    {
        if (SetAlreadySynced(setId))
        {
            Console.WriteLine($"Set {setId} already synced — skipping API fetch");
            return true;
        }

        Console.WriteLine($"Fetching One Piece cards for set: {setId}");

        var cards = await client.GetFromJsonAsync<List<OptcgCard>>(
            $"https://optcgapi.com/api/sets/{setId}/"
        );

        if (cards == null || cards.Count == 0)
        {
            Console.WriteLine($"Set {setId} has no card data available — marking as empty");
            MarkSetAsEmpty(setId, setId); // OPTCG's set list endpoint doesn't return a name on empty results
            return false;
        }

        using var connection = Database.GetConnection();

        var setCommand = connection.CreateCommand();
        setCommand.CommandText = @"
            INSERT OR IGNORE INTO Sets (id, name, total, last_synced)
            VALUES ($id, $name, $total, $lastSynced)
        ";
        setCommand.Parameters.AddWithValue("$id", setId);
        setCommand.Parameters.AddWithValue("$name", cards[0].SetName ?? setId);
        setCommand.Parameters.AddWithValue("$total", cards.Count);
        setCommand.Parameters.AddWithValue("$lastSynced", DateTime.UtcNow.ToString("o"));
        setCommand.ExecuteNonQuery();

        int count = 0;
        foreach (var card in cards)
        {
            count++;

            string cardNumber = card.CardSetId.Contains('-')
                ? card.CardSetId.Split('-')[1]
                : card.CardSetId;

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO Cards (id, set_id, name, number, image_url, rarity)
                VALUES ($id, $setId, $name, $number, $imageUrl, $rarity)
            ";
            command.Parameters.AddWithValue("$id", card.CardSetId);
            command.Parameters.AddWithValue("$setId", setId);
            command.Parameters.AddWithValue("$name", card.CardName);
            command.Parameters.AddWithValue("$number", cardNumber);
            command.Parameters.AddWithValue("$imageUrl", card.CardImage ?? "");
            command.Parameters.AddWithValue("$rarity", card.Rarity ?? "");
            command.ExecuteNonQuery();

            if (count % 20 == 0)
                Console.WriteLine($"  ...{count}/{cards.Count} cards processed");
        }

        Console.WriteLine($"Synced {cards.Count} cards for One Piece set {setId}");
        return true;
    }

    // YGOPRODeck rate limit is 20 req/sec with a 1-hour block if exceeded —
    // a much harsher, more explicit penalty than TCGdex/OPTCG documented.
    // Downloads here are deliberately SEQUENTIAL, not parallel like the
    // TCGdex rarity fetch — a single request naturally takes longer than
    // 1/20th of a second, so this stays safely under the limit without
    // needing an explicit throttle.
    //
    // YGOPRODeck's terms explicitly forbid hotlinking images — they must
    // be downloaded and re-hosted, unlike TCGdex/OPTCG where storing just
    // the URL was fine. Downloaded images live in card-images/yugioh/ and
    // get served back out via static file middleware (see Program.cs).
    public static async Task<bool> SyncYuGiOhSet(string setName)
    {
        if (SetAlreadySynced(setName))
        {
            Console.WriteLine($"Set {setName} already synced — skipping API fetch");
            return true;
        }

        var encodedSetName = Uri.EscapeDataString(setName);
        YgoCardInfoResponse? response;
        try
        {
            response = await client.GetFromJsonAsync<YgoCardInfoResponse>(
                $"https://db.ygoprodeck.com/api/v7/cardinfo.php?cardset={encodedSetName}"
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"YGOPRODeck API error for set {setName}: {ex.GetType().Name} - {ex.Message}");
            return false;
        }

        if (response?.Data == null || response.Data.Count == 0)
        {
            Console.WriteLine($"Set {setName} has no card data available — marking as empty");
            MarkSetAsEmpty(setName, setName);
            return false;
        }

        var imagesDir = Path.Combine(Directory.GetCurrentDirectory(), "card-images", "yugioh");
        Directory.CreateDirectory(imagesDir);

        using var connection = Database.GetConnection();

        var setCommand = connection.CreateCommand();
        setCommand.CommandText = @"
            INSERT OR IGNORE INTO Sets (id, name, total, last_synced)
            VALUES ($id, $name, $total, $lastSynced)
        ";
        setCommand.Parameters.AddWithValue("$id", setName);
        setCommand.Parameters.AddWithValue("$name", setName);
        setCommand.Parameters.AddWithValue("$total", response.Data.Count);
        setCommand.Parameters.AddWithValue("$lastSynced", DateTime.UtcNow.ToString("o"));
        setCommand.ExecuteNonQuery();

        int count = 0;
        foreach (var card in response.Data)
        {
            count++;

            // A card can appear in multiple sets/reprints, each with a
            // different code and rarity — find the specific printing that
            // matches the set we're actually syncing right now.
            var setEntry = card.CardSets?.FirstOrDefault(cs =>
                cs.SetName.Equals(setName, StringComparison.OrdinalIgnoreCase));

            string cardNumber = setEntry?.SetCode ?? "";
            string rarity = setEntry?.SetRarity ?? "";
            string localImagePath = "";

            var imageUrl = card.CardImages?.FirstOrDefault()?.ImageUrl;
            if (!string.IsNullOrEmpty(imageUrl))
            {
                var fileName = $"{card.Id}.jpg";
                var filePath = Path.Combine(imagesDir, fileName);

                if (!File.Exists(filePath))
                {
                    try
                    {
                        var imageBytes = await client.GetByteArrayAsync(imageUrl);
                        await File.WriteAllBytesAsync(filePath, imageBytes);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to download image for card {card.Id}: {ex.Message}");
                    }
                }

                localImagePath = $"/card-images/yugioh/{fileName}";
            }

            var cardCommand = connection.CreateCommand();
            cardCommand.CommandText = @"
                INSERT OR IGNORE INTO Cards (id, set_id, name, number, image_url, rarity)
                VALUES ($id, $setId, $name, $number, $imageUrl, $rarity)
            ";
            cardCommand.Parameters.AddWithValue("$id", card.Id.ToString());
            cardCommand.Parameters.AddWithValue("$setId", setName);
            cardCommand.Parameters.AddWithValue("$name", card.Name);
            cardCommand.Parameters.AddWithValue("$number", cardNumber);
            cardCommand.Parameters.AddWithValue("$imageUrl", localImagePath);
            cardCommand.Parameters.AddWithValue("$rarity", rarity);
            cardCommand.ExecuteNonQuery();

            if (count % 20 == 0)
                Console.WriteLine($"  ...{count}/{response.Data.Count} cards processed for {setName}");
        }

        Console.WriteLine($"Synced {response.Data.Count} cards for Yu-Gi-Oh set {setName}");
        return true;
    }
}

// ─────────────────────────────────────────
// TCGdex (Pokémon) models
// ─────────────────────────────────────────

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

public class TcgdexCardCount
{
    public int Official { get; set; }
    public int Total { get; set; }
}

public class TcgdexSetBrief
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public TcgdexCardCount? CardCount { get; set; }
}

public class TcgdexSeriesBrief
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public class TcgdexSeriesFull
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<TcgdexSetBrief>? Sets { get; set; }
}

// ─────────────────────────────────────────
// OPTCG API (One Piece) models
// ─────────────────────────────────────────

public class OptcgSet
{
    [JsonPropertyName("set_name")]
    public string SetName { get; set; } = "";

    [JsonPropertyName("set_id")]
    public string SetId { get; set; } = "";
}

public class OptcgCard
{
    [JsonPropertyName("card_set_id")]
    public string CardSetId { get; set; } = "";

    [JsonPropertyName("set_id")]
    public string SetId { get; set; } = "";

    [JsonPropertyName("set_name")]
    public string SetName { get; set; } = "";

    [JsonPropertyName("card_name")]
    public string CardName { get; set; } = "";

    [JsonPropertyName("rarity")]
    public string? Rarity { get; set; }

    [JsonPropertyName("card_image")]
    public string? CardImage { get; set; }
}

public class YgoCardSetListing
{
    [JsonPropertyName("set_name")]
    public string SetName { get; set; } = "";

    [JsonPropertyName("set_code")]
    public string SetCode { get; set; } = "";

    [JsonPropertyName("num_of_cards")]
    public int NumOfCards { get; set; }

    [JsonPropertyName("tcg_date")]
    public string? TcgDate { get; set; }
}

public class YgoCardSetEntry
{
    [JsonPropertyName("set_name")]
    public string SetName { get; set; } = "";

    [JsonPropertyName("set_code")]
    public string SetCode { get; set; } = "";

    [JsonPropertyName("set_rarity")]
    public string SetRarity { get; set; } = "";
}

public class YgoCardImage
{
    [JsonPropertyName("image_url")]
    public string ImageUrl { get; set; } = "";
}

public class YgoCard
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    [JsonPropertyName("card_sets")]
    public List<YgoCardSetEntry>? CardSets { get; set; }

    [JsonPropertyName("card_images")]
    public List<YgoCardImage>? CardImages { get; set; }
}

public class YgoCardInfoResponse
{
    public List<YgoCard>? Data { get; set; }
}