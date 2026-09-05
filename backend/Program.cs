using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DotNetEnv;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// DynamoDB client, using credentials loaded from .env — same pattern as
// every other secret this session, never hardcoded in source.
// AWS_REGION should be set in .env to whatever region your tables were
// actually created in (e.g. "us-east-1") — a mismatch here doesn't error
// clearly, it just reports tables as "not found" even though they exist.
var awsCredentials = new BasicAWSCredentials(
    Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"),
    Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY")
);
var awsRegion = RegionEndpoint.GetBySystemName(
    Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1"
);
var dynamoClient = new AmazonDynamoDBClient(awsCredentials, awsRegion);
var dynamoContext = new DynamoDBContext(dynamoClient);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        var allowedOrigins = new List<string> { "http://localhost:3000" };

        // Set this on Render once your frontend's real URL is known —
        // avoids needing another code deploy just to update CORS.
        var deployedOrigin = Environment.GetEnvironmentVariable("FRONTEND_URL");
        if (!string.IsNullOrEmpty(deployedOrigin))
            allowedOrigins.Add(deployedOrigin);

        policy.WithOrigins(allowedOrigins.ToArray())
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();
app.UseCors("AllowReactApp");

// Serves downloaded Yu-Gi-Oh card images back out over HTTP. A file saved
// at card-images/yugioh/12345.jpg becomes reachable at
// {API_BASE_URL}/card-images/yugioh/12345.jpg — this is what makes
// self-hosting (required by YGOPRODeck's terms) actually work.
var cardImagesPath = Path.Combine(Directory.GetCurrentDirectory(), "card-images");
Directory.CreateDirectory(cardImagesPath);
app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(cardImagesPath),
    RequestPath = "/card-images"
});

// shared client — now used ONLY by the one-time /api/admin/seed-catalog
// endpoint, since the browse routes read from SetCatalog instead. A longer
// timeout is fine here: nothing user-facing waits on this anymore, and
// TCGdex's full series+sets payload has grown large enough that 15s was
// too tight even under normal conditions, not just degraded ones.
var setsHttpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(90)
};

// Sets get marked here (total = 0) once a real sync attempt confirms they
// have no actual card data — TCGdex/OPTCG's own listing metadata isn't
// reliable enough to catch this upfront, so this filters based on what's
// actually been verified rather than what the API claims.
HashSet<string> GetKnownEmptySetIds()
{
    var ids = new HashSet<string>();
    using var connection = Database.GetConnection();
    var command = connection.CreateCommand();
    command.CommandText = "SELECT id FROM Sets WHERE total = 0";
    using var reader = command.ExecuteReader();
    while (reader.Read())
        ids.Add(reader.GetString(0));
    return ids;
}

app.MapGet("/ping", () => Results.Ok(new { status = "ok", message = "C# backend running" }));

// Known chronological order of TCG eras — TCGdex's own series listing isn't
// guaranteed to come back in release order, so sets are sorted using this
// reference list instead of trusting API return order.
var pokemonSeriesOrder = new List<string> {
    "Base", "Gym", "Neo", "Legendary", "e-Card", "EX", "Diamond & Pearl",
    "Platinum", "HeartGold", "Black & White", "XY", "Sun & Moon",
    "Sword & Shield", "Scarlet & Violet"
};

app.MapGet("/api/sets/pokemon", async () => {
    // Query on the Game partition key — fast, single-partition lookup,
    // not a full table scan. SortOrder isn't the sort key, so ordering
    // happens in-memory after the fetch; fine at this scale (~200 items).
    var results = await dynamoContext.QueryAsync<DynamoSetItem>("Pokémon").GetRemainingAsync();
    var sets = results
        .OrderBy(s => s.SortOrder)
        .Select(s => new { setID = s.SetID, name = s.Name });

    return Results.Ok(sets);
});

app.MapGet("/api/sets/onepiece", async () => {
    var results = await dynamoContext.QueryAsync<DynamoSetItem>("One Piece").GetRemainingAsync();
    var sets = results
        .OrderBy(s => s.SortOrder)
        .Select(s => new { setID = s.SetID, name = s.Name });

    return Results.Ok(sets);
});

// One-time (or occasional, if you re-run it) bulk load — this is the ONLY
// place that still talks to TCGdex/OPTCG for set LISTINGS. Trigger manually
// by visiting this URL in a browser or via curl; it's idempotent, so
// running it again just refreshes the catalog rather than duplicating rows.
// NOTE: unauthenticated for now, matching the rest of this app's no-login
// design — fine for a low-stakes personal project, but anyone who finds
// the URL could trigger it. An easy hardening step later: require a
// ?key=... query param checked against an environment variable.
async Task<object> SeedCatalogAsync() {
    using var connection = Database.GetConnection();
    var clearCommand = connection.CreateCommand();
    clearCommand.CommandText = "DELETE FROM SetCatalog";
    clearCommand.ExecuteNonQuery();

    int totalSeeded = 0;
    int pokemonSeeded = 0;
    int onePieceSeeded = 0;
    var knownEmpty = GetKnownEmptySetIds();

    try
    {
        var seriesList = await setsHttpClient.GetFromJsonAsync<List<TcgdexSeriesBrief>>(
            "https://api.eu1.tcgdex.net/v2/en/series"
        );

        if (seriesList != null)
        {
            var seriesDetailTasks = seriesList.Select(async s => {
                try
                {
                    return await setsHttpClient.GetFromJsonAsync<TcgdexSeriesFull>(
                        $"https://api.eu1.tcgdex.net/v2/en/series/{s.Id}"
                    );
                }
                catch
                {
                    return null;
                }
            });
            var seriesDetails = await Task.WhenAll(seriesDetailTasks);

            var orderedSeries = seriesDetails
                .Where(s => s != null)
                .OrderBy(s => {
                    int idx = pokemonSeriesOrder.FindIndex(era =>
                        s!.Name.Contains(era, StringComparison.OrdinalIgnoreCase));
                    return idx == -1 ? int.MaxValue : idx;
                });

            int order = 0;
            foreach (var series in orderedSeries)
            {
                var setsInSeries = (series!.Sets ?? new List<TcgdexSetBrief>())
                    .Where(set => (set.CardCount?.Total ?? 0) > 0)
                    .Where(set => !knownEmpty.Contains(set.Id))
                    .OrderBy(set => ApiSync.ExtractSetNumber(set.Id));

                foreach (var set in setsInSeries)
                {
                    var insertCommand = connection.CreateCommand();
                    insertCommand.CommandText = @"
                        INSERT OR REPLACE INTO SetCatalog (id, name, game, sort_order)
                        VALUES ($id, $name, $game, $order)
                    ";
                    insertCommand.Parameters.AddWithValue("$id", set.Id);
                    insertCommand.Parameters.AddWithValue("$name", set.Name);
                    insertCommand.Parameters.AddWithValue("$game", "Pokémon");
                    insertCommand.Parameters.AddWithValue("$order", order++);
                    insertCommand.ExecuteNonQuery();
                    totalSeeded++;
                    pokemonSeeded++;
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Seed error (Pokémon): {ex.GetType().Name} - {ex.Message}");
    }

    try
    {
        var response = await setsHttpClient.GetFromJsonAsync<List<OptcgSet>>(
            "https://optcgapi.com/api/allSets/"
        );

        if (response != null)
        {
            int order = 0;
            foreach (var set in response.Where(s => !knownEmpty.Contains(s.SetId)))
            {
                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = @"
                    INSERT OR REPLACE INTO SetCatalog (id, name, game, sort_order)
                    VALUES ($id, $name, $game, $order)
                ";
                insertCommand.Parameters.AddWithValue("$id", set.SetId);
                insertCommand.Parameters.AddWithValue("$name", set.SetName);
                insertCommand.Parameters.AddWithValue("$game", "One Piece");
                insertCommand.Parameters.AddWithValue("$order", order++);
                insertCommand.ExecuteNonQuery();
                totalSeeded++;
                onePieceSeeded++;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Seed error (One Piece): {ex.GetType().Name} - {ex.Message}");
    }

    int yuGiOhSeeded = 0;
    try
    {
        var ygoSets = await setsHttpClient.GetFromJsonAsync<List<YgoCardSetListing>>(
            "https://db.ygoprodeck.com/api/v7/cardsets.php"
        );

        if (ygoSets != null)
        {
            var orderedYgoSets = ygoSets
                .Where(s => !knownEmpty.Contains(s.SetName))
                .OrderBy(s => s.TcgDate ?? "9999-99-99"); // undated sets sort last

            int order = 0;
            foreach (var set in orderedYgoSets)
            {
                var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = @"
                    INSERT OR REPLACE INTO SetCatalog (id, name, game, sort_order)
                    VALUES ($id, $name, $game, $order)
                ";
                // YGOPRODeck identifies sets by name, not a separate code —
                // this same string gets passed to cardinfo.php?cardset=...
                // later, so it has to match exactly.
                insertCommand.Parameters.AddWithValue("$id", set.SetName);
                insertCommand.Parameters.AddWithValue("$name", set.SetName);
                insertCommand.Parameters.AddWithValue("$game", "Yu-Gi-Oh");
                insertCommand.Parameters.AddWithValue("$order", order++);
                insertCommand.ExecuteNonQuery();
                totalSeeded++;
                yuGiOhSeeded++;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Seed error (Yu-Gi-Oh): {ex.GetType().Name} - {ex.Message}");
    }

    return new {
        message = $"Seeded {totalSeeded} sets total",
        pokemon = pokemonSeeded,
        onePiece = onePieceSeeded,
        yuGiOh = yuGiOhSeeded
    };
}

app.MapPost("/api/admin/seed-catalog", async () => Results.Ok(await SeedCatalogAsync()));

// Separate from SeedCatalogAsync (SQLite) on purpose — reuses the same
// proven fetch logic, but writes to DynamoDB instead. Keeping this fully
// separate means the working SQLite path is never at risk from this change.
app.MapPost("/api/admin/seed-dynamodb-catalog", async () => {
    var allItems = new List<DynamoSetItem>();
    var knownEmpty = GetKnownEmptySetIds();
    int pokemonCount = 0, onePieceCount = 0, yuGiOhCount = 0;

    try
    {
        var seriesList = await setsHttpClient.GetFromJsonAsync<List<TcgdexSeriesBrief>>(
            "https://api.eu1.tcgdex.net/v2/en/series"
        );

        if (seriesList != null)
        {
            var seriesDetailTasks = seriesList.Select(async s => {
                try
                {
                    return await setsHttpClient.GetFromJsonAsync<TcgdexSeriesFull>(
                        $"https://api.eu1.tcgdex.net/v2/en/series/{s.Id}"
                    );
                }
                catch { return null; }
            });
            var seriesDetails = await Task.WhenAll(seriesDetailTasks);

            var orderedSeries = seriesDetails
                .Where(s => s != null)
                .OrderBy(s => {
                    int idx = pokemonSeriesOrder.FindIndex(era =>
                        s!.Name.Contains(era, StringComparison.OrdinalIgnoreCase));
                    return idx == -1 ? int.MaxValue : idx;
                });

            int order = 0;
            foreach (var series in orderedSeries)
            {
                var setsInSeries = (series!.Sets ?? new List<TcgdexSetBrief>())
                    .Where(set => (set.CardCount?.Total ?? 0) > 0)
                    .Where(set => !knownEmpty.Contains(set.Id))
                    .OrderBy(set => ApiSync.ExtractSetNumber(set.Id));

                foreach (var set in setsInSeries)
                {
                    allItems.Add(new DynamoSetItem {
                        Game = "Pokémon", SetID = set.Id, Name = set.Name, SortOrder = order++
                    });
                    pokemonCount++;
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DynamoDB seed error (Pokémon): {ex.GetType().Name} - {ex.Message}");
    }

    try
    {
        var response = await setsHttpClient.GetFromJsonAsync<List<OptcgSet>>(
            "https://optcgapi.com/api/allSets/"
        );

        if (response != null)
        {
            int order = 0;
            foreach (var set in response.Where(s => !knownEmpty.Contains(s.SetId)))
            {
                allItems.Add(new DynamoSetItem {
                    Game = "One Piece", SetID = set.SetId, Name = set.SetName, SortOrder = order++
                });
                onePieceCount++;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DynamoDB seed error (One Piece): {ex.GetType().Name} - {ex.Message}");
    }

    try
    {
        var ygoSets = await setsHttpClient.GetFromJsonAsync<List<YgoCardSetListing>>(
            "https://db.ygoprodeck.com/api/v7/cardsets.php"
        );

        if (ygoSets != null)
        {
            var orderedYgoSets = ygoSets
                .Where(s => !knownEmpty.Contains(s.SetName))
                .OrderBy(s => s.TcgDate ?? "9999-99-99");

            int order = 0;
            foreach (var set in orderedYgoSets)
            {
                allItems.Add(new DynamoSetItem {
                    Game = "Yu-Gi-Oh", SetID = set.SetName, Name = set.SetName, SortOrder = order++
                });
                yuGiOhCount++;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"DynamoDB seed error (Yu-Gi-Oh): {ex.GetType().Name} - {ex.Message}");
    }

    // The SDK's high-level batch write handles chunking into DynamoDB's
    // real 25-items-per-call limit internally — no manual chunking needed.
    var batch = dynamoContext.CreateBatchWrite<DynamoSetItem>();
    batch.AddPutItems(allItems);
    await batch.ExecuteAsync();

    return Results.Ok(new {
        message = $"Wrote {allItems.Count} sets to DynamoDB",
        pokemon = pokemonCount,
        onePiece = onePieceCount,
        yuGiOh = yuGiOhCount
    });
});

app.MapGet("/api/cards/{setId}", (string setId) => {
    using var connection = Database.GetConnection();
    var command = connection.CreateCommand();
    command.CommandText = @"
        SELECT id, name, rarity, image_url 
        FROM Cards 
        WHERE set_id = $setId
        ORDER BY CAST(number AS INTEGER)
    ";
    command.Parameters.AddWithValue("$setId", setId);

    var cards = new List<object>();
    using var reader = command.ExecuteReader();
    while (reader.Read())
    {
        cards.Add(new {
            id = reader.GetString(0),
            name = reader.GetString(1),
            rarity = reader.IsDBNull(2) ? "" : reader.GetString(2),
            imageUrl = reader.IsDBNull(3) ? "" : reader.GetString(3)
        });
    }

    bool hasMissingImages = cards.Any(c => string.IsNullOrEmpty(((dynamic)c).imageUrl));


    return Results.Ok(new {cards, hasMissingImages});
});

// Bulk pre-loads full card data for every set in the catalog, so that
// EVERY user's first-ever sync of a set is a pure DB read, not a live
// API call. Reuses the existing SyncPokemonSet/SyncOnePieceSet logic —
// this just loops over sets that haven't been synced yet.
//
// Designed to be called REPEATEDLY, not once: it only processes a small
// batch per call (default 10 sets), so a single request can't time out
// no matter how large the full catalog is. Already-synced sets are
// automatically skipped, so re-running this is always safe — it just
// picks up wherever the last call left off.
app.MapGet("/api/admin/catalog-status", () => {
    using var connection = Database.GetConnection();

    var catalogCommand = connection.CreateCommand();
    catalogCommand.CommandText = "SELECT COUNT(*) FROM SetCatalog";
    int catalogTotal = Convert.ToInt32(catalogCommand.ExecuteScalar());

    var syncedCommand = connection.CreateCommand();
    syncedCommand.CommandText = "SELECT COUNT(*) FROM Sets WHERE total > 0";
    int setsSynced = Convert.ToInt32(syncedCommand.ExecuteScalar());

    var emptyCommand = connection.CreateCommand();
    emptyCommand.CommandText = "SELECT COUNT(*) FROM Sets WHERE total = 0";
    int setsMarkedEmpty = Convert.ToInt32(emptyCommand.ExecuteScalar());

    var remainingCommand = connection.CreateCommand();
    remainingCommand.CommandText = @"
        SELECT COUNT(*) FROM SetCatalog sc
        LEFT JOIN Sets s ON sc.id = s.id
        WHERE s.id IS NULL OR s.total = 0
    ";
    int remainingToSync = Convert.ToInt32(remainingCommand.ExecuteScalar());

    var cardsCommand = connection.CreateCommand();
    cardsCommand.CommandText = "SELECT COUNT(*) FROM Cards";
    int totalCards = Convert.ToInt32(cardsCommand.ExecuteScalar());

    return Results.Ok(new { catalogTotal, setsSynced, setsMarkedEmpty, remainingToSync, totalCards });
});

app.MapPost("/api/admin/bulk-sync-cards", async (int? batchSize) => {
    int limit = batchSize ?? 10;
    using var connection = Database.GetConnection();

    var command = connection.CreateCommand();
    command.CommandText = @"
        SELECT sc.id, sc.game FROM SetCatalog sc
        LEFT JOIN Sets s ON sc.id = s.id
        WHERE s.id IS NULL OR s.total = 0
        LIMIT $limit
    ";
    command.Parameters.AddWithValue("$limit", limit);

    var setsToSync = new List<(string Id, string Game)>();
    using (var reader = command.ExecuteReader())
    {
        while (reader.Read())
            setsToSync.Add((reader.GetString(0), reader.GetString(1)));
    }

    var results = new List<object>();
    foreach (var (setId, game) in setsToSync)
    {
        try
        {
            bool hasCards = game switch
            {
                "One Piece" => await ApiSync.SyncOnePieceSet(setId),
                "Yu-Gi-Oh" => await ApiSync.SyncYuGiOhSet(setId),
                _ => await ApiSync.SyncPokemonSet(setId)
            };
            results.Add(new { setId, game, success = hasCards });
        }
        catch (Exception ex)
        {
            // One bad set shouldn't stop the whole batch — record it and
            // move on; it'll just get picked up again on the next call.
            results.Add(new { setId, game, success = false, error = ex.Message });
        }
    }

    var remainingCommand = connection.CreateCommand();
    remainingCommand.CommandText = @"
        SELECT COUNT(*) FROM SetCatalog sc
        LEFT JOIN Sets s ON sc.id = s.id
        WHERE s.id IS NULL OR s.total = 0
    ";
    int remaining = Convert.ToInt32(remainingCommand.ExecuteScalar());

    return Results.Ok(new { synced = results, remainingSets = remaining });
});

app.MapPost("/api/sync/{setId}", async (string setId) => {
    bool hasCards = await ApiSync.SyncPokemonSet(setId);
    return Results.Ok(new {
        hasCards,
        message = hasCards ? $"Set {setId} ready" : $"Set {setId} has no card data available"
    });
});

app.MapPost("/api/sync/onepiece/{setId}", async (string setId) => {
    bool hasCards = await ApiSync.SyncOnePieceSet(setId);
    return Results.Ok(new {
        hasCards,
        message = hasCards ? $"Synced One Piece set {setId}" : $"Set {setId} has no card data available"
    });
});

app.MapPost("/api/sync/yugioh/{setId}", async (string setId) => {
    bool hasCards = await ApiSync.SyncYuGiOhSet(setId);
    return Results.Ok(new {
        hasCards,
        message = hasCards ? $"Synced Yu-Gi-Oh set {setId}" : $"Set {setId} has no card data available"
    });
});

app.MapGet("/api/sets/yugioh", async () => {
    var results = await dynamoContext.QueryAsync<DynamoSetItem>("Yu-Gi-Oh").GetRemainingAsync();
    var sets = results
        .OrderBy(s => s.SortOrder)
        .Select(s => new { setID = s.SetID, name = s.Name });

    return Results.Ok(sets);
});

app.MapGet("/api/trackers", (string clientId) => {
    var trackers = TrackerStore.GetTrackers(clientId);
    return Results.Ok(trackers);
});

app.MapPost("/api/trackers", (TrackerRequest req) => {
    TrackerStore.AddTracker(req.ClientId, req.SetId, req.Name, req.Game);
    return Results.Ok(new { message = "Tracker saved" });
});

app.MapDelete("/api/trackers/{setId}", (string setId, string clientId) => {
    TrackerStore.RemoveTracker(clientId, setId);
    return Results.Ok(new { message = "Tracker removed" });
});

app.MapPut("/api/trackers/reorder", (ReorderRequest req) => {
    TrackerStore.ReorderTrackers(req.ClientId, req.OrderedSetIds);
    return Results.Ok(new { message = "Order saved" });
});

Database.Initialize();

// Free-tier hosts wipe ephemeral storage on spin-down/restart, which was
// leaving the live demo empty until someone manually re-ran the seed.
// This makes that self-healing: check once at startup, and if the
// catalog's empty, kick off a real reseed automatically. Fire-and-forget
// (not awaited) so a slow/degraded TCGdex doesn't delay the app actually
// starting to listen for requests.
using (var startupConnection = Database.GetConnection())
{
    var checkCommand = startupConnection.CreateCommand();
    checkCommand.CommandText = "SELECT COUNT(*) FROM SetCatalog";
    int catalogCount = Convert.ToInt32(checkCommand.ExecuteScalar());

    if (catalogCount == 0)
    {
        Console.WriteLine("SetCatalog is empty on startup — auto-seeding in the background...");
        _ = SeedCatalogAsync();
    }
}

// Render (and most hosts) assign a port via PORT — 0.0.0.0 accepts
// connections from outside the container, unlike localhost.
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");

// Mirrors the SetCatalog table's shape — Game is the partition key so all
// sets for one TCG live together, SetID is the sort key identifying each
// one within that game.
[DynamoDBTable("TCGRiderSetCatalog")]
public class DynamoSetItem
{
    [DynamoDBHashKey("Game")]
    public string Game { get; set; } = "";

    [DynamoDBRangeKey("SetID")]
    public string SetID { get; set; } = "";

    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}