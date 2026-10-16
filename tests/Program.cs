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
 Check(calls.SequenceEqual(new[] { "vnavmesh" }));
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
 "/sranksentinel", "/pvpsentinel config", "/shud", "/classysentinel config", "/sprofiles", "/srelay", "/sentinelhunts", "/shub" }));
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
 Check(!json.Contains("AntiAfk") && !json.Contains("Latency"));
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
 Check(UtilityRegistry.Planned.Length == 3);
 Check(UtilityRegistry.Planned.All(u => u.Status == "Planned / inactive"));
 Check(typeof(HubSettings).GetProperties().All(p => !p.Name.Contains("Anti") && !p.Name.Contains("Quest") && !p.Name.Contains("Compensation")));
});
Console.WriteLine(passed + " policy test groups passed.");
