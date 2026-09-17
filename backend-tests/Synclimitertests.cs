using System;
using Xunit;

[Collection("Database collection")]
public class SyncLimiterTests
{
    [Fact]
    public void FirstSync_ForNewClient_IsAllowed()
    {
        var clientId = Guid.NewGuid().ToString();

        var result = SyncLimiter.CheckAndIncrement(clientId);

        Assert.True(result.Allowed);
        Assert.Equal(9, result.RemainingToday); // 10 - 1

        Cleanup(clientId);
    }

    [Fact]
    public void EleventhSync_WithinWindow_IsRejected()
    {
        var clientId = Guid.NewGuid().ToString();

        for (int i = 0; i < 10; i++)
        {
            SyncLimiter.CheckAndIncrement(clientId);
        }

        var eleventh = SyncLimiter.CheckAndIncrement(clientId);

        Assert.False(eleventh.Allowed);
        Assert.Equal(0, eleventh.RemainingToday);

        Cleanup(clientId);
    }

    [Fact]
    public void GetStatus_DoesNotConsumeASync()
    {
        var clientId = Guid.NewGuid().ToString();

        SyncLimiter.CheckAndIncrement(clientId); // uses 1 of 10
        var statusBefore = SyncLimiter.GetStatus(clientId);
        var statusAfter = SyncLimiter.GetStatus(clientId); // checking again should NOT consume another

        Assert.Equal(statusBefore.RemainingToday, statusAfter.RemainingToday);

        Cleanup(clientId);
    }

    private static void Cleanup(string clientId)
    {
        using var connection = Database.GetConnection();
        var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM SyncLimitTracker WHERE client_id = $clientId";
        cmd.Parameters.AddWithValue("$clientId", clientId);
        cmd.ExecuteNonQuery();
    }
}