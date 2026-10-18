using Dalamud.Plugin;
using SentinelHub.Configuration;
namespace SentinelHub.Coordination;
public sealed class PauseCoordinator(IDalamudPluginInterface pi,HubSettings settings,Action save)
{
    private long next;
    private readonly string lease=Guid.NewGuid().ToString("N"); // Ephemeral, never exported.
    private readonly HashSet<string> owned=[];
    public PauseView View {get;private set;}=PauseView.Empty;
    public void Request(bool pause)
    {
        settings.AutomationPaused=pause;save();next=0;
        View=new(pause,[new("SentinelHub",PauseState.Pending,"Applying the request on the framework thread.")]);
    }
    public void Tick(bool hubStopped)
    {
        if(Environment.TickCount64<next)return;next=Environment.TickCount64+500;
        var paused=settings.AutomationPaused;
        var all=pi.InstalledPlugins.ToArray();
        var entries=new List<PauseParticipant>{new("SentinelHub",paused?(hubStopped?PauseState.Acknowledged:PauseState.Pending):PauseState.Running,
            paused?(hubStopped?"Hub Anti-AFK, Quest and latency writers are suspended; saved preferences retained.":"Waiting for Hub writers to suspend."):
            "Hub pause released; saved preferences and compatibility/readiness gates still apply.")};
        foreach(var id in new[]{"SRankSentinel","PvPSentinel"}){
            var copies=all.Where(p=>p.InternalName.Equals(id,StringComparison.OrdinalIgnoreCase)).ToArray();
            var result=PausePolicy.Resolve(id,paused,copies.Length>0,copies.Any(p=>p.IsLoaded),
                ()=>pi.GetIpcSubscriber<int>("Sentinel."+id+".Pause.Protocol.v1").InvokeFunc(),
                value=>pi.GetIpcSubscriber<string,bool,bool>("Sentinel."+id+".Pause.Set.v1").InvokeFunc(lease,value),
                ()=>pi.GetIpcSubscriber<bool>("Sentinel."+id+".Pause.IsPaused.v1").InvokeFunc(),owned.Contains(id));
            if(result.Accepted){if(paused)owned.Add(id);else owned.Remove(id);}entries.Add(result);
        }
        View=new(paused,entries.ToArray());
    }
}
