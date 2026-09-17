using System.Collections.Concurrent;

// Separate from SyncLimiter on purpose — that one caps TOTAL volume over a
// time window; this one caps how many syncs can be IN FLIGHT at the exact
// same instant, per client. Different failure mode, different mechanism:
// this is in-memory only (no persistence needed — "currently running" is
// only ever meaningful for the current process anyway), using the same
// SemaphoreSlim pattern already used for the Pokemon rarity-fetch
// parallelization elsewhere in this codebase, just keyed per client.
public static class ConcurrencyLimiter
{
    private const int MaxConcurrentPerClient = 2;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _clientSemaphores = new();

    private static SemaphoreSlim GetSemaphore(string clientId) =>
        _clientSemaphores.GetOrAdd(clientId, _ => new SemaphoreSlim(MaxConcurrentPerClient, MaxConcurrentPerClient));

    // Non-blocking: immediately returns true (and holds a slot) if this
    // client is under their concurrent limit, or false if already at max.
    // Never makes the caller wait — a full slot means "reject now", not
    // "queue and wait", since a sync can take a genuinely long time.
    public static bool TryEnter(string clientId)
    {
        return GetSemaphore(clientId).Wait(0);
    }

    // MUST be called (in a finally block) once a sync finishes, whether it
    // succeeded or threw — otherwise a crashed sync permanently leaks a
    // slot, eventually locking that client out of syncing at all.
    public static void Exit(string clientId)
    {
        GetSemaphore(clientId).Release();
    }
}