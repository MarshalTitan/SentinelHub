using SentinelHub.Ecosystem;
using SentinelHub.Health;
using System.Text;
using System.IO.Compression;
using SentinelHub.Diagnostics;
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
        test("Diagnostics ZIP composition projects fields and excludes known secrets and arbitrary provider text",()=>{
            const string secret="PRIVATE_CANARY_DISCORD_TOKEN_https://discord.com/api/webhooks/ACCOUNT_COOKIE";
            var h=new HealthView(DateTimeOffset.UtcNow,true,[new("SRankSentinel",HealthState.Ready,secret,secret,false,secret),new(secret,HealthState.Ready,secret,secret,true,secret)],null,secret);
            var state=StatusPolicy.Build([new("SRankSentinel",true,secret),new(secret,true,secret)],true,_=>Response.Ready);
            var provider=DiagnosticProviderPolicy.Parse("SRankSentinel","{\"Schema\":1,\"State\":1,\"Warnings\":2,\"Errors\":0,\"Token\":\""+secret+"\",\"Password\":\""+secret+"\",\"WebhookUrl\":\""+secret+"\",\"Cookie\":\""+secret+"\"}");
            var bytes=DiagnosticsBundle.Create(state,h,[],[provider,new(secret,ProviderState.Ready,1,0,0)],1,uint.MaxValue,uint.MaxValue);
            using var zip=new ZipArchive(new MemoryStream(bytes),ZipArchiveMode.Read);
            check(zip.Entries.Select(e=>e.FullName).SequenceEqual(new[]{"report.json","hub-state.json","summary.txt"}));
            foreach(var entry in zip.Entries){using var reader=new StreamReader(entry.Open());var text=reader.ReadToEnd();check(!text.Contains(secret)&&!text.Contains("PRIVATE_CANARY")&&!text.Contains("webhooks/"));}
            check(bytes.Length<65536&&provider.State==ProviderState.Ready&&provider.Warnings==2);
        });
        test("Diagnostic provider corruption, missing fields and unknown schemas fail closed",()=>{
            foreach(var json in new string?[]{null,"{","{}","{\"Schema\":2,\"State\":1,\"Warnings\":0,\"Errors\":0}","{\"Schema\":1,\"State\":999,\"Warnings\":0,\"Errors\":0}",new string('x',16385)})
                check(DiagnosticProviderPolicy.Parse("SentinelRelay",json).State==ProviderState.Unavailable);
            var failed=false;try{DiagnosticProviderPolicy.Parse("arbitrary",null);}catch{failed=true;}check(failed);
        });
    }
}
