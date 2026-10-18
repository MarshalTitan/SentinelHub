using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using SentinelHub.Activity;
using SentinelHub.Diagnostics;
namespace SentinelHub.Coordination;
public sealed class HubOperationsIpc : IDisposable
{
    private readonly ICallGateProvider<string,int,bool>? publish;
    private readonly ICallGateProvider<int>? health;
    private readonly ICallGateProvider<string>? diagnostics;
    private bool disposed;
    public HubOperationsIpc(IDalamudPluginInterface pi,IFramework framework,ActivityJournal journal,FaultJournal faults)
    {
        try{
        publish=pi.GetIpcProvider<string,int,bool>("SentinelHub.Activity.Publish.v1");
        health=pi.GetIpcProvider<int>("Sentinel.SentinelHub.Health.v1");
        diagnostics=pi.GetIpcProvider<string>("Sentinel.SentinelHub.Diagnostics.v1");
        publish.RegisterFunc((source,code)=>{
            if(!framework.IsInFrameworkUpdateThread)return false;
            try{return Enum.IsDefined((ActivityCode)code)&&Ecosystem.Catalog.Components.Any(c=>c.Id==source)&&source!="SentinelHub"
                &&pi.InstalledPlugins.Any(p=>p.InternalName==source&&p.IsLoaded)&&journal.Add(source,(ActivityCode)code,DateTimeOffset.UtcNow);}
            catch{return false;}
        });
        health.RegisterFunc(()=>0);
        diagnostics.RegisterFunc(()=>System.Text.Json.JsonSerializer.Serialize(new{Schema=1,State=(int)ProviderState.Ready,Warnings=faults.Snapshot().Length,Errors=0}));
        }catch{Dispose();faults.Record(FaultCode.OperationsContractUnavailable);}
    }
    public void Dispose(){if(disposed)return;disposed=true;try{publish?.UnregisterFunc();}catch{}try{health?.UnregisterFunc();}catch{}try{diagnostics?.UnregisterFunc();}catch{}}
}
