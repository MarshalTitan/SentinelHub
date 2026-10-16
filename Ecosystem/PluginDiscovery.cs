using Dalamud.Plugin;
using SentinelHub.Diagnostics;
namespace SentinelHub.Ecosystem;
public sealed class PluginDiscovery : IDisposable
{
    private readonly IDalamudPluginInterface pi;
    private readonly FaultJournal faults;
    private volatile bool dirty = true;
    private DateTime next = DateTime.MinValue;
    public HubSnapshot Snapshot { get; private set; } = HubSnapshot.Unknown;
    public PluginDiscovery(IDalamudPluginInterface pi, FaultJournal faults)
    {
        this.pi = pi; this.faults = faults;
        pi.ActivePluginsChanged += Changed;
    }
    private void Changed(IActivePluginsChangedEventArgs _) => dirty = true;
    public void Refresh(bool force = false)
    {
        if (!force && !dirty && DateTime.UtcNow < next) return;
        dirty = false; next = DateTime.UtcNow.AddSeconds(2);
        try
        {
            var plugins = Capture();
            Snapshot = StatusPolicy.Build(plugins, true, Probe);
        }
        catch
        {
            Snapshot = HubSnapshot.Unknown; // Never label stale observations current.
            faults.Record(FaultCode.DiscoveryUnavailable);
        }
    }
    public InstalledPlugin[] Capture() => pi.InstalledPlugins.Select(p =>
        new InstalledPlugin(p.InternalName, p.IsLoaded, p.Version.ToString(),
            p.IsOutdated || p.IsBanned || p.IsOrphaned || p.IsDecommissioned, p.IsDev)).ToArray();
    private Response Probe(Dependency d)
    {
        try
        {
            var result = pi.GetIpcSubscriber<bool>(d.Probe!).InvokeFunc();
            return d.Id == "vnavmesh" ? result ? Response.Ready : Response.NotReady : Response.Responding;
        }
        catch { faults.Record(FaultCode.DependencyStatusUnavailable); return Response.Unavailable; }
    }
    public void Dispose() => pi.ActivePluginsChanged -= Changed;
}
