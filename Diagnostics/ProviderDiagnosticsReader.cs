using Dalamud.Plugin;
using SentinelHub.Ecosystem;
using SentinelHub.Health;
namespace SentinelHub.Diagnostics;
public sealed class ProviderDiagnosticsReader(IDalamudPluginInterface pi,Func<string>? ownCharacterKey=null)
{
    public ProviderDiagnostic[] Read()=>Catalog.Components.Select(c=>{
        try{
            if(!pi.InstalledPlugins.Any(p=>p.InternalName==c.Id&&p.IsLoaded))return new ProviderDiagnostic(c.Id,ProviderState.Unavailable,0,0,0);
            if(c.Id=="SRankSentinel"&&ownCharacterKey is not null&&ownCharacterKey() is {Length:>0} key){
                try{var legacy=SRankStatusProjection.Parse(pi.GetIpcSubscriber<string,string>("SRankSentinel.Remote.v1.Status").InvokeFunc(key));
                    if(legacy.Available)return new ProviderDiagnostic(c.Id,legacy.Health==0?ProviderState.Ready:legacy.Health==1?ProviderState.Disabled:ProviderState.Degraded,1,0,0){QueueCount=legacy.QueueCount,Recovering=legacy.Recovering};}catch{}
            }
            return DiagnosticProviderPolicy.Parse(c.Id,pi.GetIpcSubscriber<string>("Sentinel."+c.Id+".Diagnostics.v1").InvokeFunc());
        }catch{return new ProviderDiagnostic(c.Id,ProviderState.Unavailable,0,0,0);}
    }).ToArray();
}
