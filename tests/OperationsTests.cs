using SentinelHub.Ecosystem;
using SentinelHub.Health;
using System.Text;
internal static class OperationsTests
{
    public static void Run(Action<string,Action> test,Action<bool> check)
    {
        var component=Catalog.Find("SRankSentinel")!;
        var versions=new Dictionary<string,string>{{"SRankSentinel","0.7.62.0"}};
        test("Health distinguishes missing/not-loaded/loaded/responding and discovery failure",()=>{
            check(HealthPolicy.Plugin(component,[],true,versions).State==HealthState.Missing);
            check(HealthPolicy.Plugin(component,[new("SRankSentinel",false,"0.7.62.0")],true,versions).State==HealthState.Disabled);
            check(HealthPolicy.Plugin(component,[new("SRankSentinel",true,"0.7.62.0")],true,versions).State==HealthState.Loaded);
            check(HealthPolicy.Plugin(component,[new("SRankSentinel",true,"0.7.62.0")],true,versions,0).State==HealthState.Ready);
            check(HealthPolicy.Plugin(component,[],false,versions).State==HealthState.Unknown);
            check(HealthPolicy.Plugin(component,[new("SRankSentinel",true,"0.7.62.0")],true,versions,-1).State==HealthState.IpcUnavailable);
        });
        test("Health runtime development override is separate from installed public version",()=>{
            var h=HealthPolicy.Plugin(component,[new("SRankSentinel",false,"0.7.62.0"),new("SRankSentinel",true,"0.7.99.0",IsDev:true)],true,versions);
            check(h.State==HealthState.DevelopmentBuild&&h.DevelopmentOverride&&h.RuntimeVersion=="0.7.99.0"&&h.PublicVersion=="0.7.62.0");
            check(HealthPolicy.Plugin(component,[new("SRankSentinel",true,"0.7.61.0")],true,versions).State==HealthState.VersionMismatch);
        });
        test("Dependency installed does not prove IPC/readiness",()=>{
            var d=Catalog.Dependencies[0];
            check(HealthPolicy.Dependency(new(d,Presence.Missing,"Unknown",Response.Unknown)).State==HealthState.DependencyMissing);
            check(HealthPolicy.Dependency(new(d,Presence.Disabled,"1.0",Response.Unknown)).State==HealthState.DependencyDisabled);
            check(HealthPolicy.Dependency(new(d,Presence.Loaded,"1.0",Response.Unavailable)).State==HealthState.IpcUnavailable);
            check(HealthPolicy.Dependency(new(d,Presence.Loaded,"1.0",Response.NotReady)).State==HealthState.Degraded);
        });
        test("Public version parsing rejects duplicate/invalid/oversized catalogs and projects only known identities",()=>{
            var good=Encoding.UTF8.GetBytes("[{\"InternalName\":\"SRankSentinel\",\"AssemblyVersion\":\"0.7.62.0\",\"Token\":\"SECRET\"},{\"InternalName\":\"other\",\"AssemblyVersion\":\"PRIVATE\"}]");
            check(PublicCatalog.Parse(good).Count==1);
            foreach(var bad in new[]{"{}","[]","[{\"InternalName\":\"SRankSentinel\",\"AssemblyVersion\":\"secret\"}]","[{\"InternalName\":\"SRankSentinel\",\"AssemblyVersion\":\"1.0\"},{\"InternalName\":\"SRankSentinel\",\"AssemblyVersion\":\"2.0\"}]"}){
                var rejected=false;try{PublicCatalog.Parse(Encoding.UTF8.GetBytes(bad));}catch{rejected=true;}check(rejected);
            }
        });
    }
}
