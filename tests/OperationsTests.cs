using SentinelHub.Ecosystem;
using SentinelHub.Health;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using SentinelHub.Diagnostics;
using SentinelHub.Compatibility;
using SentinelHub.Utilities.Quest;
using SentinelHub.Inspection;
using SentinelHub.Identification;
using SentinelHub.Activity;
using SentinelHub.Storage;
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
            var bytes=DiagnosticsBundle.Create(state,h,[],[provider,new(secret,ProviderState.Ready,1,0,0)],1,uint.MaxValue,uint.MaxValue,
                compatibility:new([new(OverlapProvider.NoClippy,CompatibilityFeature.Latency,OverlapKind.Possible,true,secret)]),
                activity:[new(DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,"SentinelHub",ActivityCode.DiagnosticsSaved,1),new(DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,secret,ActivityCode.HuntCredited,1)]);
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
        test("Existing SRank status projects useful primitives while discarding private names/IDs/status text",()=>{
            const string raw="{\"Schema\":\"sentinel.srank.remote.v1\",\"Success\":true,\"Status\":{\"Loaded\":true,\"Enabled\":true,\"Ready\":true,\"QueueCount\":12,\"Recovering\":false,\"Character\":\"PRIVATE_NAME\",\"CharacterKey\":\"PRIVATE_CID\",\"ActiveHunt\":\"PRIVATE_TEXT\",\"StatusText\":\"SECRET_TOKEN\"}}";
            var safe=SRankStatusProjection.Parse(raw);check(safe.Available&&safe.Health==0&&safe.QueueCount==12);
            var json=JsonSerializer.Serialize(safe);check(!json.Contains("PRIVATE")&&!json.Contains("SECRET"));
            check(!SRankStatusProjection.Parse("{\"Success\":false}").Available&&!SRankStatusProjection.Parse("{").Available);
            check(SRankStatusProjection.Parse(raw.Replace("\"Enabled\":true","\"Enabled\":false")).Health==1);
        });
        test("Compatibility distinguishes definite/possible/inactive and suspends only Hub's overlapping features",()=>{
            var saved=new QuestConvenienceOptions{SkipDialogue=true,SkipCutscenes=true,QuestRewardSelection=QuestRewardSelectionMode.FirstReward};
            var view=new CompatibilityView(CompatibilityPolicy.TextAdvance(true,true,false,true,false,true));
            check(view.Findings.Count(x=>x.Kind==OverlapKind.Definite)==2);
            var effective=view.Effective(saved);check(!effective.SkipDialogue&&effective.SkipCutscenes&&effective.QuestRewardSelection==QuestRewardSelectionMode.Manual);
            check(saved.SkipDialogue&&saved.SkipCutscenes&&saved.QuestRewardSelection==QuestRewardSelectionMode.FirstReward);
            check(!new CompatibilityView(CompatibilityPolicy.TextAdvance(true,false,false,true,true,true)).Blocks(CompatibilityFeature.Dialogue));
            check(!new CompatibilityView(CompatibilityPolicy.TextAdvance(true,true,true,true,true,true)).Blocks(CompatibilityFeature.Dialogue));
            check(CompatibilityPolicy.TextAdvance(false,null,null,null,null,null).Length==0);
        });
        test("Compatibility unknown active state protects Hub and recovery restores effective saved preferences",()=>{
            var unknown=new CompatibilityView(CompatibilityPolicy.TextAdvance(true,null,null,null,null,null));
            check(unknown.Findings.All(f=>f.Kind==OverlapKind.Possible&&f.SuspendHub));
            var saved=new QuestConvenienceOptions{SkipDialogue=true};check(!unknown.Effective(saved).SkipDialogue);
            var recovered=new CompatibilityView(CompatibilityPolicy.TextAdvance(true,true,false,false,false,false));
            check(recovered.Effective(saved).SkipDialogue);
        });
        test("Inspector identifies only explicit supported emote IDs and rejects combat/pose/timeline stand-ins",()=>{
            InspectionEntry? Find(uint id)=>id==123?new(InspectionKind.Emote,id,"Lean",1,"/lean",true,[],"Unknown"):null;
            uint? Mode(uint id)=>id==123?25u:null;
            check(InspectionPolicy.Emote(new(true,true,false,123,3,25),Find,Mode).State==InspectionState.Found);
            foreach(var mode in new byte[]{0,1,2,4,8,11,16})check(InspectionPolicy.Emote(new(true,true,false,123,mode,25),Find,Mode).State==InspectionState.None);
            check(InspectionPolicy.Emote(new(true,true,false,123,3,26),Find,Mode).State==InspectionState.None);
            check(InspectionPolicy.Emote(new(true,true,false,0,3,25),Find,Mode).State==InspectionState.None);
            check(InspectionPolicy.Emote(new(true,true,false,999,3,25),Find,Mode).State==InspectionState.UnknownId);
            check(InspectionPolicy.Emote(new(true,false,false,123,3,25),Find,Mode).State==InspectionState.Unavailable);
            check(InspectionPolicy.Emote(new(true,true,true,123,3,25),Find,Mode).State==InspectionState.Unavailable);
        });
        test("Inspector session clears on stale/unloaded/reused actors, session/zone changes and time expiry",()=>{
            var t=new IdentificationTicket(1,2,3,4,5,100);
            check(InspectionPolicy.SessionMatches(t,1,2,3,4,5,200,true,true));
            check(!InspectionPolicy.SessionMatches(t,1,9,3,4,5,200,true,true));
            check(!InspectionPolicy.SessionMatches(t,1,2,3,9,5,200,true,true));
            check(!InspectionPolicy.SessionMatches(t,1,2,3,4,9,200,true,true));
            check(!InspectionPolicy.SessionMatches(t,1,2,3,4,5,300101,true,true));
            check(!InspectionPolicy.SessionMatches(t,1,2,3,4,5,200,true,false));
            check(InspectionPolicy.Resolve(InspectionKind.Mount,0,(_,_)=>throw new Exception()).State==InspectionState.None);
            check(InspectionPolicy.Resolve(InspectionKind.Fashion,999,(_,_)=>null).State==InspectionState.UnknownId);
            check(InspectorView.Empty.Emote.State==InspectionState.Unavailable);
        });
        test("Inspection acquisition snapshot covers all 201 emote identities with exact ID/name gates",()=>{
            check(InspectionMetadata.Emotes.Rows.Length==201);
            foreach(var fact in InspectionMetadata.Emotes.Rows){
                check(InspectionMetadata.Find(InspectionKind.Emote,fact.Id,fact.EnglishName)==fact);
                check(InspectionMetadata.Find(InspectionKind.Emote,fact.Id,fact.EnglishName+" changed") is null);
            }
            check(InspectionMetadata.Patch("7.55")=="7.55"&&InspectionMetadata.Expansion("3.55a")=="Heavensward");
            check(InspectionMetadata.Patch("unknown") is null&&InspectionMetadata.Expansion("8.0") is null);
            check(InspectionMetadata.Category(new("Premium","Online Store"))=="Online Store (recorded source)");
            var snapshotBad=InspectionMetadata.Emotes with{Rows=[InspectionMetadata.Emotes.Rows[0],InspectionMetadata.Emotes.Rows[0]]};
            var rejected=false;try{InspectionMetadata.Validate(snapshotBad,"Emote","emotes");}catch{rejected=true;}check(rejected);
        });
        test("Activity retention, grouping and severity are bounded and typed",()=>{
            var journal=new ActivityJournal();var now=DateTimeOffset.UtcNow;
            for(var i=0;i<1000;i++)journal.Add("SentinelHub",ActivityCode.DiagnosticsSaved,now);
            check(journal.Snapshot(now).Length==1&&journal.Snapshot(now)[0].Count==1000);
            for(var i=0;i<400;i++)journal.Add("SentinelHub",ActivityCode.DiagnosticsSaved,now.AddMinutes(i+1));
            check(journal.Snapshot(now.AddMinutes(401)).Length<=ActivityJournal.Capacity);
            check(journal.Snapshot(now.AddDays(8)).Length==0);
            check(ActivityCatalog.Severity(ActivityCode.DiagnosticsFailed)==ActivitySeverity.Error);
            check(ActivityCatalog.Severity(ActivityCode.HealthRecovered)==ActivitySeverity.Success);
            check(!journal.Add("PRIVATE_TOKEN",ActivityCode.HuntCredited,now));
            check(!journal.Add("SentinelRelay",ActivityCode.HuntCredited,now));
            check(!journal.Add("SentinelHub",(ActivityCode)9999,now));
        });
        test("Recent Activity is session-only and a new Hub service has no previous observations",()=>{
            var now=DateTimeOffset.UtcNow;var session=new ActivityService();
            session.Add(ActivityCode.DiagnosticsSaved);check(session.Journal.Snapshot(now.AddSeconds(1)).Length==1);
            var reloaded=new ActivityService();check(reloaded.Journal.Snapshot(now.AddSeconds(1)).Length==0);
            session.Journal.Clear();check(session.Journal.Snapshot(now.AddSeconds(1)).Length==0);
        });
        test("Legacy activity cleanup removes only the known history file and leaves settings/exports alone",()=>{
            var folder=Path.Combine(Path.GetTempPath(),"sentinel-history-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try{
                var old=Path.Combine(folder,"recent-activity.json");var settings=Path.Combine(folder,"SentinelHub.json");var export=Path.Combine(folder,"my-export.zip");
                File.WriteAllText(old,"old history");File.WriteAllText(settings,"settings");File.WriteAllText(export,"manual export");
                check(LegacyActivityCleanup.TryRemove(folder)&&!File.Exists(old));
                check(File.ReadAllText(settings)=="settings"&&File.ReadAllText(export)=="manual export");
                check(LegacyActivityCleanup.TryRemove(folder)&&Directory.GetFiles(folder).Length==2);
            }finally{Directory.Delete(folder,true);}
        });
        test("Explicit diagnostics exports replace one file instead of accumulating session archives",()=>{
            var folder=Path.Combine(Path.GetTempPath(),"sentinel-export-tests-"+Guid.NewGuid().ToString("N"));
            try{
                check(!Directory.Exists(folder));DiagnosticsFile.Save(folder,Encoding.UTF8.GetBytes("first export"));
                DiagnosticsFile.Save(folder,Encoding.UTF8.GetBytes("latest export"));
                check(Directory.GetFiles(folder).Length==1&&File.ReadAllText(Path.Combine(folder,DiagnosticsFile.FileName))=="latest export");
            }finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
        });
        test("Anti-AFK compact control reports actual readiness and never labels a blocked/disabled writer active",()=>{
            foreach(var state in Enum.GetValues<SentinelHub.Utilities.AntiAfkState>()){
                var view=new SentinelHub.Utilities.AntiAfkView(state,47,null);
                check(view.ControlLabel(false)=="Anti-AFK: Off");
                check((view.ControlLabel(true)=="Anti-AFK: Active")== (state==SentinelHub.Utilities.AntiAfkState.Active));
            }
        });
        test("Atomic storage injected replacement failure preserves original bytes and removes temporary files",()=>{
            var folder=Path.Combine(Path.GetTempPath(),"sentinel-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try{var path=Path.Combine(folder,"state.json");File.WriteAllText(path,"original");var failed=false;
                try{AtomicFile.Write(path,Encoding.UTF8.GetBytes("candidate"),(_,_)=>throw new IOException());}catch{failed=true;}
                check(failed&&File.ReadAllText(path)=="original"&&Directory.GetFiles(folder).Length==1);
                AtomicFile.Write(path,Encoding.UTF8.GetBytes("replacement"));check(File.ReadAllText(path)=="replacement");
            }finally{Directory.Delete(folder,true);}
        });
        test("Retired candidate settings and activity codes do not change retained preferences or identities",()=>{
            var config=JsonSerializer.Deserialize<SentinelHub.Configuration.HubSettings>("{\"Version\":1,\"Page\":12,\"AutomationPaused\":true,\"QuestConvenienceEnabled\":false,\"AntiAfkEnabled\":true,\"LatencyMode\":1}")!;
            config.Normalize();check(config.Page==SentinelHub.Configuration.HubPage.Dashboard&&!config.QuestConvenienceEnabled&&config.AntiAfkEnabled&&config.LatencyMode==SentinelHub.Utilities.LatencyMode.Measure);
            check(!JsonSerializer.Serialize(config).Contains("AutomationPaused"));
            var journal=new ActivityJournal();var now=DateTimeOffset.UtcNow;
            foreach(var code in new[]{5,6,7,10,11,12,13})check(!journal.Add("SentinelHub",(ActivityCode)code,now));
            check((int)ActivityCode.DiagnosticsSaved==8&&(int)ActivityCode.ProfileApplied==14&&(int)ActivityCode.HuntCredited==17);

        });
    }
}
