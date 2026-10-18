using Dalamud.Plugin;
using SentinelHub.Ecosystem;
namespace SentinelHub.Health;

public sealed class HealthService(IDalamudPluginInterface pi,Func<InstalledPlugin[]> capture,Func<HubSnapshot> snapshot,PublicCatalog catalog)
{
    private long next;
    public HealthView View {get;private set;}=HealthView.Empty;
    public void Tick(bool force=false)
    {
        catalog.Tick();if(!force&&Environment.TickCount64<next)return;next=Environment.TickCount64+2000;
        try{
            var all=capture();var s=snapshot();
            var checks=Catalog.Components.Select(c=>HealthPolicy.Plugin(c,all,s.DiscoveryAvailable,catalog.Versions,
                c.Id=="SentinelHub"?0:Probe(c.Id,all))).Concat(s.Dependencies.Select(HealthPolicy.Dependency)).ToList();
            var core=typeof(SentinelCore.Ipc.SentinelIpcContract).Assembly.GetName().Version?.ToString();
            checks.Add(new("SentinelCore",HealthState.Loaded,StatusPolicy.NumericVersion(core),"Unknown",false,
                "Hub's bundled shared assembly is loaded. Other plugins own their bundled Core copies; no global Core service is assumed."));
            checks.Add(new("Faloop",HealthState.Unknown,"Unknown","Unknown",false,
                "SRank's live source fixes direct Faloop off. HuntAlerts/Sonar may carry Faloop reports; their installation does not prove a live report feed."));
            View=new(DateTimeOffset.UtcNow,s.DiscoveryAvailable,checks.ToArray(),catalog.At,catalog.Status);
        }catch{View=HealthView.Empty;}
    }
    private int? Probe(string id,InstalledPlugin[] all)
    {
        if(!all.Any(p=>p.Id==id&&p.Loaded))return null;
        try{var result=pi.GetIpcSubscriber<int>("Sentinel."+id+".Health.v1").InvokeFunc();return result is >=0 and <=2?result:-1;}
        catch{return -1;}
    }
}
