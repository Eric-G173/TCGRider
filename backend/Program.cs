using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using DotNetEnv;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
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

// shared client for the /api/sets/pokemon endpoint — created once, reused for every request
var pokemonHttpClient = new HttpClient();

app.MapGet("/ping", () => Results.Ok(new { status = "ok", message = "C# backend running" }));

app.MapGet("/api/sets/pokemon", async () => {
    try
    {
        var response = await pokemonHttpClient.GetFromJsonAsync<List<TcgdexSetBrief>>(
            "https://api.tcgdex.net/v2/en/sets"
        );

        if (response == null)
            return Results.Ok(new List<object>());

        var sets = response.Select(s => new { name = s.Name, setID = s.Id }).ToList();
        return Results.Ok(sets);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"TCGdex API error: {ex.GetType().Name} - {ex.Message}");
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
    await ApiSync.SyncSet(setId);
    return Results.Ok(new { message = $"Synced set {setId}" });
});

Database.Initialize();
app.Run("http://localhost:5000");