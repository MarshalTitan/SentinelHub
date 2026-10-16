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
 Check(!result.AntiAfkEnabled && !result.AntiAfkPreferenceImported && !json.Contains("Latency"));
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
Test("utility enable boundaries: no engines or active configuration", () => {
 Check(UtilityRegistry.Planned.Length == 2);
 Check(UtilityRegistry.Planned.All(u => u.Status == "Planned / inactive"));
 Check(typeof(HubSettings).GetProperties().All(p => !p.Name.Contains("Quest") && !p.Name.Contains("Compensation")));
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

Console.WriteLine(passed + " policy test groups passed.");
