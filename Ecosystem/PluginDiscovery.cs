using Dalamud.Plugin;
using SentinelHub.Diagnostics;
namespace SentinelHub.Ecosystem;
public sealed class PluginDiscovery : IDisposable
{
    private readonly IDalamudPluginInterface pi;
    private readonly FaultJournal faults;
    private volatile bool dirty = true;
    private volatile bool capturing;
    public bool PendingChange=>dirty||capturing;
    private long nextInventory, nextProbes;
    public long Revision { get; private set; }
    public bool Available { get; private set; }
    public InstalledPlugin[] Inventory { get; private set; } = [];
    public HubSnapshot Snapshot { get; private set; } = HubSnapshot.Unknown;
    public PluginDiscovery(IDalamudPluginInterface pi, FaultJournal faults)
    {
        this.pi = pi; this.faults = faults;
        pi.ActivePluginsChanged += Changed;
    }
    private void Changed(IActivePluginsChangedEventArgs _) => dirty = true;
    public void Invalidate() => dirty = true;
    public bool Refresh(bool force = false, bool visible = false)
    {
        var now = Environment.TickCount64;
        var changed = false;
        if (force || dirty || now >= nextInventory)
        {
            capturing=true;
            dirty = false; nextInventory = now + 60_000;
            try
            {
                var current = Capture();
                changed = !Available || !current.SequenceEqual(Inventory);
                Available = true;
                if (changed) { Inventory = current; Revision++; }
            }
            catch
            {
                changed = Available || Revision == 0;
                Inventory = []; Available = false;
                if (changed) Revision++;
                nextInventory = now + 5_000;
                faults.Record(FaultCode.DiscoveryUnavailable);
            }
            finally{capturing=false;}
        }
        if (!force && !changed && now < nextProbes) return false;
        nextProbes = now + (visible ? 2_000 : 15_000);
        Snapshot = StatusPolicy.Build(Inventory, Available, Probe);
        return true;
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
