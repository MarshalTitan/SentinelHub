namespace SentinelHub.Diagnostics;
public enum FaultCode { DiscoveryUnavailable, DependencyStatusUnavailable, OpenSettingsUnavailable, ExportUnavailable, ConfigurationSaveUnavailable, AntiAfkOwnershipUnavailable, QuestConvenienceOwnershipUnavailable, LatencyUnavailable }
public sealed record Fault(DateTimeOffset At, FaultCode Code);
public sealed class FaultJournal
{
    private readonly Queue<Fault> entries = new();
    private readonly Dictionary<FaultCode, DateTimeOffset> last = new();
    public void Record(FaultCode code)
    {
        var now = DateTimeOffset.UtcNow;
        if (last.TryGetValue(code, out var at) && now - at < TimeSpan.FromSeconds(30)) return;
        last[code] = now;
        if (entries.Count == 64) entries.Dequeue();
        entries.Enqueue(new(now, code));
    }
    public Fault[] Snapshot() => entries.ToArray();
    public void Clear() { entries.Clear(); last.Clear(); }
}
