using SentinelHub.Fashion;
using SentinelHub.Identification;
using SentinelHub.Utilities.Quest;
using SentinelHub.Mounts;
using System.Text.Json;
using SentinelHub.Configuration;
using SentinelHub.Ecosystem;
using SentinelHub.Diagnostics;
using SentinelHub.Utilities;
var passed = 0;
void Test(string name, Action action) { action(); Console.WriteLine("PASS " + name); passed++; }
void Check(bool value) { if (!value) throw new Exception("Assertion failed"); }
InstalledPlugin Loaded(string id, string? version = "1.2.3.4") => new(id, true, version);
Test("all components missing", () => {
 var s = StatusPolicy.Build([], true, _ => throw new Exception());
 Check(s.Plugins.All(p => p.State == Presence.Missing));
 Check(s.Dependencies.All(p => p.State == Presence.Missing && p.Response == Response.Unknown));
});
Test("failed discovery is unknown, never missing", () => {
 var s = StatusPolicy.Build([Loaded("SRankSentinel")], false, _ => throw new Exception());
 Check(s.Plugins.All(p => p.State == Presence.Unknown));
});
Test("disabled and loaded are distinct", () => {
 var s = StatusPolicy.Build([new("SentinelHUD", false, "0.8.4.5"), Loaded("SRankSentinel")], true, _ => Response.Unknown);
 Check(s.Plugins.Single(p => p.Component.Id == "SentinelHUD").State == Presence.Disabled);
 Check(s.Plugins.Single(p => p.Component.Id == "SRankSentinel").State == Presence.Loaded);
});
Test("restricted installer state is degraded", () => {
 Check(StatusPolicy.PresenceOf(new("SentinelHUD", false, "1.0", true), true) == Presence.Degraded);
});
Test("case-insensitive discovery and dependency aliases", () => {
 var s = StatusPolicy.Build([Loaded("sonar"), Loaded("rotationsolverreborn"), Loaded("sentinelprofiles")], true, _ => Response.Responding);
 Check(s.Dependencies.Single(p => p.Dependency.Id == "SonarPlugin").State == Presence.Loaded);
 Check(s.Dependencies.Single(p => p.Dependency.Id == "RotationSolver").Response == Response.Responding);
 Check(s.Plugins.Single(p => p.Component.Id == "SentinelProfiles").State == Presence.Loaded);
});
Test("only enabled verified probes are called", () => {
 var calls = new List<string>();
 var s = StatusPolicy.Build([Loaded("vnavmesh"), new("Lifestream", false, "1.0"), Loaded("Splatoon")], true, d => { calls.Add(d.Id); return Response.NotReady; });
 Check(calls.SequenceEqual(new[] { "vnavmesh", "Splatoon" }));
 Check(s.Dependencies.Single(d => d.Dependency.Id == "vnavmesh").Response == Response.NotReady);
});
Test("responding does not claim ready or combat-active", () => {
 var s = StatusPolicy.Build([Loaded("RotationSolver")], true, _ => Response.Responding);
 Check(StatusPolicy.DependencyText(s.Dependencies.Single(d => d.Dependency.Id == "RotationSolver")) == "Responding");
});
Test("optional absent dependencies do not alter consumer loaded state", () => {
 var s = StatusPolicy.Build([Loaded("SentinelHUD")], true, _ => Response.Unknown);
 Check(s.Plugins.Single(p => p.Component.Id == "SentinelHUD").State == Presence.Loaded);
});
Test("versions are numeric and arbitrary payloads rejected", () => {
 Check(StatusPolicy.NumericVersion("0.7.62.0") == "0.7.62.0");
 foreach (var bad in new[] { "", "secret-webhook", "https://secret.example/123", new string('9', 200) })
   Check(StatusPolicy.NumericVersion(bad) == "Unknown");
});
Test("quick-open denies unknown, missing, disabled, restricted and discovery failure", () => {
 var c = Catalog.Find("SentinelHUD");
 Check(!StatusPolicy.CanOpen(null, [Loaded("SentinelHUD")], true));
 Check(!StatusPolicy.CanOpen(c, [], true));
 Check(!StatusPolicy.CanOpen(c, [new("SentinelHUD", false, "1.0")], true));
 Check(!StatusPolicy.CanOpen(c, [new("SentinelHUD", true, "1.0", true)], true));
 Check(!StatusPolicy.CanOpen(c, [Loaded("SentinelHUD")], false));
 Check(StatusPolicy.CanOpen(c, [Loaded("sentinelhud")], true));
});
Test("fresh unload observation refuses formerly loaded consumer", () => {
 var c = Catalog.Find("SentinelHUD");
 Check(StatusPolicy.CanOpen(c, [Loaded("SentinelHUD")], true));
 Check(!StatusPolicy.CanOpen(c, [new("SentinelHUD", false, "1.0")], true));
});
Test("settings commands contain no gameplay actions", () => {
 Check(Catalog.Components.Select(c => c.SettingsCommand).SequenceEqual(new[] {
 "/sranksentinel", "/pvpsentinel config", "/shud", "/classysentinel config", "/sprofiles", "/srelay", "/shub" }));
 Check(Catalog.Components.Count(c => c.DiagnosticsCommand is not null) == 1);
});
Test("settings normalize corrupt values", () => {
 var c = new HubSettings { Theme = 99, Page = (HubPage)99, ExpandedWidth = float.NaN, ExpandedHeight = -200 };
 c.Normalize(); Check(c.Theme == 1 && c.Page == HubPage.Dashboard && c.ExpandedWidth == 860 && c.ExpandedHeight == 500);
});
Test("appearance, page and expanded size persist without utility flags", () => {
 var c = new HubSettings { Theme = 0, Page = HubPage.Dependencies, ReducedMotion = true, Collapsed = true, ExpandedWidth = 1100, ExpandedHeight = 900 };
 var json = JsonSerializer.Serialize(c);
 var result = JsonSerializer.Deserialize<HubSettings>(json)!; result.Normalize();
 Check(JsonSerializer.Serialize(result) == json);
 Check(!result.AntiAfkEnabled && !result.AntiAfkPreferenceImported && result.LatencyMode == LatencyMode.Off);
});
Test("faults are bounded and throttled by typed reason", () => {
 var journal = new FaultJournal();
 for (var i = 0; i < 1000; i++) journal.Record(FaultCode.DiscoveryUnavailable);
 Check(journal.Snapshot().Length == 1);
 journal.Clear(); Check(journal.Snapshot().Length == 0);
 journal.Record(FaultCode.DiscoveryUnavailable); Check(journal.Snapshot().Length == 1);
});
Test("diagnostics exclude arbitrary names, paths, secrets and private inventory", () => {
 var secret = "https://discord.com/api/webhooks/PRIVATE_TOKEN";
 var unknown = new Component(secret, secret, secret, secret);
 var baseSnapshot = StatusPolicy.Build([Loaded("SentinelHUD", secret), Loaded(secret)], true, _ => Response.Unknown);
 var s = baseSnapshot with { Plugins = [..baseSnapshot.Plugins, new(unknown, Presence.Loaded, secret)] };
 var text = DiagnosticExport.Serialize(s, [new(DateTimeOffset.UtcNow, (FaultCode)900)]);
 using var doc = JsonDocument.Parse(text);
 Check(!text.Contains("PRIVATE_TOKEN") && !text.Contains("webhooks"));
 Check(doc.RootElement.GetProperty("Faults").GetArrayLength() == 0);
 Check(doc.RootElement.GetProperty("Schema").GetString() == "sentinel.hub.v1");
 Check(doc.RootElement.GetProperty("Profiles").GetProperty("Status").GetString() == "Unknown");
});
Test("remaining utility plans are inactive and compensation is explicit", () => {
 Check(UtilityRegistry.Planned.Length == 0);
 Check(UtilityRegistry.Planned.All(u => u.Status == "Planned / inactive"));
 Check(typeof(HubSettings).GetProperties().All(p => !p.Name.Contains("Compensation")));
});

Test("quick-open dispatch is fixed, denied actions do not execute", () => {
 var sent = new List<string>(); var loaded = true; var faults = new FaultJournal();
 var router = new QuickOpen(() => [new("PvPSentinel", loaded, "1.0")], cmd => { sent.Add(cmd); return true; }, faults);
 Check(router.Open("PvPSentinel") && sent.Single() == "/pvpsentinel config");
 Check(router.Open("PvPSentinel", true) && sent.Last() == "/pvpsentinel");
 loaded = false; Check(!router.Open("PvPSentinel") && sent.Count == 2);
 Check(!router.Open("/arbitrary enable") && sent.Count == 2);
});
Test("quick-open command and capture exceptions are isolated", () => {
 var faults = new FaultJournal();
 Check(!new QuickOpen(() => [Loaded("SentinelHUD")], _ => throw new Exception("PRIVATE"), faults).Open("SentinelHUD"));
 Check(!new QuickOpen(() => throw new Exception("PRIVATE"), _ => true, faults).Open("SentinelHUD"));
 Check(faults.Snapshot().All(f => f.Code == FaultCode.OpenSettingsUnavailable));
 Check(!DiagnosticExport.Serialize(HubSnapshot.Unknown, faults.Snapshot()).Contains("PRIVATE"));
});
Test("unsupported diagnostics and refused commands fail without guessing", () => {
 var calls = 0; var faults = new FaultJournal();
 var router = new QuickOpen(() => [Loaded("SentinelHUD")], _ => { calls++; return false; }, faults);
 Check(!router.Open("SentinelHUD", true) && calls == 0);
 Check(!router.Open("SentinelHUD") && calls == 1 && faults.Snapshot().Length == 1);
});


Test("Hub absence does not change other components' status or availability", () => {
 var consumers = Catalog.Components.Where(c => c.Id != "SentinelHub").Select(c => Loaded(c.Id)).ToArray();
 var without = StatusPolicy.Build(consumers, true, _ => Response.Unknown);
 var with = StatusPolicy.Build([..consumers, Loaded("SentinelHub")], true, _ => Response.Unknown);
 Check(without.Plugins.Where(p => p.Component.Id != "SentinelHub").SequenceEqual(with.Plugins.Where(p => p.Component.Id != "SentinelHub")));
 Check(without.Dependencies.SequenceEqual(with.Dependencies));
 Check(StatusPolicy.CanOpen(Catalog.Find("SentinelHUD"), consumers, true));
});


Test("Anti-AFK remains default OFF on new settings and old JSON upgrades", () => {
 Check(!new HubSettings().AntiAfkEnabled);
 Check(!JsonSerializer.Deserialize<HubSettings>("{\"Version\":1}")!.AntiAfkEnabled);
 Check(!Catalog.Components.Any(c => c.Id == "SentinelHunts"));
 Check(!Catalog.Dependencies.Any(d => d.Uses.Contains("Sentinel Hunts")));
});
Test("Anti-AFK requires login, supported HUD and fresh acknowledgment", () => {
 AntiAfkState Decide(bool enabled=true, bool login=true, bool installed=false, bool loaded=false, string? version=null, bool ack=false) =>
  AntiAfkOwnershipPolicy.Resolve(enabled, login, installed, loaded, version, ack);
 Check(Decide(false) == AntiAfkState.Off && Decide(login:false) == AntiAfkState.WaitingForLogin);
 Check(Decide(installed:true, version:"0.8.4.5") == AntiAfkState.LegacyHudInstalled);
 Check(Decide(installed:true, version:null) == AntiAfkState.LegacyHudInstalled);
 Check(Decide(installed:true, loaded:true, version:"0.8.4.6") == AntiAfkState.WaitingForHud);
 Check(Decide(installed:true, loaded:true, version:"0.8.4.6", ack:true) == AntiAfkState.Active);
 Check(Decide() == AntiAfkState.Active);
 Check(Decide(installed:true, loaded:false, version:"0.8.4.6") == AntiAfkState.Active);
});
Test("Anti-AFK timer adapter never runs off, logged out or without ownership", () => {
 int calls=0; var engine=new AntiAfkEngine(() => { calls++; return true; });
 foreach(var state in Enum.GetValues<AntiAfkState>().Where(s=>s!=AntiAfkState.Active)) engine.Tick(state,100000);
 Check(calls==0);
 engine.Tick(AntiAfkState.Active,100000); engine.Tick(AntiAfkState.Active,100001);
 Check(calls==1 && engine.Cycles==1);
 engine.Tick(AntiAfkState.Active,109999); Check(calls==1);
 engine.Tick(AntiAfkState.Active,110000); Check(calls==2);
 engine.Suspend(); Check(engine.State==AntiAfkState.Off);
});
Test("Anti-AFK preserves the accepted 30-second threshold with bounded faults", () => {
 Check(!AntiAfkEngine.ShouldReset(30) && AntiAfkEngine.ShouldReset(30.01f));
 Check(!AntiAfkEngine.ShouldReset(float.NaN) && !AntiAfkEngine.ShouldReset(float.PositiveInfinity));
 int calls=0;var engine=new AntiAfkEngine(()=>{calls++;throw new Exception("PRIVATE");});
 engine.Tick(AntiAfkState.Active,0);Check(engine.State==AntiAfkState.Faulted);
 engine.Tick(AntiAfkState.Active,9999);Check(calls==1 && engine.State==AntiAfkState.Faulted);
 engine.Tick(AntiAfkState.Active,10000);Check(calls==2);
 var missing=new AntiAfkEngine(()=>false);missing.Tick(AntiAfkState.Active,0);Check(missing.State==AntiAfkState.TimerUnavailable);
});
Test("Anti-AFK import round-trips only explicit preference and export excludes lease identities", () => {
 var config=new HubSettings { AntiAfkEnabled=true, AntiAfkPreferenceImported=true };
 var round=JsonSerializer.Deserialize<HubSettings>(JsonSerializer.Serialize(config))!;
 Check(round.AntiAfkEnabled && round.AntiAfkPreferenceImported);
 var export=DiagnosticExport.Serialize(HubSnapshot.Unknown,[],new((AntiAfkState)999,4,true));
 Check(export.Contains("\"State\": \"Unknown\"") && !export.Contains("HudPreference") && !export.Contains("token"));
});
Test("actual HUD lease and Hub engine simulate both callback orders and handoff", () => {
 foreach(var hudFirst in new[]{true,false}) {
  var lease=new SentinelHUD.Core.AntiAfkLease();var token=Guid.NewGuid().ToString("N");
  int writes=0;var engine=new AntiAfkEngine(()=>{writes++;return true;});bool requested=true;
  bool HudMayWrite(long now) => !SentinelHUD.Core.AntiAfkLease.ShouldYield(lease.IsClaimed(now),true,"0.2.0.0",requested);
  if(hudFirst) Check(!HudMayWrite(0));
  var ack=lease.TryClaim(token,0,requested);
  engine.Tick(AntiAfkOwnershipPolicy.Resolve(true,true,true,true,"0.8.4.6",ack),0);
  Check(!HudMayWrite(0) && writes==1);
  Check(!HudMayWrite(5000));
  var replacement=Guid.NewGuid().ToString("N");Check(lease.TryClaim(replacement,5000,true));
  Check(!lease.Release(token));
  engine.Suspend();requested=false;Check(lease.Release(replacement));
  Check(HudMayWrite(5001) && engine.State==AntiAfkState.Off);
 }
});


Test("Quest defaults, explicit import and normalization preserve approved Anti-AFK settings", () => {
 var old=JsonSerializer.Deserialize<HubSettings>("{\"Version\":1,\"AntiAfkEnabled\":true}")!;old.Normalize();
 Check(old.AntiAfkEnabled && !old.QuestConvenienceEnabled && !old.QuestDialogue && !old.QuestCutscenes && old.QuestRewardMode==QuestRewardSelectionMode.Manual);
 old.ImportQuestPreferences(11);Check(old.QuestConvenienceEnabled && old.QuestDialogue && old.QuestCutscenes && old.QuestRewardMode==QuestRewardSelectionMode.CurrentJobReward && old.AntiAfkEnabled);
 var round=JsonSerializer.Deserialize<HubSettings>(JsonSerializer.Serialize(old))!;round.Normalize();
 Check(round.QuestPreferenceImported && round.QuestRewardMode==old.QuestRewardMode && round.AntiAfkEnabled);
 var before=JsonSerializer.Serialize(round);round.ImportQuestPreferences(999);Check(before==JsonSerializer.Serialize(round));
 round.QuestRewardMode=(QuestRewardSelectionMode)999;round.Normalize();Check(round.QuestRewardMode==QuestRewardSelectionMode.Manual);
 round.ImportQuestPreferences(0);Check(!round.QuestConvenienceEnabled && round.AntiAfkEnabled);
});
Test("Quest ownership never enables native work on legacy, unloaded or unacknowledged discovery failures", () => {
 Check(QuestConvenienceOwnershipPolicy.Resolve(false,true,false,false,null,false)==QuestConvenienceState.Off);
 Check(QuestConvenienceOwnershipPolicy.Resolve(true,false,false,false,null,false)==QuestConvenienceState.WaitingForLogin);
 Check(QuestConvenienceOwnershipPolicy.Resolve(true,true,true,false,"0.8.4.6",false)==QuestConvenienceState.LegacyHudInstalled);
 Check(QuestConvenienceOwnershipPolicy.Resolve(true,true,true,true,null,true)==QuestConvenienceState.LegacyHudInstalled);
 Check(QuestConvenienceOwnershipPolicy.Resolve(true,true,true,true,"0.8.4.7",false)==QuestConvenienceState.WaitingForHud);
 Check(QuestConvenienceOwnershipPolicy.Resolve(true,true,true,true,"0.8.4.7",true)==QuestConvenienceState.Active);
 Check(QuestConvenienceOwnershipPolicy.Resolve(true,true,false,false,null,false)==QuestConvenienceState.Active);
 Check(QuestConvenienceOwnershipPolicy.Resolve(true,true,true,false,"0.8.4.7",false)==QuestConvenienceState.Active);
});
Test("Quest session revokes pending work on ownership loss, option changes and faults", () => {
 int actions=0,resets=0;bool pending=false;
 var session=new QuestExecutionSession(_=>{actions++;pending=true;},()=>{resets++;pending=false;});
 foreach(var state in Enum.GetValues<QuestConvenienceState>().Where(x=>x!=QuestConvenienceState.Active))session.Tick(state,3,0);
 Check(actions==0);
 session.Tick(QuestConvenienceState.Active,3,0);Check(actions==1&&pending);
 session.Tick(QuestConvenienceState.WaitingForHud,3,1);Check(!pending&&actions==1);
 session.Tick(QuestConvenienceState.Active,3,2);var previous=resets;
 session.Tick(QuestConvenienceState.Active,7,3);Check(resets==previous+1&&actions==3);
 session.Stop();Check(!pending&&session.State==QuestConvenienceState.Off);
 int failures=0;var fault=new QuestExecutionSession(_=>{failures++;throw new Exception("PRIVATE");},()=>{});
 fault.Tick(QuestConvenienceState.Active,1,100);fault.Tick(QuestConvenienceState.Active,1,1099);
 Check(failures==1&&fault.State==QuestConvenienceState.Faulted);
 fault.Tick(QuestConvenienceState.Active,1,1100);Check(failures==2);
});
Test("Quest wire preferences round-trip the actual HUD v1 contract", () => {
 foreach(var dialogue in new[]{false,true})foreach(var skip in new[]{false,true})for(int mode=0;mode<4;mode++){
  var packed=SentinelHUD.Core.QuestConvenienceHandoff.Encode(dialogue,skip,mode);
  Check(QuestPreferenceCodec.TryDecode(packed,out var opts));
  Check(opts.SkipDialogue==dialogue && opts.SkipCutscenes==skip && (int)opts.QuestRewardSelection==mode && QuestPreferenceCodec.Encode(opts)==packed);
 }
 Check(!QuestPreferenceCodec.TryDecode(-1,out _)&&!QuestPreferenceCodec.TryDecode(16,out _));
});
Test("Quest and Anti-AFK leases are independent in both initial callback orders", () => {
 foreach(var hudFirst in new[]{false,true}){
  var lease=new SentinelHUD.Core.AntiAfkLease();var anti=new SentinelHUD.Core.AntiAfkLease();var token=Guid.NewGuid().ToString("N");
  bool HudMayAct(long now,bool requested)=>!SentinelHUD.Core.QuestConvenienceHandoff.ShouldYield(lease.IsClaimed(now),true,"0.3.0.0",requested);
  if(hudFirst)Check(!HudMayAct(0,true));
  Check(lease.TryClaim(token,0,true));Check(!HudMayAct(0,true)&&!anti.IsClaimed(0));
  Check(!lease.TryClaim(Guid.NewGuid().ToString("N"),1,true));Check(!lease.Release(Guid.NewGuid().ToString("N")));
  Check(lease.Release(token));Check(HudMayAct(1,false));
  Check(!SentinelHUD.Core.QuestConvenienceHandoff.ShouldYield(false,true,"0.2.0.0",null));
 }
});
Test("Quest cutscene prompt identity, cancellation and deadline preserve the submarine fix", () => {
 foreach(var name in new[]{"SelectString","CutSceneSelectString"})Check(CutsceneSkipPromptPolicy.TryMatch(name,42,42,out _));
 foreach(var name in new[]{"SelectYesno","Talk","JournalResult",null})Check(!CutsceneSkipPromptPolicy.TryMatch(name,42,42,out _));
 Check(!CutsceneSkipPromptPolicy.TryMatch("SelectString",41,42,out _)&&!CutsceneSkipPromptPolicy.TryMatch("SelectString",0,0,out _));
 var state=CutsceneSkipConfirmationState.Begin(0,5000);
 Check(CutsceneSkipConfirmationPolicy.Evaluate(ref state,1,false,false)==CutsceneSkipConfirmationDecision.Wait);
 Check(CutsceneSkipConfirmationPolicy.Evaluate(ref state,2,true,false)==CutsceneSkipConfirmationDecision.Wait);
 Check(CutsceneSkipConfirmationPolicy.Evaluate(ref state,3,true,true)==CutsceneSkipConfirmationDecision.Confirm);
 Check(CutsceneSkipConfirmationPolicy.Evaluate(ref state,4,false,false)==CutsceneSkipConfirmationDecision.Dismissed);
 state=CutsceneSkipConfirmationState.Begin(0,5000);Check(CutsceneSkipConfirmationPolicy.Evaluate(ref state,5000,true,true)==CutsceneSkipConfirmationDecision.TimedOut);
 state=default;Check(CutsceneSkipConfirmationPolicy.Evaluate(ref state,100,true,true)==CutsceneSkipConfirmationDecision.Wait);
});
Test("Quest reward policy preserves manual, job specificity, vendor quantity and first fallback", () => {
 var list=new[]{
  new QuestRewardCandidate(0,1,1,"FIRST",false,0,1,false,1),
  new QuestRewardCandidate(1,2,1,"JOB",true,900,10,false,1),
  new QuestRewardCandidate(2,3,1,"BROAD",true,800,1000,false,1),
  new QuestRewardCandidate(3,5824,9,"PIECE",false,0,1,true,100),
  new QuestRewardCandidate(4,5825,1,"PIECE2",false,0,1,true,500)};
 Check(QuestRewardSelectionPolicy.Select(QuestRewardSelectionMode.Manual,list)==null);
 Check(QuestRewardSelectionPolicy.Select(QuestRewardSelectionMode.CurrentJobReward,list)?.Index==1);
 Check(QuestRewardSelectionPolicy.Select(QuestRewardSelectionMode.AllaganPiece,list)?.Index==3);
 Check(QuestRewardSelectionPolicy.Select(QuestRewardSelectionMode.FirstReward,list)?.Index==0);
 Check(QuestRewardSelectionPolicy.Select(QuestRewardSelectionMode.CurrentJobReward,new[]{list[0]})?.Index==0);
 Check(QuestRewardSelectionPolicy.Select(QuestRewardSelectionMode.AllaganPiece,new[]{list[0]})?.Index==0);
 Check(QuestRewardSelectionPolicy.Select((QuestRewardSelectionMode)999,list)==null);
 Check(QuestRewardSelectionPolicy.Select(QuestRewardSelectionMode.FirstReward,Array.Empty<QuestRewardCandidate>())==null);
});
Test("Quest diagnostics export only allowlisted state, never native status strings or saved preferences", () => {
 var json=DiagnosticExport.Serialize(HubSnapshot.Unknown,[],quest:new(QuestConvenienceState.Active,15,"PRIVATE dialogue","PRIVATE cutscene","PRIVATE reward"));
 Check(json.Contains("\"QuestConvenience\"")&&json.Contains("\"Active\"")&&!json.Contains("PRIVATE")&&!json.Contains("HudPreferences"));
});

Test("HUD without native utility engines needs no ownership acknowledgement", () => {
 Check(HudUtilityOwnership.RequiresLease(null)&&HudUtilityOwnership.RequiresLease("bad")&&HudUtilityOwnership.RequiresLease("0.8.4.9"));
 foreach(var version in new[]{"0.8.5.0","0.8.5.1","0.8.5.2"}){
  Check(!HudUtilityOwnership.RequiresLease(version));
  Check(AntiAfkOwnershipPolicy.Resolve(true,true,true,true,version,false)==AntiAfkState.Active);
  Check(AntiAfkOwnershipPolicy.Resolve(true,true,true,true,version,true)==AntiAfkState.Active);
  Check(AntiAfkOwnershipPolicy.Resolve(false,true,true,true,version,true)==AntiAfkState.Off);
  Check(QuestConvenienceOwnershipPolicy.Resolve(true,true,true,true,version,false)==QuestConvenienceState.Active);
  Check(QuestConvenienceOwnershipPolicy.Resolve(true,true,true,true,version,true)==QuestConvenienceState.Active);
  Check(QuestConvenienceOwnershipPolicy.Resolve(false,true,true,true,version,true)==QuestConvenienceState.Off);
 }
});
Test("HUD ownership selection does not let disabled modern inventory hide a loaded or unsafe older engine", () => {
 var modern=new InstalledPlugin("SentinelHUD",false,"0.8.5.2");
 foreach(var older in new[]{new InstalledPlugin("SentinelHUD",true,"0.8.4.7",IsDev:true),new InstalledPlugin("SentinelHUD",false,"0.8.4.5"),new InstalledPlugin("SentinelHUD",true,null)})
 foreach(var order in new[]{new[]{modern,older},new[]{older,modern}})
  Check(HudUtilityOwnership.Select(order,new Version(0,8,4,7))==older);
 Check(HudUtilityOwnership.Select([],new Version(0,8,4,7)) is null);
 var active=modern with{Loaded=true};
 Check(HudUtilityOwnership.Select([modern,active],new Version(0,8,4,7))==active);
});
Test("live inventory wins over development in either discovery order", () => {
 foreach(var liveLoaded in new[]{false,true})
 foreach(var devLoaded in new[]{false,true})
 foreach(var devFirst in new[]{false,true}){
  var live=new InstalledPlugin("SRankSentinel",liveLoaded,"0.7.56.0");
  var dev=new InstalledPlugin("sranksentinel",devLoaded,"0.7.61.0",IsDev:true);
  var all=devFirst?new[]{dev,live}:new[]{live,dev};
  var result=StatusPolicy.Build(all,true,_=>Response.Unknown).Plugins.Single(p=>p.Component.Id=="SRankSentinel");
  Check(result.Version=="0.7.56.0"&&!result.IsDev&&result.State==(liveLoaded?Presence.Loaded:Presence.Disabled));
  Check(StatusPolicy.CanOpen(Catalog.Find("SRankSentinel"),all,true)==liveLoaded);
 }
});
Test("development-only inventory stays explicit and live versions are not hardcoded", () => {
 var dev=new InstalledPlugin("SRankSentinel",true,"0.7.61.0",IsDev:true);
 var row=StatusPolicy.Build([dev],true,_=>Response.Unknown).Plugins.Single(p=>p.Component.Id=="SRankSentinel");
 Check(row.IsDev&&row.Version=="0.7.61.0"&&row.State==Presence.Loaded);
 Check(StatusPolicy.CanOpen(Catalog.Find("SRankSentinel"),[dev],true));
 foreach(var component in Catalog.Components){
  var live=new InstalledPlugin(component.Id,true,"9.10.11.12");
  var candidate=new InstalledPlugin(component.Id,true,"99.99.99.99",IsDev:true);
  var selected=StatusPolicy.Build([candidate,live],true,_=>Response.Unknown).Plugins.Single(p=>p.Component.Id==component.Id);
  Check(!selected.IsDev&&selected.Version=="9.10.11.12");
 }
});
Test("quick-open checks the same live instance and refuses a loaded dev substitute", () => {
 var dev=new InstalledPlugin("SRankSentinel",true,"0.7.61.0",IsDev:true);
 var live=new InstalledPlugin("SRankSentinel",false,"0.7.56.0"); var calls=0;
 var router=new QuickOpen(()=>new[]{dev,live},_=>{calls++;return true;},new FaultJournal());
 Check(!router.Open("SRankSentinel")&&calls==0);
 live=live with{Loaded=true};Check(router.Open("SRankSentinel")&&calls==1);
 live=live with{Restricted=true};Check(!router.Open("SRankSentinel")&&calls==1);
 live=live with{Restricted=false,Loaded=false};Check(!router.Open("SRankSentinel")&&calls==1);
});

Test("Latency upgrades default Off and normalization preserves accepted utilities and layout", () => {
 var fresh=new HubSettings(); Check(fresh.LatencyMode==LatencyMode.Off);
 var old=JsonSerializer.Deserialize<HubSettings>("{\"Theme\":0,\"AntiAfkEnabled\":true,\"QuestDialogue\":true,\"ExpandedWidth\":900}")!;
 old.Normalize();Check(old.LatencyMode==LatencyMode.Off&&old.AntiAfkEnabled&&old.QuestDialogue&&old.Theme==0&&old.ExpandedWidth==900);
 old.LatencyMode=(LatencyMode)99;old.Normalize();Check(old.LatencyMode==LatencyMode.Off);
 old.LatencyMode=LatencyMode.Measure;old.ImportQuestPreferences(15);Check(old.LatencyMode==LatencyMode.Measure);
 var round=JsonSerializer.Deserialize<HubSettings>(JsonSerializer.Serialize(old))!;round.Normalize();Check(round.LatencyMode==LatencyMode.Measure);
});
Test("Latency action eligibility excludes cast-time and special categories", () => {
 for(uint category=0;category<30;category++)for(int cast=0;cast<10;cast++)
  Check(LatencyActionPolicy.IsEligible(category,cast)==(cast==0&&(category==2||category==3||category==4)));
 Check(!LatencyActionPolicy.IsEligible(4,-1));
});
Test("Latency compensation lattice preserves intrinsic lock, buffer, cap and measure-only behavior", () => {
 foreach(var mode in Enum.GetValues<LatencyMode>())
 foreach(var wire in new[]{0.5f,0.6f,0.7f,0.75f,0.8f})
 for(int delay=1;delay<500;delay++){
  var session=new LatencySession(mode);var tracked=session.Track(1,100,0.5f,1000);
  Check(tracked==(mode!=LatencyMode.Off));
  var d=session.Receive(1,100,wire,0.5f-delay/1000f,wire,1000+delay);
  Check(d.Write==(mode==LatencyMode.Compensate&&delay>40));
  if(mode==LatencyMode.Off){Check(session.View.Samples==0);continue;}
  Check(session.View.Samples==1&&session.View.Adjustments==0);
  Check(float.IsFinite(d.RemainingLock)&&d.RemainingLock>=0.04f&&d.RemainingLock<=wire);
  Check(wire-d.RemainingLock<=0.25001f);
  Check(d.RemainingLock+delay/1000f+0.00001f>=wire+Math.Min(0.04f,delay/1000f));
  session.RecordApplied();session.RecordApplied();
  Check(session.View.Adjustments==(d.Write?1:0));
  Check(session.View.TotalReductionMs>=0&&session.View.TotalReductionMs<=250.01);
 }
});
Test("Latency correlates only latest sequence/action, rejects replay and handles sequence wrap", () => {
 var s=new LatencySession(LatencyMode.Compensate);Check(s.Track(ushort.MaxValue,100,.5f,0));
 Check(!s.Receive(1,100,.6f,.4f,.6f,100).Write&&s.Matches(ushort.MaxValue,100));
 Check(!s.Receive(ushort.MaxValue,101,.6f,.4f,.6f,100).Write&&s.Matches(ushort.MaxValue,100));
 Check(s.Track(1,200,.5f,20));
 Check(!s.Receive(ushort.MaxValue,100,.6f,.4f,.6f,100).Write&&s.Matches(1,200));
 Check(s.Receive(1,200,.6f,.4f,.6f,120).Write);s.RecordApplied();
 Check(!s.Receive(1,200,.6f,.4f,.6f,121).Write&&s.View.Adjustments==1);
 Check(!s.Track(0,200,.5f,0)&&!s.Track(2,0,.5f,0));
});
Test("Latency expires pending work and rejects invalid, long and inconsistent samples", () => {
 foreach(var delay in new[]{-1,0,501,5000}){
  var s=new LatencySession(LatencyMode.Compensate);s.Track(1,100,.5f,1000);
  Check(!s.Receive(1,100,.6f,.4f,.6f,1000+delay).Write&&!s.Matches(1,100));
 }
 foreach(var wire in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity,-1f,0f,.1f,.49f,.81f,2f,10f}){
  var s=new LatencySession(LatencyMode.Compensate);s.Track(1,100,.5f,0);
  Check(!s.Receive(1,100,wire,.4f,wire,100).Write&&s.View.Adjustments==0);
 }
 foreach(var before in new[]{float.NaN,float.PositiveInfinity,-1f,0f,.6f,.1f}){
  var s=new LatencySession(LatencyMode.Compensate);s.Track(1,100,.5f,0);
  Check(!s.Receive(1,100,.6f,before,.6f,100).Write);
 }
 var wrongPrediction=new LatencySession(LatencyMode.Compensate);
 Check(!wrongPrediction.Track(1,100,.54f,0));
});
Test("Latency unexpected header/client locks latch contention without writing", () => {
 foreach(var pair in new[]{(.6f,.55f),(.605f,.605f)}){
  var s=new LatencySession(LatencyMode.Compensate);s.Track(1,100,.5f,0);
  Check(!s.Receive(1,100,pair.Item1,.4f,pair.Item2,100).Write&&s.View.State==LatencyState.Contested);
  Check(!s.Track(2,100,.5f,101)&&s.View.Adjustments==0);
 }
 var stopped=new LatencySession(LatencyMode.Compensate);stopped.Track(1,100,.5f,0);
 stopped.Receive(1,100,.6f,.4f,.6f,100);stopped.Stop(LatencyState.Off);stopped.RecordApplied();
 Check(stopped.View.Adjustments==0);
});
Test("Latency lifecycle creates no hooks Off or while ineligible and tears down on every boundary", () => {
 var all=new List<FakeLatencyRuntime>();using var life=new LatencyLifecycle(mode=>{var r=new FakeLatencyRuntime(mode);all.Add(r);return r;});
 life.Tick(LatencyMode.Off,LatencyState.Compensating);
 foreach(var state in Enum.GetValues<LatencyState>().Where(s=>s!=LatencyState.Compensating))
  life.Tick(LatencyMode.Measure,state);
 Check(all.Count==0);
 life.Tick(LatencyMode.Measure,LatencyState.Compensating);Check(all.Count==1&&life.View.State==LatencyState.Measuring);
 life.Tick(LatencyMode.Compensate,LatencyState.Compensating);Check(all.Count==2&&all[0].Disposed);
 foreach(var state in new[]{LatencyState.Paused,LatencyState.WaitingForLogin,LatencyState.NoClippyConflict,LatencyState.XivAlexanderConflict,LatencyState.ConflictCheckUnavailable}){
  life.Tick(LatencyMode.Compensate,state);Check(all.Last().Disposed&&life.View.State==state);
  life.Tick(LatencyMode.Compensate,LatencyState.Compensating);Check(!all.Last().Disposed);
 }
 life.Tick(LatencyMode.Off,LatencyState.Compensating);Check(all.Last().Disposed&&life.View.State==LatencyState.Off);
 var count=all.Count;life.Tick((LatencyMode)999,LatencyState.Compensating);Check(all.Count==count);
 life.Dispose();life.Tick(LatencyMode.Compensate,LatencyState.Compensating);Check(all.Count==count);
});
Test("Latency contention/factory faults require Off before a bounded retry", () => {
 var all=new List<FakeLatencyRuntime>();using var life=new LatencyLifecycle(mode=>{var r=new FakeLatencyRuntime(mode);all.Add(r);return r;});
 life.Tick(LatencyMode.Compensate,LatencyState.Compensating);all[0].View=new(LatencyState.Contested);
 life.Tick(LatencyMode.Compensate,LatencyState.Compensating);Check(all[0].Disposed&&life.View.State==LatencyState.Contested);
 life.Tick(LatencyMode.Measure,LatencyState.Compensating);Check(all.Count==1);
 life.Tick(LatencyMode.Off,LatencyState.Compensating);life.Tick(LatencyMode.Measure,LatencyState.Compensating);Check(all.Count==2);
 int attempts=0;using var failing=new LatencyLifecycle(_=>{attempts++;throw new Exception("PRIVATE");});
 for(int i=0;i<20;i++)failing.Tick(LatencyMode.Compensate,LatencyState.Compensating);
 Check(attempts==1&&failing.View.State==LatencyState.UnsupportedClient);
 failing.Tick(LatencyMode.Off,LatencyState.Compensating);failing.Tick(LatencyMode.Compensate,LatencyState.Compensating);Check(attempts==2);
});
Test("Latency native transaction forwards Original exactly once even when observation fails", () => {
 foreach(var result in new[]{false,true})
 foreach(var before in new[]{false,true})
 foreach(var after in new[]{false,true})
 foreach(var fault in new[]{false,true}){
  var trace=new LatencyCallTrace{Return=result,ThrowBefore=before,ThrowAfter=after,ThrowFault=fault};
  var call=new FakeLatencyCall{Trace=trace};
  Check(LatencyInterception.Invoke(ref call)==result&&trace.Originals==1);
  Check(trace.Before==1&&trace.After==(before?0:1));
  Check(trace.Faults==(before||after?1:0));
 }
 var original=new LatencyCallTrace{ThrowOriginal=true};var originalCall=new FakeLatencyCall{Trace=original};
 try{LatencyInterception.Invoke(ref originalCall);throw new Exception("Expected original exception");}
 catch(InvalidOperationException){Check(original.Originals==1&&original.After==0);}
});
Test("Latency detects loaded dev/live NoClippy and named or renamed Alexander modules", () => {
 Check(LatencyConflictPolicy.IsNoClippy("NoClippy",true)&&LatencyConflictPolicy.IsNoClippy("noclippyPlus",true));
 Check(!LatencyConflictPolicy.IsNoClippy("NoClippy",false)&&!LatencyConflictPolicy.IsNoClippy("OtherPlugin",true));
 foreach(var name in new[]{"XivAlexander64.dll","XivAlexander32.dll","xivalexanderloader64.dll","XivAlexanderLoader32.dll"})
  Check(LatencyConflictPolicy.IsAlexander(name,false,false,false));
 Check(LatencyConflictPolicy.IsAlexander("renamed.dll",true,false,false));
 Check(LatencyConflictPolicy.IsAlexander("d3d11.dll",false,true,true));
 Check(!LatencyConflictPolicy.IsAlexander("d3d11.dll",false,false,false)&&!LatencyConflictPolicy.IsAlexander("other.dll",false,true,false));
});
Test("Latency export allows bounded numbers/state without native or action identifiers", () => {
 var json=DiagnosticExport.Serialize(HubSnapshot.Unknown,[],latency:new(LatencyState.Compensating,3,2,120,100,80,160));
 using var document=JsonDocument.Parse(json);
 Check(document.RootElement.GetProperty("Latency").GetProperty("State").GetString()=="Compensating");
 Check(!json.Contains("SourceSequence")&&!json.Contains("ActionId")&&!json.Contains("LatencyMode")&&!json.Contains("ModuleName"));
 Check(Version.TryParse(document.RootElement.GetProperty("HubVersion").GetString(),out _));
 var invalid=DiagnosticExport.Serialize(HubSnapshot.Unknown,[],latency:new((LatencyState)999,-1,-2,double.NaN,double.PositiveInfinity,-5,double.NegativeInfinity));
 using var invalidDocument=JsonDocument.Parse(invalid);
 var data=invalidDocument.RootElement.GetProperty("Latency");
 Check(data.GetProperty("State").GetString()=="Unknown"&&data.GetProperty("Samples").GetInt64()==0);
 Check(data.GetProperty("LastResponseMs").GetDouble()==0&&!invalid.Contains("NaN")&&!invalid.Contains("Infinity"));
});


MountEntry M(uint id, string name, int? expansion = null, MountKnowledge market = MountKnowledge.Unknown,
    MountKnowledge obtainable = MountKnowledge.Unknown, MountSource[]? sources = null, int seats = 1) =>
    new(id, name, name, 123, seats, [], 0, 0, expansion, market, obtainable, sources ?? [], []);
MountCatalog C(params MountEntry[] entries) => new(entries, [], [], new Dictionary<int,string>{{0,"ARR"},{1,"HW"}}, 0);
MountCharacterSnapshot O(params (uint Id, bool Owned)[] entries) => new(true, false, entries.ToDictionary(x=>x.Id,x=>x.Owned), new Dictionary<uint,long>());
Test("Mount old settings retain enum values, native opt-ins and default-off context lookup", () => {
 Check((int)HubPage.About==7 && (int)HubPage.Mounts==8);
 var old=JsonSerializer.Deserialize<HubSettings>("{\"Version\":1,\"Page\":7,\"AntiAfkEnabled\":true,\"LatencyMode\":2}")!;old.Normalize();
 Check(old.Page==HubPage.About && old.AntiAfkEnabled && old.LatencyMode==LatencyMode.Compensate && !old.MountContextMenuEnabled);
 var newSettings=new HubSettings{Page=HubPage.Mounts,MountContextMenuEnabled=true};
 var copy=JsonSerializer.Deserialize<HubSettings>(JsonSerializer.Serialize(newSettings))!;copy.Normalize();
 Check(copy.Page==HubPage.Mounts&&copy.MountContextMenuEnabled&&!copy.AntiAfkEnabled&&!copy.QuestConvenienceEnabled);
});
Test("Mount journal candidates reject blank, model-less, icon-less and negative-order rows", () => {
 Check(MountPolicy.IsCollectionRow(1,"Chocobo",1,1,0));
 Check(!MountPolicy.IsCollectionRow(0,"Chocobo",1,1,0)&&!MountPolicy.IsCollectionRow(1," ",1,1,0));
 Check(!MountPolicy.IsCollectionRow(1,"Chocobo",0,1,0)&&!MountPolicy.IsCollectionRow(1,"Chocobo",1,0,0));
 Check(!MountPolicy.IsCollectionRow(1,"Chocobo",1,1,-1));
});
Test("Mount item correlation only accepts verified unlock action and known mount ID", () => {
 var ids=new HashSet<uint>{4,9};
 Check(MountPolicy.UnlockMount(1322,4,ids)==4);
 Check(MountPolicy.UnlockMount(853,4,ids)==null&&MountPolicy.UnlockMount(1322,5,ids)==null&&MountPolicy.UnlockMount(1322,0,ids)==null);
});
Test("Mount marketability preserves unknown and accounts for every known item", () => {
 Check(MountPolicy.MarketStatus([])==MountKnowledge.Unknown);
 Check(MountPolicy.MarketStatus([new(1,"a",false)])==MountKnowledge.No);
 Check(MountPolicy.MarketStatus([new(1,"a",false),new(2,"b",true)])==MountKnowledge.Yes);
});
Test("Mount filters never treat unknown ownership as missing", () => {
 var cat=C(M(1,"a"),M(2,"b"),M(3,"c"));var own=O((1,true),(2,false));var f=new MountFilter();
 f.Ownership=1;Check(MountPolicy.Filter(cat,own,f).Select(x=>x.Id).SequenceEqual(new uint[]{2}));
 f.Ownership=2;Check(MountPolicy.Filter(cat,own,f).Single().Id==1);
 f.Ownership=3;Check(MountPolicy.Filter(cat,own,f).Single().Id==3);
 f.Ownership=1;Check(MountPolicy.Filter(cat,MountCharacterSnapshot.Unknown,f).Length==0);
});
Test("Mount knowledge filters exhaustively distinguish yes, no and unknown", () => {
 foreach(var value in Enum.GetValues<MountKnowledge>())foreach(var filter in new[]{0,1,2,3}){
  Check(MountPolicy.KnowledgeMatches(value,filter)==(filter==0||filter==1&&value==MountKnowledge.Yes||filter==2&&value==MountKnowledge.No||filter==3&&value==MountKnowledge.Unknown));
 }
 Check(!MountPolicy.KnowledgeMatches(MountKnowledge.Unknown,999));
 var cat=C(M(1,"a",market:MountKnowledge.Yes),M(2,"b",market:MountKnowledge.No),M(3,"c"));
 var f=new MountFilter{Marketable=1};Check(MountPolicy.Filter(cat,O(),f).Single().Id==1);
 f.Marketable=3;Check(MountPolicy.Filter(cat,O(),f).Single().Id==3);
 f.Marketable=0;f.Obtainable=1;Check(MountPolicy.Filter(cat,O(),f).Length==0);
 f.Obtainable=3;Check(MountPolicy.Filter(cat,O(),f).Length==3);
});
Test("Mount search supports localization, English keys, numeric ID, items and known source", () => {
 var row=M(17,"Localized") with{EnglishName="Firebird",Items=[new(99,"Whistle",false)],Sources=[new(MountCategory.Quest,"Quest: Fiery Wings","Game data")]};
 foreach(var term in new[]{"local"," FIREBIRD ","17","whistle","fiery"}){
  Check(MountPolicy.Filter(C(row),O(),new MountFilter{Search=term}).Length==1);
 }
 Check(MountPolicy.Filter(C(row),O(),new MountFilter{Search="absent"}).Length==0);
});
Test("Mount expansion and source filters retain unknown without inventing an expansion", () => {
 var cat=C(M(1,"a",0,sources:[new(MountCategory.Quest,"quest","data")]),M(2,"b",1),M(3,"c"));
 Check(MountPolicy.Filter(cat,O(),new MountFilter{Expansion=0}).Single().Id==1);
 Check(MountPolicy.Filter(cat,O(),new MountFilter{Expansion=-1}).Single().Id==3);
 Check(MountPolicy.Filter(cat,O(),new MountFilter{Category=MountCategory.Quest}).Single().Id==1);
 Check(MountPolicy.Filter(cat,O(),new MountFilter{Category=MountCategory.Unknown}).Length==2);
});
Test("Mount sorting is stable by ID across ties, localization and seats", () => {
 var cat=C(M(2,"Alpha",seats:2),M(1,"alpha",seats:1),M(3,"Beta",seats:8));
 Check(MountPolicy.Filter(cat,O(),new MountFilter()).Select(x=>x.Id).SequenceEqual(new uint[]{1,2,3}));
 Check(MountPolicy.Filter(cat,O(),new MountFilter{Sort=MountSort.NameDescending}).First().Id==3);
 Check(MountPolicy.Filter(cat,O(),new MountFilter{Sort=MountSort.SeatsDescending}).First().Id==3);
 Check(MountPolicy.Filter(cat,O(),new MountFilter{Sort=MountSort.Id}).First().Id==1);
});
Test("Mount completion ignores duplicate IDs and never converts an unread character into zero-owned", () => {
 var done=MountPolicy.Completion(new uint[]{1,1,2,3},O((1,true),(2,false)));
 Check(done==new MountCompletion(1,1,1)&&done.Total==3&&done.Label.Contains("unknown"));
 var none=MountPolicy.Completion(new uint[]{1,2},MountCharacterSnapshot.Unknown);
 Check(none==new MountCompletion(0,0,2));
});
Test("Mount track exact English resolution preserves missing and ambiguous requirements", () => {
 var seed=new MountTrackSeed("x",1,"Reward","Quest","Official",["a","b","missing"]);
 var track=MountPolicy.ResolveTrack(seed,new[]{M(1,"a"),M(2,"b"),M(3,"Reward")});
 Check(track.RewardId==3&&track.Members[2].MountId==null);
 Check(MountPolicy.TrackCompletion(track,O((1,true),(2,false)))==new MountCompletion(1,1,1));
 var ambiguous=MountPolicy.ResolveTrack(seed,new[]{M(1,"a"),M(2,"a")});
 Check(ambiguous.Members[0].MountId==null&&ambiguous.RewardId==null);
});
Test("Sentinel metadata has six independent complete expansion sets with provenance", () => {
 Check(SentinelMountMetadata.Tracks.Length==6);
 Check(SentinelMountMetadata.Tracks.Select(t=>t.Expansion).SequenceEqual(Enumerable.Range(0,6)));
 Check(SentinelMountMetadata.Tracks.Select(t=>t.Members.Length).SequenceEqual(new[]{6,7,7,7,7,7}));
 foreach(var t in SentinelMountMetadata.Tracks){
  Check(t.Members.Distinct(StringComparer.OrdinalIgnoreCase).Count()==t.Members.Length);
  Check(!t.Members.Contains(t.Reward,StringComparer.OrdinalIgnoreCase)&&t.Evidence.StartsWith("https://"));
  Check(SentinelMountMetadata.Find(t.Reward)==t&&SentinelMountMetadata.Find(t.Members[0].ToUpperInvariant())==t);
 }
 Check(SentinelMountMetadata.Find("unverified new mount")==null);
});
Test("Mount trial-token exchanges reject complex, HQ, currency and nontrial offers", () => {
 bool Offer(bool trial=true,byte mode=0,uint receive=1,bool hq=false,int costs=1,uint type=0,uint item=100,long required=99,bool untradable=true,string name="King Totem",long collectability=0) =>
  MountPolicy.SafeTokenOffer(trial,mode,receive,hq,costs,type,item,required,untradable,name,collectability);
 Check(Offer()&&Offer(mode:4)&&Offer(mode:8)&&Offer(mode:16));
 foreach(var alias in Enumerable.Range(0,8))Check(!Offer(item:(uint)alias));
 Check(!Offer(collectability:1));
 Check(!Offer(trial:false)&&!Offer(mode:1)&&!Offer(receive:2)&&!Offer(hq:true)&&!Offer(costs:2)&&!Offer(type:3));
 Check(!Offer(item:0)&&!Offer(required:0)&&!Offer(required:10000)&&!Offer(untradable:false)&&!Offer(name:"Irregular Tomestone"));
 Check(MountPolicy.Progress(200,99)==1&&MountPolicy.Progress(0,99)==0&&MountPolicy.Progress(-1,99)==0&&MountPolicy.Progress(1,0)==0);
});
MountBagSnapshot[] Bags() => Enumerable.Range(0,4).Select(n=>new MountBagSnapshot(n,true,35,new MountBagItem[35])).ToArray();
Test("Mount token counts sum split stacks in main bags and ignore unrelated items", () => {
 var bags=Bags();bags[0].Items[0]=new(1,50);bags[3].Items[34]=new(1,49);bags[2].Items[1]=new(2,999);
 var counts=MountInventoryPolicy.Count(bags,new HashSet<uint>{1,3})!;
 Check(counts[1]==99&&counts[3]==0&&!counts.ContainsKey(2));
});
Test("Mount token inventory uncertainty fails closed for unloaded, missing, duplicated or invalid bags", () => {
 var wanted=new HashSet<uint>{1};
 Check(MountInventoryPolicy.Count(Bags()[..3],wanted)==null);
 var bags=Bags();bags[1]=bags[1] with{Loaded=false};Check(MountInventoryPolicy.Count(bags,wanted)==null);
 bags=Bags();bags[1]=bags[1] with{Kind=0};Check(MountInventoryPolicy.Count(bags,wanted)==null);
 bags=Bags();bags[0]=bags[0] with{DeclaredSize=36};Check(MountInventoryPolicy.Count(bags,wanted)==null);
 bags=Bags();bags[0]=bags[0] with{Items=[]};Check(MountInventoryPolicy.Count(bags,wanted)==null);
 foreach(var item in new[]{new MountBagItem(1,-1),new MountBagItem(1,1000000),new MountBagItem(1,1,true)}){
  bags=Bags();bags[0].Items[0]=item;Check(MountInventoryPolicy.Count(bags,wanted)==null);
 }
 bags=Bags();bags[3]=bags[3] with{Kind=10000};Check(MountInventoryPolicy.Count(bags,wanted)==null);
});
Test("Mount own-character observations clear on logout, character switch, unready state and failure", () => {
 var session=new MountObservationSession();
 session.Observe(1,true,()=>O((1,true)));Check(session.Snapshot.Ownership(1)==MountOwnership.Unlocked);
 session.Observe(2,true,()=>O((1,false)));Check(session.Snapshot.Ownership(1)==MountOwnership.Locked);
 session.Observe(2,false,()=>throw new Exception());Check(!session.Snapshot.Available);
 session.Observe(1,true,()=>O((1,true)));session.Observe(1,true,()=>throw new Exception());
 Check(!session.Snapshot.Available&&session.Snapshot.Ownership(1)==MountOwnership.Unknown);
 session.Observe(0,true,()=>O((1,true)));Check(!session.Snapshot.Available);
});
Test("Mount context menu tickets reject stale identity, logout, zone changes, Off, dismount and disposal", () => {
 var ticket=new MountLookupTicket(10,20,30);
 bool Allowed(ulong obj=10,ulong session=20,uint territory=30,bool enabled=true,bool ready=true,bool player=true,uint mount=1,bool disposed=false)=>
  MountLookupPolicy.CanOpen(ticket,obj,session,territory,enabled,ready,player,mount,disposed);
 Check(Allowed()&&Allowed(mount:2));
 Check(!Allowed(obj:11)&&!Allowed(session:21)&&!Allowed(territory:31)&&!Allowed(enabled:false));
 Check(!Allowed(ready:false)&&!Allowed(player:false)&&!Allowed(mount:0)&&!Allowed(disposed:true));
 Check(!MountLookupPolicy.CanOpen(new(0,20,30),0,20,30,true,true,true,1,false));
});


Test("Mount acquisition snapshot is bounded, attributed, uniquely keyed and covers every recorded source", () => {
 var data=MountAcquisitionMetadata.Snapshot;
 Check(data.Provider=="FFXIV Collect"&&data.Mounts.Length==356&&data.ResponseSha256.Length==64);
 Check(data.Mounts.Select(m=>m.Id).Distinct().Count()==data.Mounts.Length);
 Check(DateTimeOffset.TryParse(data.RetrievedUtc,out _));
 foreach(var m in data.Mounts){
  Check(m.Id>0&&!string.IsNullOrWhiteSpace(m.EnglishName)&&m.Sources.Length>0);
  Check(MountAcquisitionMetadata.Expansion(m.Patch) is >=0 and <=5);
  foreach(var source in MountAcquisitionMetadata.Sources(m))
   Check(source.Community&&!string.IsNullOrWhiteSpace(source.Description)&&source.Evidence.Contains("2026-10-17")&&MountAcquisitionMetadata.SafeSourceUrl(source.Url));
 }
});
Test("Mount acquisition identity rejects ID/name drift instead of fuzzy matching", () => {
 Check(MountAcquisitionMetadata.Find(317,"chrysomallos")?.Id==317);
 Check(MountAcquisitionMetadata.Find(317,"Wrong mount")==null&&MountAcquisitionMetadata.Find(99999,"Chrysomallos")==null);
 Check(!MountAcquisitionMetadata.IdentityMatches(0,"a",0,"a")&&!MountAcquisitionMetadata.IdentityMatches(317,"Chrysomallos",316,"Chrysomallos"));
 Check(MountAcquisitionMetadata.OfficialVendor(317,"Other")==null&&MountAcquisitionMetadata.FindStore(42,"Other")==null);
});
Test("Mount release patches map major versions only and preserve unknown future/malformed values", () => {
 foreach(var (patch,expansion) in new[]{("2.0",0),("3.55",1),("3.55a",1),("3.55b",1),("4.0",2),("5.58",3),("6.3",4),("7.56",5)})
  Check(MountAcquisitionMetadata.Expansion(patch)==expansion);
 foreach(var patch in new[]{"","1.23","8.0","7","7.","7.a","7.5.1"," 7.5","7.5x"})
  Check(MountAcquisitionMetadata.Expansion(patch)==null);
});
Test("Online Store, collector editions and historical event sources remain distinct", () => {
 MountSource[] Sources(uint id)=>MountAcquisitionMetadata.Sources(MountAcquisitionMetadata.Snapshot.Mounts.Single(m=>m.Id==id));
 Check(Sources(42).Single().Category==MountCategory.OnlineStore);
 Check(Sources(8).Single().Category==MountCategory.Premium&&!Sources(8).Any(s=>s.Category==MountCategory.OnlineStore));
 Check(Sources(99).Any(s=>s.Category==MountCategory.Event&&s.Description.Contains("2017"))&&Sources(99).Any(s=>s.Category==MountCategory.OnlineStore));
 Check(MountAcquisitionMetadata.Snapshot.Mounts.Count(m=>m.Sources.Any(s=>MountAcquisitionMetadata.Category(s)==MountCategory.OnlineStore))==46);
 Check(MountAcquisitionMetadata.Category(new("New category","Unverified"))==MountCategory.Other);
});
Test("Chrysomallos regression records Edelina, Mor Dhona, coordinates and fifty million gil", () => {
 var row=MountAcquisitionMetadata.Find(317,"Chrysomallos")!;
 Check(row.Sources.Any(s=>s.Text.Contains("Edelina")&&s.Text.Contains("50,000,000")));
 var official=MountAcquisitionMetadata.OfficialVendor(317,"Chrysomallos")!;
 Check(official.Category==MountCategory.GilVendor&&official.Description.Contains("Mor Dhona")&&official.Description.Contains("22.1")&&official.Description.Contains("4.8")&&official.Description.Contains("50,000,000"));
 var gil=MountAcquisitionMetadata.GilSource(1,0,"Luxury","Horn",50000000,false,["Quest test"]);
 Check(gil.Description.Contains("50,000,000 gil")&&gil.Description.Contains("Quest test")&&gil.Evidence.Contains("GilShopItem"));
 Check(MountAcquisitionMetadata.GilSource(1,0,"","Horn",0,false,[]).Description.Contains("price unknown"));
 Check(MountAcquisitionMetadata.GilSource(1,0,"","Horn",100,true,[]).Description.Contains("price not resolved"));
});
Test("Mount shop cost labels reject unknown currency modes, compact aliases and conditional costs", () => {
 foreach(byte mode in new byte[]{0,4,8,16})Check(MountAcquisitionMetadata.DirectCost(mode,100,0,0));
 foreach(uint id in Enumerable.Range(0,8).Select(n=>(uint)n))Check(!MountAcquisitionMetadata.DirectCost(0,id,0,0));
 Check(!MountAcquisitionMetadata.DirectCost(1,100,0,0)&&!MountAcquisitionMetadata.DirectCost(0,100,1,0)&&!MountAcquisitionMetadata.DirectCost(0,100,0,1));
});
Test("Official store links have unique reviewed mount identities and safe primary-source URLs", () => {
 var store=MountAcquisitionMetadata.Store;
 Check(store.Mounts.Length==45&&store.Mounts.Select(m=>m.Id).Distinct().Count()==45&&store.Mounts.Select(m=>m.Url).Distinct().Count()==45);
 foreach(var m in store.Mounts){
  var fact=MountAcquisitionMetadata.Find(m.Id,m.EnglishName);
  Check(fact is not null&&fact.Sources.Any(s=>MountAcquisitionMetadata.Category(s)==MountCategory.OnlineStore));
  Check(MountAcquisitionMetadata.SafeSourceUrl(m.Url)&&m.Url.Contains("/product/"));
 }
 Check(MountAcquisitionMetadata.FindStore(320,"Rising Phoenix")!.ProductName.Contains("Reborn Phoenix"));
 Check(MountAcquisitionMetadata.FindStore(397,"Shagcoat Cat")!.ProductName.Contains("Shagcat"));
 Check(!MountAcquisitionMetadata.SafeSourceUrl("http://ffxivcollect.com/mounts/317"));
 Check(!MountAcquisitionMetadata.SafeSourceUrl("https://ffxivcollect.com.evil.test/mounts/317"));
 Check(!MountAcquisitionMetadata.SafeSourceUrl("https://name:pass@ffxivcollect.com/mounts/317"));
 Check(!MountAcquisitionMetadata.SafeSourceUrl("javascript:alert(1)")&&!MountAcquisitionMetadata.SafeSourceUrl(null));
});
Test("Mount enriched sources are searchable and independently filterable without asserting availability", () => {
 MountEntry Enriched(uint id){var fact=MountAcquisitionMetadata.Snapshot.Mounts.Single(m=>m.Id==id);return M(id,fact.EnglishName,sources:MountAcquisitionMetadata.Sources(fact));}
 var cat=C(Enriched(317),Enriched(42),Enriched(8),Enriched(99));
 Check(MountPolicy.Filter(cat,O(),new MountFilter{Search="Edelina"}).Single().Id==317);
 Check(MountPolicy.Filter(cat,O(),new MountFilter{Category=MountCategory.OnlineStore}).Length==2);
 Check(MountPolicy.Filter(cat,O(),new MountFilter{Category=MountCategory.GilVendor}).Single().Id==317);
 Check(MountPolicy.Filter(cat,O(),new MountFilter{Category=MountCategory.Premium}).Single().Id==8);
 Check(MountPolicy.Filter(cat,O(),new MountFilter{Obtainable=1}).Length==0);
});


FashionEntry F(uint id,string name,int? expansion=null,FashionSource[]? sources=null,
    FashionKnowledge market=FashionKnowledge.Unknown,FashionKnowledge obtainable=FashionKnowledge.Unknown,FashionKnowledge cash=FashionKnowledge.Unknown) =>
    new(id,name,name,8001,3000,(int)id,null,expansion,sources??[],[],FashionKnowledge.Unknown,market,cash,obtainable,"Not verified");
FashionCatalog FC(params FashionEntry[] entries)=>new(entries,new Dictionary<int,string>{{3,"Shadowbringers"},{4,"Endwalker"},{5,"Dawntrail"}},0,0,null);
FashionCharacter FO(params (uint Id,bool Owned)[] values)=>new(true,values.ToDictionary(v=>v.Id,v=>v.Owned));
Test("Fashion preserves prior pages and independent default-off identification settings",()=>{
 Check((int)HubPage.Mounts==8&&(int)HubPage.Fashion==9);
 var old=JsonSerializer.Deserialize<HubSettings>("{\"Page\":8,\"MountContextMenuEnabled\":true}")!;old.Normalize();
 Check(old.Page==HubPage.Mounts&&old.MountContextMenuEnabled&&!old.FashionContextMenuEnabled);
});
Test("All 52 independently audited current ornament IDs load, including unknown acquisition",()=>{
 uint[] known=[1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,23,24,27,28,30,31,33,34,35,36,37,38,39,40,41,42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,58];
 var data=FashionMetadata.Snapshot;
 Check(data.Accessories.Select(x=>x.Id).SequenceEqual(known));
 var rows=data.Accessories.Select(m=>F(m.Id,m.EnglishName,FashionMetadata.Expansion(m.Patch),FashionMetadata.Sources(m))).ToArray();
 Check(FashionPolicy.Validate(rows).Length==52&&data.Accessories.Count(x=>x.Sources.Length>0)==51);
 Check(rows.Single(x=>x.Id==42).Sources.Length==0&&rows.Single(x=>x.Id==58).Name=="Antique Lantern");
 foreach(var row in rows)Check(FashionMetadata.Find(row.Id,row.EnglishName)!=null);
});
Test("Fashion rejects blank/zero/duplicate/invalid and migrated Facewear rows",()=>{
 Check(FashionPolicy.Candidate(1,"Parasol",8001,2939,0));
 foreach(uint id in new uint[]{22,25,26,32})Check(!FashionPolicy.Candidate(id,"Old spectacles",786,3431,1));
 Check(!FashionPolicy.Candidate(0,"x",1,1,0)&&!FashionPolicy.Candidate(65536,"x",1,1,0));
 Check(!FashionPolicy.Candidate(1,"",1,1,0)&&!FashionPolicy.Candidate(1,"x",0,1,0)&&!FashionPolicy.Candidate(1,"x",1,0,0)&&!FashionPolicy.Candidate(1,"x",1,1,-1));
 bool Reject(FashionEntry[] rows){try{FashionPolicy.Validate(rows);return false;}catch(InvalidDataException){return true;}}
 Check(Reject([F(1,"a"),F(1,"b")])&&Reject([F(0,"invalid")]));
 var snapshot=FashionMetadata.Snapshot;
 bool RejectSnapshot(FashionSnapshot value){try{FashionMetadata.ValidateSnapshot(value);return false;}catch(InvalidDataException){return true;}}
 Check(RejectSnapshot(snapshot with{Accessories=[snapshot.Accessories[0],snapshot.Accessories[0]]}));
 Check(RejectSnapshot(snapshot with{Accessories=[snapshot.Accessories[0] with{Id=0}]}));
});
Test("Fashion item mapping verifies action 20086 and preserves trade/market unknowns",()=>{
 var ids=new HashSet<uint>{1,16};
 Check(FashionPolicy.Unlock(20086,16,ids)==16&&FashionPolicy.Unlock(1322,16,ids)==null&&FashionPolicy.Unlock(20086,0,ids)==null&&FashionPolicy.Unlock(20086,999,ids)==null);
 Check(FashionPolicy.ItemStatus([],true)==FashionKnowledge.Unknown);
 Check(FashionPolicy.ItemStatus([new(1,"a",false,false)],false)==FashionKnowledge.No);
 Check(FashionPolicy.ItemStatus([new(1,"a",true,false)],false)==FashionKnowledge.Yes&&FashionPolicy.ItemStatus([new(1,"a",true,false)],true)==FashionKnowledge.No);
 Check(FashionPolicy.ItemStatus([new(1,"a",false,false),new(2,"b",true,true)],true)==FashionKnowledge.Yes);
});
Test("Fashion own unlocks distinguish missing keys and clear on session/read failure",()=>{
 var owner=FO((1,true),(2,false));
 Check(owner.Ownership(1)==FashionOwnership.Unlocked&&owner.Ownership(2)==FashionOwnership.Locked&&owner.Ownership(3)==FashionOwnership.Unknown);
 var obs=new FashionObservation();obs.Observe(1,true,()=>owner);Check(obs.Snapshot.Available);
 obs.Observe(2,true,()=>FO((1,false)));Check(obs.Snapshot.Ownership(1)==FashionOwnership.Locked);
 obs.Observe(2,true,()=>throw new Exception());Check(!obs.Snapshot.Available);
 obs.Observe(1,true,()=>owner);obs.Observe(0,true,()=>owner);Check(!obs.Snapshot.Available);
 obs.Observe(1,true,()=>owner);obs.Observe(1,false,()=>owner);Check(!obs.Snapshot.Available);
});
Test("Fashion source identities, historical/store categories and unsupported aliases remain explicit",()=>{
 Check(FashionMetadata.Find(16,"red moon parasol")!=null&&FashionMetadata.Find(16,"wrong")==null&&FashionMetadata.Find(999,"Parasol")==null);
 var red=FashionMetadata.Sources(FashionMetadata.Find(16,"Red Moon Parasol")!);
 Check(red.Any(s=>s.Category=="Online Store")&&red.Any(s=>s.Category=="Seasonal / event"&&s.Description.Contains("2021")));
 Check(FashionMetadata.Sources(FashionMetadata.Find(42,"Mandragora Parasol")!).Length==0);
 Check(FashionMetadata.Correction(42,"Mandragora Parasol")!.Note!.Contains("no verified"));
 foreach(var fact in FashionMetadata.Snapshot.Accessories){
  Check(FashionMetadata.Expansion(fact.Patch) is >=3 and <=5);
  foreach(var source in FashionMetadata.Sources(fact))Check(FashionMetadata.SafeUrl(source.Url));
 }
 foreach(var patch in new[]{"8.0","","7.","7.a","7.5.1"})Check(FashionMetadata.Expansion(patch)==null);
 Check(!FashionMetadata.SafeUrl("https://ffxivcollect.com.evil.test/fashions/1")&&!FashionMetadata.SafeUrl("http://ffxivcollect.com/fashions/1")&&!FashionMetadata.SafeUrl("javascript:alert(1)"));
});
Test("Fashion current availability requires dated evidence and expires without implying active events",()=>{
 var current=FashionMetadata.Correction(3,"Vermilion Paper Parasol")!;
 var now=DateTimeOffset.Parse("2026-10-18T15:00:00Z");
 Check(FashionMetadata.Availability(current,now)==FashionKnowledge.Yes);
 Check(FashionMetadata.Availability(current,DateTimeOffset.Parse("2026-11-18T00:00:00Z"))==FashionKnowledge.Unknown);
 Check(FashionMetadata.Availability(current,DateTimeOffset.Parse("2026-10-17T00:00:00Z"))==FashionKnowledge.Unknown);
 Check(FashionMetadata.Availability(current with{SourceUrl=null},now)==FashionKnowledge.Unknown);
 Check(FashionMetadata.Availability(current with{Availability="No"},now)==FashionKnowledge.No);
 foreach(var id in new uint[]{15,16,41,42,54})Check(FashionMetadata.Availability(FashionMetadata.Corrections.Entries.Single(x=>x.Id==id),now)==FashionKnowledge.Unknown);
 var stale=F(3,"x",obtainable:FashionKnowledge.Yes) with{AvailabilityExpiresUtc=now.AddSeconds(-1)};
 Check(FashionPolicy.CurrentAvailability(stale,now)==FashionKnowledge.Unknown);
});
Test("Fashion search, sorting and ownership never turn unknown into missing",()=>{
 var cat=FC(F(2,"Beta",sources:[new("Quest","The Coming Dawn","game")]) with{EnglishName="English alias",Items=[new(99,"Knapsack item",true,true)]},F(1,"alpha"),F(3,"Alpha"));
 var own=FO((1,true),(2,false));
 foreach(var term in new[]{"beta","English alias","knapsack","coming dawn","2"})Check(FashionPolicy.Filter(cat,own,new(){Search=term}).Single().Id==2);
 Check(FashionPolicy.Filter(cat,own,new(){Ownership=1}).Single().Id==2);
 Check(FashionPolicy.Filter(cat,FashionCharacter.Unknown,new(){Ownership=1}).Length==0);
 Check(FashionPolicy.Filter(cat,own,new(){Ownership=3}).Single().Id==3);
 Check(FashionPolicy.Filter(cat,own,new()).Select(x=>x.Id).SequenceEqual(new uint[]{1,3,2}));
 Check(FashionPolicy.Filter(cat,own,new(){Sort=FashionSort.Id}).First().Id==1);
 Check(FashionPolicy.Filter(cat,own,new(){Sort=FashionSort.NameDescending}).First().Id==2);
 Check(FashionPolicy.Completion(cat.Entries,own)==(1,1,1));
});
Test("Fashion market/category/expansion/cash/availability filters respect independent tri-state fields",()=>{
 var cat=FC(F(1,"a",3,[new("Online Store","Store","community")],FashionKnowledge.No,FashionKnowledge.Yes,FashionKnowledge.Yes),
    F(2,"b",4,[new("Quest","Story","game")],FashionKnowledge.Yes,FashionKnowledge.No),F(3,"c"));
 Check(FashionPolicy.Filter(cat,FO(),new(){Marketable=1}).Single().Id==2);
 Check(FashionPolicy.Filter(cat,FO(),new(){Category="Online Store"}).Single().Id==1);
 Check(FashionPolicy.Filter(cat,FO(),new(){Category="Unknown"}).Single().Id==3);
 Check(FashionPolicy.Filter(cat,FO(),new(){Expansion=4}).Single().Id==2&&FashionPolicy.Filter(cat,FO(),new(){Expansion=-1}).Single().Id==3);
 Check(FashionPolicy.Filter(cat,FO(),new(){CashShop=1}).Single().Id==1&&FashionPolicy.Filter(cat,FO(),new(){Obtainable=2}).Single().Id==2);
 foreach(var value in Enum.GetValues<FashionKnowledge>())foreach(var filter in new[]{0,1,2,3})
  Check(FashionPolicy.Knowledge(value,filter)==(filter==0||filter==1&&value==FashionKnowledge.Yes||filter==2&&value==FashionKnowledge.No||filter==3&&value==FashionKnowledge.Unknown));
});
Test("Live ornament resolution handles none, unknown, retired and unloaded/transformed actors",()=>{
 bool Known(uint id)=>id==16;
 Check(IdentificationPolicy.Resolve(new(true,true,false,16),true,Known)==new IdentificationPart(IdentificationState.Found,16));
 Check(IdentificationPolicy.Resolve(new(true,true,false,0),true,Known).State==IdentificationState.None);
 Check(IdentificationPolicy.Resolve(new(true,true,false,22),true,Known).State==IdentificationState.UnknownId);
 Check(IdentificationPolicy.Resolve(new(true,true,false,999),true,Known).State==IdentificationState.UnknownId);
 Check(IdentificationPolicy.Resolve(new(false,true,false,16),true,Known).State==IdentificationState.Unavailable);
 Check(IdentificationPolicy.Resolve(new(true,false,false,16),true,Known).State==IdentificationState.Unavailable);
 Check(IdentificationPolicy.Resolve(new(true,true,true,16),true,Known).State==IdentificationState.Unavailable);
 Check(IdentificationPolicy.Resolve(new(true,true,false,16),false,_=>throw new Exception()).State==IdentificationState.Off);
});
Test("Shared identification rejects stale tickets, identity reuse, zoning, logout, option Off and disposal",()=>{
 var t=new IdentificationTicket(10,20,3,40,50,100);
 bool Allowed(ulong obj=10,uint entity=20,ushort index=3,ulong session=40,uint zone=50,long now=101,bool enabled=true,bool ready=true,bool player=true,bool valid=true,bool disposed=false)=>
  IdentificationPolicy.TicketMatches(t,obj,entity,index,session,zone,now,enabled,ready,player,valid,disposed);
 Check(Allowed()&&!Allowed(obj:11)&&!Allowed(entity:21)&&!Allowed(index:4)&&!Allowed(session:41)&&!Allowed(zone:51));
 Check(!Allowed(now:99)&&!Allowed(now:10101)&&!Allowed(enabled:false)&&!Allowed(ready:false)&&!Allowed(player:false)&&!Allowed(valid:false)&&!Allowed(disposed:true));
 var queue=new IdentificationQueue();queue.Request(t);queue.Request(t with{ObjectId=11});Check(queue.Take()!.Value.ObjectId==11&&queue.Take()==null);
 queue.Request(t);queue.Clear();Check(queue.Take()==null);
});


Test("Shared menu preserves Mount-only offers and adds the explicit Fashion no-accessory flow",()=>{
 Check(!IdentificationPolicy.Offer(false,false,false)&&!IdentificationPolicy.Offer(false,false,true));
 Check(!IdentificationPolicy.Offer(true,false,false)&&IdentificationPolicy.Offer(true,false,true));
 Check(IdentificationPolicy.Offer(false,true,false)&&IdentificationPolicy.Offer(false,true,true));
 Check(IdentificationPolicy.Offer(true,true,false)&&IdentificationPolicy.Offer(true,true,true));
});

OperationsTests.Run(Test, Check);
PerformancePolicyTests.Run(Test, Check);
Console.WriteLine(passed + " policy test groups passed.");

sealed class FakeLatencyRuntime : ILatencyRuntime
{
 public LatencyView View { get; set; }
 public bool Disposed { get; private set; }
 public FakeLatencyRuntime(LatencyMode mode) { View=new(mode==LatencyMode.Measure?LatencyState.Measuring:LatencyState.Compensating); }
 public void Dispose(){Disposed=true;}
}
sealed class LatencyCallTrace
{
 public int Before,Originals,After,Faults;
 public bool Return,ThrowBefore,ThrowAfter,ThrowOriginal,ThrowFault;
}
struct FakeLatencyCall : ILatencyInterceptedCall
{
 public LatencyCallTrace Trace;
 public void Before(){Trace.Before++;if(Trace.ThrowBefore)throw new Exception("observer");}
 public bool Original(){Trace.Originals++;if(Trace.ThrowOriginal)throw new InvalidOperationException("original");return Trace.Return;}
 public void After(bool result){Trace.After++;if(Trace.ThrowAfter)throw new Exception("observer");}
 public void Fault(){Trace.Faults++;if(Trace.ThrowFault)throw new Exception("fault observer");}
}

