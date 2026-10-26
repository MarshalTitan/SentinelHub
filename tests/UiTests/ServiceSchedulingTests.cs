using System.Text.Json;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SentinelHub.Compatibility;
using SentinelHub.Configuration;
using SentinelHub.Diagnostics;
using SentinelHub.Ecosystem;
using SentinelHub.Profiles;
using SentinelHub.Utilities;
using SentinelHub.Utilities.AntiAfk;
using SentinelHub.Utilities.Quest;

internal static class ServiceSchedulingTests
{
    private static void Check(bool value,string why){if(!value)throw new Exception(why);}
    public static void Run()
    {
        Discovery();Ownership();Compatibility();Profiles();
        Console.WriteLine("PASS shared inventory scheduling, dormant/legacy ownership, pre-enable conflict protection and event-driven Profiles IPC lifecycle.");
    }
    private static void Discovery()
    {
        var reads=0;Delegate? changed=null;
        var pi=IdlePerformanceTests.Fake<IDalamudPluginInterface>((m,args)=>{
            if(m.Name=="get_InstalledPlugins")reads++;
            if(m.Name=="add_ActivePluginsChanged")changed=(Delegate)args![0]!;
            return IdlePerformanceTests.Default(m.ReturnType);
        });
        using var discovery=new PluginDiscovery(pi,new());
        discovery.Refresh();var initial=discovery.Inventory;
        for(var i=0;i<10000;i++)discovery.Refresh();
        Check(reads==1,"Unchanged inventory was enumerated each frame.");
        changed!.DynamicInvoke([null]);discovery.Refresh();Check(reads==2&&ReferenceEquals(initial,discovery.Inventory),"Notification did not refresh/reuse identical inventory.");
        discovery.Refresh(true);Check(reads==3,"Explicit inventory refresh was ignored.");
    }
    private static void Ownership()
    {
        var ipc=0;var claims=0;var releases=0;var captures=0;
        var pi=IdlePerformanceTests.Fake<IDalamudPluginInterface>((m,args)=>{
            if(m.Name=="get_InstalledPlugins")throw new Exception("Controller bypassed shared inventory.");
            if(m.Name=="GetIpcSubscriber"){
                var name=(string)args![0]!;
                return IdlePerformanceTests.Proxy(m.ReturnType,(call,_)=>{
                    if(call.Name=="InvokeFunc"){
                        ipc++;
                        if(name.EndsWith("ProtocolVersion"))return 1;
                        if(name.EndsWith("Preference"))return 0;
                        if(name.EndsWith("TryClaim")){claims++;return true;}
                        if(name.EndsWith("Release")){releases++;return true;}
                    }
                    return IdlePerformanceTests.Default(call.ReturnType);
                });
            }
            return IdlePerformanceTests.Default(m.ReturnType);
        });
        var client=IdlePerformanceTests.Fake<IClientState>((m,_)=>m.Name=="get_IsLoggedIn"?true:IdlePerformanceTests.Default(m.ReturnType));
        InstalledPlugin[] all=[new("SentinelHUD",true,"0.8.4.7")];
        InstalledPlugin[] Inventory(){captures++;return all;}
        var settings=new HubSettings();
        using var quest=new QuestConvenienceController(pi,client,IdlePerformanceTests.Fake<IGameGui>(),IdlePerformanceTests.Fake<ICondition>(),
            IdlePerformanceTests.Fake<IObjectTable>(),IdlePerformanceTests.Fake<IDataManager>(),settings,new(),inventory:Inventory);
        using var afk=new AntiAfkController(pi,client,settings,new(),Inventory);
        for(var i=0;i<10000;i++){quest.Tick();afk.Tick();}
        Check(ipc==0&&captures==0,"Off ownership controllers did work.");
        settings.QuestConvenienceEnabled=true;quest.Tick();
        Check(claims==1&&quest.View.State==QuestConvenienceState.Active,"Legacy Quest lease was not claimed before activation.");
        for(var i=0;i<10000;i++)quest.Tick();
        Check(claims<5,"Legacy ownership IPC is still per-frame.");
        settings.QuestConvenienceEnabled=false;quest.Tick();Check(releases==1,"Off did not release the lease immediately.");
        settings.QuestConvenienceEnabled=true;all=[new("SentinelHUD",false,"0.8.3.0"),new("SentinelHUD",true,"0.8.5.2",IsDev:true)];quest.Tick();
        Check(quest.View.State==QuestConvenienceState.LegacyHudInstalled,"Mixed legacy inventory bypassed ownership protection.");
        all=[new("SentinelHUD",true,"0.8.5.2")];quest.Tick();
        Check(quest.View.State==QuestConvenienceState.Active,"Modern engine-free HUD was blocked.");
    }
    private static void Compatibility()
    {
        var scans=0;var available=true;InstalledPlugin[] all=[];
        var pi=IdlePerformanceTests.Fake<IDalamudPluginInterface>();
        var scanner=new CompatibilityScanner(pi,()=>all,()=>available,_=>{scans++;return LatencyState.XivAlexanderConflict;});
        for(var i=0;i<10000;i++)scanner.Tick();Check(scans==0,"Latency Off enumerated modules.");
        scanner.Tick(latencyEnabled:true);Check(scans==1&&scanner.View.Blocks(CompatibilityFeature.Latency),"Pre-enable native conflict check missing.");
        scanner.Tick(force:true);Check(scans==1&&scanner.View.Blocks(CompatibilityFeature.Latency),"Off discarded cached protection or scanned modules.");
        all=[new("NoClippy",true,"1.0.0.0")];scanner.Tick();
        Check(scanner.View.Findings.Any(f=>f.Provider==OverlapProvider.NoClippy&&f.SuspendHub),"Plugin notification missed NoClippy.");
        available=false;scanner.Tick();Check(scanner.View.Blocks(CompatibilityFeature.Latency)&&scanner.View.Blocks(CompatibilityFeature.Dialogue),"Unavailable inventory failed open.");
        all=[];available=true;
        var actual=new CompatibilityScanner(pi,()=>all,()=>available);
        actual.Tick(latencyEnabled:true);
        Check(actual.LatencyReadiness()==LatencyState.Compensating,"Native fixture did not establish fresh eligibility.");
        all=[new("NoClippy",true,"1.0.0.0")];
        Check(actual.LatencyReadiness()==LatencyState.ConflictCheckUnavailable,"Unconsumed inventory change reused stale native eligibility.");
        actual.Tick(latencyEnabled:true);Check(actual.LatencyReadiness()==LatencyState.NoClippyConflict,"Refreshed inventory failed to protect native writes.");
    }
    private static void Profiles()
    {
        var reads=0;var opens=0;var unsubscribed=0;Action<long>? changed=null;
        var id=Guid.NewGuid().ToString("D");var json=Payload(id,"Hunts",1);
        InstalledPlugin[] all=[new("SentinelProfiles",true,"0.2.1.8")];
        var pi=IdlePerformanceTests.Fake<IDalamudPluginInterface>((m,args)=>{
            if(m.Name=="get_InstalledPlugins")throw new Exception("Profiles integration bypassed shared inventory.");
            if(m.Name=="GetIpcSubscriber"){
                var endpoint=(string)args![0]!;
                return IdlePerformanceTests.Proxy(m.ReturnType,(call,values)=>{
                    if(call.Name=="Subscribe")changed=(Action<long>)values![0]!;
                    if(call.Name=="Unsubscribe")unsubscribed++;
                    if(call.Name=="InvokeFunc"&&endpoint.EndsWith("Snapshot")){reads++;return json;}
                    if(call.Name=="InvokeFunc"&&endpoint.EndsWith("Open")){opens++;Check(string.Equals(values?[0] as string,id,StringComparison.Ordinal),"Open used wrong stable ID.");return true;}
                    return IdlePerformanceTests.Default(call.ReturnType);
                });
            }
            return IdlePerformanceTests.Default(m.ReturnType);
        });
        var integration=new ProfilesIntegration(pi,()=>all,()=>true);
        for(var i=0;i<10000;i++)integration.Tick(false);Check(reads==0,"Hidden Profiles page polled full profiles.");
        integration.Tick(true);Check(reads==1&&integration.View.Selected=="Hunts","Opening page did not fetch authoritative Profiles.");
        for(var i=0;i<10000;i++)integration.Tick(true);Check(reads==1,"Visible unchanged Profiles page polled every frame.");
        json=Payload(id,"Gathering",2);changed!(2);integration.Tick(true);Check(reads==2&&integration.View.Selected=="Gathering","Change notification did not refresh cached snapshot.");
        integration.RequestOpen(id);Check(opens==0,"UI callback directly invoked Profiles action.");integration.Tick(true);Check(opens==1,"Queued profile Open not delivered.");
        all=[new("SentinelProfiles",false,"0.2.1.8")];integration.Tick(false);Check(integration.View.Availability==ProfilesAvailability.Disabled&&integration.View.Profiles.Length==0,"Unloaded provider retained live state.");
        all=[];integration.Tick(false);Check(integration.View.Availability==ProfilesAvailability.Missing,"Missing Profiles mislabeled.");
        all=[new("SentinelProfiles",true,"0.2.1.7")];json="";integration.Tick(true);Check(integration.View.Availability==ProfilesAvailability.Unavailable,"Unsupported IPC did not degrade clearly.");
        integration.Dispose();Check(unsubscribed==1,"Profiles notification handler leaked on unload.");
    }
    private static string Payload(string id,string name,int revision)=>JsonSerializer.Serialize(new{Schema=1,Revision=revision,Profiles=new[]{new{Id=id,Name=name}},SelectedProfileId=id,LastAppliedProfileId=(string?)null,Match="NoProfileApplied",Applying=false});
}
