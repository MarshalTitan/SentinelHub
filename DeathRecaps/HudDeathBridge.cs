using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
namespace SentinelHub.DeathRecaps;

public sealed class HudDeathBridge : IDisposable
{
    private readonly ICallGateSubscriber<int> version;
    private readonly ICallGateSubscriber<string> snapshot;
    private readonly ICallGateSubscriber<long, object?> changed;
    private readonly Action<long> notification;
    private volatile bool dirty = true;
    private bool disposed;
    private ThreatEnvelope? current;
    public string Availability { get; private set; } = "HUD death history not yet requested.";
    public HudDeathBridge(IDalamudPluginInterface pi)
    {
        version = pi.GetIpcSubscriber<int>("SentinelHUD.Deaths.v1.Version");
        snapshot = pi.GetIpcSubscriber<string>("SentinelHUD.Deaths.v1.Snapshot");
        changed = pi.GetIpcSubscriber<long, object?>("SentinelHUD.Deaths.v1.Changed");
        notification = _ => dirty = true; changed.Subscribe(notification);
    }
    public void Reset() { current = null; dirty = true; }
    public ThreatDeath? Find(uint territory, uint local, DateTimeOffset at, bool retry = false)
    {
        if (disposed) return null;
        if (dirty || retry) {
            dirty = false; current = null;
            try {
                if (version.InvokeFunc() == 1) current = DeathPolicy.ParseThreat(snapshot.InvokeFunc());
                Availability = current is null ? "HUD death contract unavailable or incompatible." : "HUD v1 death snapshots available.";
            } catch { Availability = "HUD unavailable; PvE capture remains independent."; }
        }
        return current?.Deaths.Where(d => d.Territory == territory && d.LocalEntityId == local
            && Math.Abs((d.At - at).TotalSeconds) <= 1.5).MinBy(d => Math.Abs((d.At - at).Ticks));
    }
    public void Dispose() { if (disposed) return; disposed = true; current = null; try { changed.Unsubscribe(notification); } catch { } }
}
