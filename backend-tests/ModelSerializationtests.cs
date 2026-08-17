using System.Text.Json;
using Xunit;

public class TcgdexModelTests
{
    // Plain JsonSerializer.Deserialize is case-SENSITIVE by default — this
    // has to be explicit here to actually match how GetFromJsonAsync behaves
    // against TCGdex's camelCase JSON in the real sync code.
    private static readonly JsonSerializerOptions Options =
        new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void TcgdexSetBrief_DeserializesCardCount()
    {
        var json = @"{
            ""id"": ""swshp"",
            ""name"": ""SWSH Black Star Promos"",
            ""cardCount"": { ""official"": 73, ""total"": 73 }
        }";

        var set = JsonSerializer.Deserialize<TcgdexSetBrief>(json, Options);

        Assert.Equal("swshp", set!.Id);
        Assert.Equal("SWSH Black Star Promos", set.Name);
        Assert.NotNull(set.CardCount);
        Assert.Equal(73, set.CardCount!.Total);
    }

    [Fact]
    public void TcgdexSetBrief_HandlesMissingCardCount()
    {
        // Some TCGdex entries omit cardCount entirely — this is exactly the
        // case CardCount?.Total ?? 0 in Program.cs was written to handle.
        var json = @"{ ""id"": ""base1"", ""name"": ""Base Set"" }";

        var set = JsonSerializer.Deserialize<TcgdexSetBrief>(json, Options);

        Assert.Null(set!.CardCount);
    }

    [Fact]
    public void TcgdexSeriesFull_DeserializesNestedSets()
    {
        var json = @"{
            ""id"": ""swsh"",
            ""name"": ""Sword & Shield"",
            ""sets"": [
                { ""id"": ""swsh1"", ""name"": ""Sword & Shield"" },
                { ""id"": ""swsh2"", ""name"": ""Rebel Clash"" }
            ]
        }";

        var series = JsonSerializer.Deserialize<TcgdexSeriesFull>(json, Options);

        Assert.Equal("Sword & Shield", series!.Name);
        Assert.Equal(2, series.Sets!.Count);
        Assert.Equal("swsh2", series.Sets[1].Id);
    }
}

public class OptcgModelTests
{
    // OptcgCard uses explicit [JsonPropertyName] attributes, so it doesn't
    // need case-insensitive options — it's testing the snake_case mapping
    // specifically (card_set_id -> CardSetId), which is the thing that
    // silently fails if the attributes are ever removed or typo'd.
    [Fact]
    public void OptcgCard_DeserializesSnakeCaseFields()
    {
        var json = @"{
            ""card_set_id"": ""OP01-001"",
            ""set_id"": ""OP-01"",
            ""set_name"": ""Romance Dawn"",
            ""card_name"": ""Monkey D. Luffy"",
            ""rarity"": ""L"",
            ""card_image"": ""https://example.com/OP01-001.png""
        }";

        var card = JsonSerializer.Deserialize<OptcgCard>(json);

        Assert.Equal("OP01-001", card!.CardSetId);
        Assert.Equal("Romance Dawn", card.SetName);
        Assert.Equal("Monkey D. Luffy", card.CardName);
        Assert.Equal("L", card.Rarity);
    }

    [Fact]
    public void OptcgCard_HandlesMissingOptionalFields()
    {
        // Rarity and image are nullable in the model — confirms a card
        // missing that data deserializes instead of throwing.
        var json = @"{ ""card_set_id"": ""OP01-002"", ""card_name"": ""Roronoa Zoro"" }";

        var card = JsonSerializer.Deserialize<OptcgCard>(json);

        Assert.Equal("OP01-002", card!.CardSetId);
        Assert.Null(card.Rarity);
        Assert.Null(card.CardImage);
    }
}