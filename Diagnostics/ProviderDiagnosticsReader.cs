using Dalamud.Plugin;
using SentinelHub.Ecosystem;
namespace SentinelHub.Diagnostics;
public sealed class ProviderDiagnosticsReader(IDalamudPluginInterface pi)
{
    public ProviderDiagnostic[] Read()=>Catalog.Components.Select(c=>{
        try{
            if(!pi.InstalledPlugins.Any(p=>p.InternalName==c.Id&&p.IsLoaded))return new ProviderDiagnostic(c.Id,ProviderState.Unavailable,0,0,0);
            return DiagnosticProviderPolicy.Parse(c.Id,pi.GetIpcSubscriber<string>("Sentinel."+c.Id+".Diagnostics.v1").InvokeFunc());
        }catch{return new ProviderDiagnostic(c.Id,ProviderState.Unavailable,0,0,0);}
    }).ToArray();
}
