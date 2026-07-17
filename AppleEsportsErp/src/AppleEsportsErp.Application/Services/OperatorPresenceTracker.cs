using System.Collections.Concurrent;

namespace AppleEsportsErp.Application.Services;

public static class OperatorPresenceTracker
{
    // branchId -> number of connected operators
    private static readonly ConcurrentDictionary<string, int> _branchOperatorCounts = new();

    public static void OperatorConnected(string branchId)
    {
        _branchOperatorCounts.AddOrUpdate(branchId, 1, (key, count) => count + 1);
    }

    public static void OperatorDisconnected(string branchId)
    {
        _branchOperatorCounts.AddOrUpdate(branchId, 0, (key, count) => Math.Max(0, count - 1));
    }

    public static bool IsOperatorAvailable(string branchId)
    {
        return _branchOperatorCounts.TryGetValue(branchId, out var count) && count > 0;
    }
}
