using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DotNetEnv;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

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

// shared client reused across the /api/sets/* endpoints (renamed from
// pokemonHttpClient now that it's also used for One Piece)
// Timeout reduced from the 100s default — if TCGdex/OPTCG is unreachable
// during a seed run, we want that to fail fast rather than hang.
var setsHttpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(15)
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

app.MapGet("/api/sets/pokemon", () => {
    using var connection = Database.GetConnection();
    var command = connection.CreateCommand();
    command.CommandText = @"
        SELECT id, name FROM SetCatalog
        WHERE game = 'Pokémon'
        ORDER BY sort_order
    ";

    var sets = new List<object>();
    using var reader = command.ExecuteReader();
    while (reader.Read())
        sets.Add(new { setID = reader.GetString(0), name = reader.GetString(1) });

    return Results.Ok(sets);
});

app.MapGet("/api/sets/onepiece", () => {
    using var connection = Database.GetConnection();
    var command = connection.CreateCommand();
    command.CommandText = @"
        SELECT id, name FROM SetCatalog
        WHERE game = 'One Piece'
        ORDER BY sort_order
    ";

    var sets = new List<object>();
    using var reader = command.ExecuteReader();
    while (reader.Read())
        sets.Add(new { setID = reader.GetString(0), name = reader.GetString(1) });

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
app.MapPost("/api/admin/seed-catalog", async () => {
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
            "https://api.tcgdex.net/v2/en/series"
        );

        if (seriesList != null)
        {
            var seriesDetailTasks = seriesList.Select(async s => {
                try
                {
                    return await setsHttpClient.GetFromJsonAsync<TcgdexSeriesFull>(
                        $"https://api.tcgdex.net/v2/en/series/{s.Id}"
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

    return Results.Ok(new {
        message = $"Seeded {totalSeeded} sets total",
        pokemon = pokemonSeeded,
        onePiece = onePieceSeeded
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

// Render (and most hosts) assign a port via PORT — 0.0.0.0 accepts
// connections from outside the container, unlike localhost.
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");