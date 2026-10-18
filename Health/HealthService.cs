using Dalamud.Plugin;
using SentinelHub.Ecosystem;
namespace SentinelHub.Health;

public sealed class HealthService(IDalamudPluginInterface pi,Func<InstalledPlugin[]> capture,Func<HubSnapshot> snapshot,PublicCatalog catalog,Func<string>? ownCharacterKey=null)
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
                "Direct Faloop is dormant in audited SRank 0.7.62.0. Live feed state is not exposed here; HuntAlerts/Sonar installation does not prove a live report feed."));
            View=new(DateTimeOffset.UtcNow,s.DiscoveryAvailable,checks.ToArray(),catalog.At,catalog.Status);
        }catch{View=HealthView.Empty;}
    }
    private int? Probe(string id,InstalledPlugin[] all)
    {
        if(!all.Any(p=>p.Id==id&&p.Loaded))return null;
        try{var result=pi.GetIpcSubscriber<int>("Sentinel."+id+".Health.v1").InvokeFunc();return result is >=0 and <=2?result:-1;}
        catch{
            if(id=="SRankSentinel"&&ownCharacterKey is not null){
                var key=ownCharacterKey();if(key.Length==0)return null;
                try{var status=SRankStatusProjection.Parse(pi.GetIpcSubscriber<string,string>("SRankSentinel.Remote.v1.Status").InvokeFunc(key));if(status.Available)return status.Health;}catch{}
            }
            return -1;
        }
    }
}
