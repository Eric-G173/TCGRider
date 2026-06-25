using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
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
app.UseCors();

app.MapGet("/ping", () => Results.Ok(new { status = "ok", message = "C# backend running" }));

app.MapGet("/api/sets", () => {
    return Results.Ok(new[] {
        new { id = "sv8pt5", name = "Prismatic Evolution", total = 193 },
        new { id = "sv3pt5", name = "Pokémon 151", total = 165 }
    });
});

app.MapGet("/api/cards/{setId}", (string setId) => {
    using var connection = Database.GetConnection();
    var command = connection.CreateCommand();
    command.CommandText = @"
        SELECT name, rarity, image_url 
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
            name = reader.GetString(0),
            rarity = reader.IsDBNull(1) ? "" : reader.GetString(1),
            imageUrl = reader.IsDBNull(2) ? "" : reader.GetString(2)
        });
    }

    return Results.Ok(cards);
});

app.MapPost("/api/sync/{setId}", async (string setId) => {
    await ApiSync.SyncSet(setId);
    return Results.Ok(new { message = $"Synced set {setId}" });
});

Database.Initialize();
app.Run("http://localhost:5000");