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
var setsHttpClient = new HttpClient();

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
    try
    {
        var seriesList = await setsHttpClient.GetFromJsonAsync<List<TcgdexSeriesBrief>>(
            "https://api.tcgdex.net/v2/en/series"
        );

        if (seriesList == null)
            return Results.Ok(new List<object>());

        // Fetch each series' full set list in parallel — a fixed, small number
        // of calls (one per era, not per set), so this stays cheap.
        var seriesDetailTasks = seriesList.Select(async s => {
            try
            {
                return await setsHttpClient.GetFromJsonAsync<TcgdexSeriesFull>(
                    $"https://api.tcgdex.net/v2/en/series/{s.Id}"
                );
            }
            catch
            {
                return null; // one bad series shouldn't take down the whole list
            }
        });
        var seriesDetails = await Task.WhenAll(seriesDetailTasks);

        var orderedSeries = seriesDetails
            .Where(s => s != null)
            .OrderBy(s => {
                int idx = pokemonSeriesOrder.FindIndex(era =>
                    s!.Name.Contains(era, StringComparison.OrdinalIgnoreCase));
                return idx == -1 ? int.MaxValue : idx; // unrecognized series sort last, not dropped
            });

        var knownEmpty = GetKnownEmptySetIds();

        var sets = orderedSeries
            .SelectMany(s => (s!.Sets ?? new List<TcgdexSetBrief>())
                .Where(set => (set.CardCount?.Total ?? 0) > 0) // drop sets with no actual cards
                .OrderBy(set => ApiSync.ExtractSetNumber(set.Id))
            )
            .Where(set => !knownEmpty.Contains(set.Id))
            .Select(set => new { name = set.Name, setID = set.Id })
            .ToList();

        return Results.Ok(sets);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"TCGdex API error: {ex.GetType().Name} - {ex.Message}");
        return Results.Ok(new List<object>());
    }
});

app.MapGet("/api/sets/onepiece", async () => {
    try
    {
        var response = await setsHttpClient.GetFromJsonAsync<List<OptcgSet>>(
            "https://optcgapi.com/api/allSets/"
        );

        if (response == null)
            return Results.Ok(new List<object>());

        var knownEmpty = GetKnownEmptySetIds();

        var sets = response
            .Where(s => !knownEmpty.Contains(s.SetId))
            .Select(s => new { name = s.SetName, setID = s.SetId })
            .ToList();
        return Results.Ok(sets);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"OPTCG API error: {ex.GetType().Name} - {ex.Message}");
        return Results.Ok(new List<object>());
    }
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