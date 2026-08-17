using Xunit;

public class SetSortingTests
{
    [Theory]
    [InlineData("base1", 1)]
    [InlineData("base2", 2)]
    [InlineData("neo4", 4)]
    [InlineData("swsh12", 12)]
    public void ExtractSetNumber_ParsesNumberFromId(string setId, int expected)
    {
        int result = ApiSync.ExtractSetNumber(setId);
        Assert.Equal(expected, result);
    }

    // Promos and one-off specials (no trailing number) should sort to the
    // very end rather than landing randomly in the middle of a series.
    [Theory]
    [InlineData("basep")]
    [InlineData("southernislands")]
    [InlineData("jumbo")]
    public void ExtractSetNumber_ReturnsMaxValueForIdsWithNoNumber(string setId)
    {
        int result = ApiSync.ExtractSetNumber(setId);
        Assert.Equal(int.MaxValue, result);
    }

    [Fact]
    public void ExtractSetNumber_SortsAListIntoReleaseOrder()
    {
        var ids = new[] { "base3", "basep", "base1", "base2" };

        var sorted = System.Linq.Enumerable.OrderBy(ids, ApiSync.ExtractSetNumber);

        Assert.Equal(new[] { "base1", "base2", "base3", "basep" }, sorted);
    }
}