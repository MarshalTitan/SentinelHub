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
using SentinelHub.Coordination;
using SentinelHub.Activity;
using SentinelHub.Storage;
using SentinelHub.Backups;
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
            check(InspectorView.Empty.Barding.State==InspectionState.Unsupported);
        });
        test("Pause requires acknowledgement, respects unsupported/missing/rejecting participants and resumes only its own lease",()=>{
            var paused=false;var writes=0;
            bool Set(bool value){writes++;paused=value;return true;}
            check(PausePolicy.Resolve("owner",false,true,true,()=>1,Set,()=>paused).State==PauseState.NotRequested&&writes==0);
            check(PausePolicy.Resolve("owner",true,true,true,()=>1,Set,()=>paused).State==PauseState.Acknowledged&&paused);
            check(PausePolicy.Resolve("owner",false,true,true,()=>1,Set,()=>paused,true).State==PauseState.Running&&!paused);
            check(PausePolicy.Resolve("owner",true,false,false,()=>throw new Exception(),Set,()=>false).State==PauseState.Missing);
            check(PausePolicy.Resolve("owner",true,true,true,()=>throw new Exception(),Set,()=>false).State==PauseState.Unsupported);
            check(PausePolicy.Resolve("owner",true,true,true,()=>1,_=>false,()=>false).State==PauseState.Rejected);
            check(PausePolicy.Resolve("owner",true,true,true,()=>1,_=>true,()=>false).State==PauseState.Pending);
            check(PausePolicy.Resolve("owner",false,true,true,()=>1,_=>true,()=>true,true).State==PauseState.RemainsPaused);
            check(!new PauseView(true,[new("owner",PauseState.Unsupported,"")]).FullyAcknowledged);
        });
        test("Activity retention, grouping and severity are bounded and typed",()=>{
            var journal=new ActivityJournal();var now=DateTimeOffset.UtcNow;
            for(var i=0;i<1000;i++)journal.Add("SentinelHub",ActivityCode.DiagnosticsSaved,now);
            check(journal.Snapshot(now).Length==1&&journal.Snapshot(now)[0].Count==1000);
            for(var i=0;i<400;i++)journal.Add("SentinelHub",ActivityCode.BackupSaved,now.AddMinutes(i+1));
            check(journal.Snapshot(now.AddMinutes(401)).Length<=ActivityJournal.Capacity);
            check(journal.Snapshot(now.AddDays(8)).Length==0);
            check(ActivityCatalog.Severity(ActivityCode.DiagnosticsFailed)==ActivitySeverity.Error);
            check(ActivityCatalog.Severity(ActivityCode.HealthRecovered)==ActivitySeverity.Success);
            check(!journal.Add("PRIVATE_TOKEN",ActivityCode.HuntCredited,now));
            check(!journal.Add("SentinelRelay",ActivityCode.HuntCredited,now));
            check(!journal.Add("SentinelHub",(ActivityCode)9999,now));
        });
        test("Activity persistence rejects corrupt/oversized/unknown data and does not retain arbitrary payloads",()=>{
            var journal=new ActivityJournal();var now=DateTimeOffset.UtcNow;
            journal.Add("SentinelHub",ActivityCode.DiagnosticsSaved,now);var json=journal.Serialize(now);
            var restored=new ActivityJournal();restored.Load(json,now);check(restored.Snapshot(now).Length==1);
            restored.Load("[{\"Source\":\"SECRET_TOKEN\",\"Code\":1,\"Count\":1}]",now);check(restored.Snapshot(now).Length==0&&!restored.Serialize(now).Contains("SECRET_TOKEN"));
            restored.Load("{",now);check(restored.Snapshot(now).Length==0);
            restored.Load(new string('x',256*1024+1),now);check(restored.Snapshot(now).Length==0);
        });
        test("Atomic storage injected replacement failure preserves original bytes and removes temporary files",()=>{
            var folder=Path.Combine(Path.GetTempPath(),"sentinel-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try{var path=Path.Combine(folder,"state.json");File.WriteAllText(path,"original");var failed=false;
                try{AtomicFile.Write(path,Encoding.UTF8.GetBytes("candidate"),(_,_)=>throw new IOException());}catch{failed=true;}
                check(failed&&File.ReadAllText(path)=="original"&&Directory.GetFiles(folder).Length==1);
                AtomicFile.Write(path,Encoding.UTF8.GetBytes("replacement"));check(File.ReadAllText(path)=="replacement");
            }finally{Directory.Delete(folder,true);}
        });
        test("Backup registry is unique, versioned and permits only reviewed non-text scalar fields",()=>{
            check(BackupRegistry.Owners.Length==7&&BackupRegistry.Owners.Select(o=>o.Id).Distinct().Count()==7);
            foreach(var owner in BackupRegistry.Owners){check(owner.Schema>0&&Version.TryParse(owner.Version,out _));check(owner.Fields.Select(f=>f.Path).Distinct().Count()==owner.Fields.Length);
                check(owner.Fields.All(f=>f.Path!="Version"&&!f.Path.Contains("Token")&&!f.Path.Contains("Password")&&!f.Path.Contains("Webhook")&&!f.Path.Contains("Character")&&!f.Path.Contains("Session")&&!f.Path.Contains("History")&&!f.Path.Contains("Profiles")&&!f.Path.Contains("Gearset")));
            }
            check(BackupRegistry.Find("SentinelRelay")!.Fields.All(f=>!f.Path.StartsWith("CharacterProfiles")));
            check(BackupRegistry.Find("SentinelHub")!.Fields.All(f=>f.Path!="AutomationPaused"));
        });
        test("Backup projections exclude credentials/ciphertext/IDs and restore preserves current excluded and unrelated fields",()=>{
            var relay=BackupRegistry.Find("SentinelRelay")!;
            var before=Encoding.UTF8.GetBytes("{\"Version\":5,\"ConfigurationTheme\":1,\"CharacterProfiles\":{\"PRIVATE_ID\":{\"ProtectedWebhookUrl\":\"SECRET_WEBHOOK\",\"ProtectedDiscordBotToken\":\"SECRET_BOT\",\"Password\":\"SECRET_PASSWORD\",\"Cookie\":\"SECRET_COOKIE\"}},\"ApiToken\":\"SECRET_API\"}");
            var entry=BackupPolicy.Project(relay,relay.Version,before);var json=BackupPolicy.Write(new(1,"test backup",DateTimeOffset.UtcNow,[entry]));
            var text=Encoding.UTF8.GetString(json);check(!text.Contains("SECRET")&&!text.Contains("PRIVATE_ID")&&!text.Contains("CharacterProfiles")&&!text.Contains("Token"));
            var current=Encoding.UTF8.GetBytes("{\"Version\":5,\"ConfigurationTheme\":0,\"FutureField\":987,\"CharacterProfiles\":{\"NEW_ID\":{\"ProtectedWebhookUrl\":\"CURRENT_SECRET\"}}}");
            var restored=Encoding.UTF8.GetString(BackupPolicy.Restore(entry,relay,relay.Version,current));
            using var doc=JsonDocument.Parse(restored);check(doc.RootElement.GetProperty("ConfigurationTheme").GetInt32()==1&&doc.RootElement.GetProperty("FutureField").GetInt32()==987&&restored.Contains("CURRENT_SECRET")&&!restored.Contains("SECRET_WEBHOOK"));
        });
        test("Backup schema/version/type/owner/checksum/duplicate/invalid payloads reject before restore",()=>{
            var owner=BackupRegistry.Find("SentinelHub")!;var entry=BackupPolicy.Project(owner,owner.Version,Encoding.UTF8.GetBytes("{\"Version\":1,\"Theme\":1}"));
            var rejected=0;
            foreach(var action in new Action[]{
                ()=>BackupPolicy.Restore(entry,owner,owner.Version,Encoding.UTF8.GetBytes("{\"Version\":2,\"Theme\":0}")),
                ()=>BackupPolicy.Restore(entry,owner,"0.8.0.0",Encoding.UTF8.GetBytes("{\"Version\":1}")),
                ()=>BackupPolicy.Validate(new(1,"test",DateTimeOffset.UtcNow,[entry with{Sha256="bad"}])),
                ()=>BackupPolicy.Validate(new(1,"test",DateTimeOffset.UtcNow,[entry,entry])),
                ()=>BackupPolicy.Parse(Encoding.UTF8.GetBytes("{\"Version\":1,\"Version\":2}")),
                ()=>BackupPolicy.Project(owner,owner.Version,Encoding.UTF8.GetBytes("{\"Version\":1,\"Theme\":\"SECRET\"}")),
                ()=>BackupPolicy.Parse(new byte[BackupPolicy.MaximumBytes+1]),
            }){try{action();}catch{rejected++;}}
            check(rejected==7&&!BackupPolicy.ValidName("https://discord.com/webhook")&&BackupPolicy.ValidName("Before restore 2026-10-18"));
        });
        test("Actual backup service creates sanitized package, rejects loaded owner and atomically restores one owner with pre-backup",()=>{
            var folder=Path.Combine(Path.GetTempPath(),"sentinel-backup-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try{
                var hub=Path.Combine(folder,"SentinelHub.json");var srank=Path.Combine(folder,"SRankSentinel.json");var root=Path.Combine(folder,"backups");
                File.WriteAllText(hub,"{\"Version\":1,\"Theme\":1,\"FutureField\":1}");
                File.WriteAllText(srank,"{\"Version\":16,\"WindowTheme\":1,\"Enabled\":true,\"FaloopProtectedPassword\":\"SECRET_CANARY\"}");
                var settings=new SentinelHub.Configuration.HubSettings{Theme=1,AutomationPaused=true};var loaded=true;var events=new List<ActivityCode>();
                var service=new BackupService(root,hub,settings,()=>[new("SRankSentinel",loaded,"0.7.62.0")],events.Add,()=>"0.7.0.0");
                service.RequestCreate("Before test");service.Tick();check(service.View.Items.Length==1);
                var backup=service.View.Items[0];check(backup.Owners.Contains("SRankSentinel")&&!File.ReadAllText(Directory.GetFiles(root).Single()).Contains("SECRET_CANARY"));
                var original=File.ReadAllText(srank);service.RequestRestore(backup.Id,"SRankSentinel");service.Tick();check(File.ReadAllText(srank)==original&&events.Last()==ActivityCode.BackupFailed);
                loaded=false;File.WriteAllText(srank,"{\"Version\":16,\"WindowTheme\":0,\"Enabled\":false,\"FutureField\":9,\"FaloopProtectedPassword\":\"CURRENT_SECRET\"}");
                service.RequestRestore(backup.Id,"SRankSentinel");service.Tick();var restored=File.ReadAllText(srank);
                check(restored.Contains("CURRENT_SECRET")&&!restored.Contains("SECRET_CANARY")&&restored.Contains("FutureField")&&events.Last()==ActivityCode.BackupRestored&&service.View.Items.Length==2);
                using(var doc=JsonDocument.Parse(restored))check(doc.RootElement.GetProperty("Enabled").GetBoolean()&&doc.RootElement.GetProperty("FutureField").GetInt32()==9);
                File.WriteAllText(hub,"{\"Version\":1,\"Theme\":0,\"FutureField\":99}");settings.Theme=0;service.RequestRestore(backup.Id,"SentinelHub");service.Tick();
                check(settings.Theme==1&&settings.AutomationPaused&&File.ReadAllText(hub).Contains("FutureField"));
                var rejected=File.ReadAllText(hub);var failing=new BackupService(root,hub,settings,()=>[],events.Add,()=>"0.7.0.0",(_,_)=>throw new IOException());
                failing.RequestRestore(backup.Id,"SentinelHub");failing.Tick();check(File.ReadAllText(hub)==rejected&&events.Last()==ActivityCode.BackupFailed);
                check(!Directory.GetFiles(folder,".sentinel-*.tmp").Any());
            }finally{Directory.Delete(folder,true);}
        });
    }
}
