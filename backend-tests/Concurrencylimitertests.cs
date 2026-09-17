using System;
using Xunit;

public class ConcurrencyLimiterTests
{
    [Fact]
    public void TryEnter_AllowsUpToTwoConcurrentSyncsPerClient()
    {
        var clientId = Guid.NewGuid().ToString();

        bool first = ConcurrencyLimiter.TryEnter(clientId);
        bool second = ConcurrencyLimiter.TryEnter(clientId);
        bool third = ConcurrencyLimiter.TryEnter(clientId);

        Assert.True(first);
        Assert.True(second);
        Assert.False(third); // 3rd concurrent sync for the same client should be rejected

        ConcurrencyLimiter.Exit(clientId);
        ConcurrencyLimiter.Exit(clientId);
    }

    [Fact]
    public void Exit_ReleasesASlotForReuse()
    {
        var clientId = Guid.NewGuid().ToString();

        ConcurrencyLimiter.TryEnter(clientId);
        ConcurrencyLimiter.TryEnter(clientId);
        ConcurrencyLimiter.Exit(clientId); // free one slot, as syncing/finally would

        bool canEnterAgain = ConcurrencyLimiter.TryEnter(clientId);
        Assert.True(canEnterAgain);

        ConcurrencyLimiter.Exit(clientId);
        ConcurrencyLimiter.Exit(clientId);
    }

    [Fact]
    public void DifferentClients_HaveIndependentLimits()
    {
        var clientA = Guid.NewGuid().ToString();
        var clientB = Guid.NewGuid().ToString();

        ConcurrencyLimiter.TryEnter(clientA);
        ConcurrencyLimiter.TryEnter(clientA); // client A now at its limit

        bool clientBCanEnter = ConcurrencyLimiter.TryEnter(clientB);
        Assert.True(clientBCanEnter); // client B's limit is separate

        ConcurrencyLimiter.Exit(clientA);
        ConcurrencyLimiter.Exit(clientA);
        ConcurrencyLimiter.Exit(clientB);
    }
}