using Xunit;

// xUnit test projects never run Program.cs, so Database.Initialize()
// (which creates every table, including SyncLimitTracker) would otherwise
// never run at all in this context. This fixture runs it once, explicitly,
// before any test in the collection executes. Safe to call even if it's
// already run elsewhere — every statement inside is CREATE TABLE IF NOT
// EXISTS, so this is fully idempotent.
public class DatabaseFixture
{
    public DatabaseFixture()
    {
        Database.Initialize();
    }
}

[CollectionDefinition("Database collection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}