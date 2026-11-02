using System.Text.Json;
using SentinelHub.DeathRecaps;
internal static class DeathTests
{
    public static void Run(Action<string, Action> test)
    {
        static void Check(bool v) { if (!v) throw new Exception("Death recap policy regression"); }
        var now = DateTimeOffset.UtcNow;
        test("PvE alive/dead transitions, initial dead and respawn", () => {
            var l = new DeathLedger(); Check(!l.Vitals(1, now, 0, 100, 0, []));
            Check(!l.Vitals(1, now, 100, 100, 10, [])); Check(l.Vitals(1, now.AddSeconds(1), 0, 100, 0, []));
            Check(!l.Vitals(1, now.AddSeconds(2), 0, 100, 0, []));
            l.Vitals(1, now.AddSeconds(3), 100, 100, 0, []); Check(l.Snapshot(1, now.AddSeconds(3)).Length == 1);
            Check(l.Vitals(1, now.AddSeconds(4), 0, 100, 0, []));
        });
        test("missing fatal evidence never becomes a killer or exact HP estimate", () => {
            var r = DeathPreview.Create(); Check(r.Fatal is null && r.Events.Where(e => e.Kind == RecapEventKind.Damage).All(e => e.Hp is null));
            Check(DeathPolicy.Overkill(85000, null, 10000) is null && DeathPolicy.Overkill(85000, 62000, null) is null);
            Check(DeathPolicy.Overkill(85000, 62000, 10000) == 13000 && DeathPolicy.Overkill(10000, 62000, 10000) == 0);
        });
        test("chronological damage healing and status snapshots preserve values", () => {
            var l = new DeathLedger(); l.Vitals(1, now, 900, 1000, 10, [new(1, "Observed status", 2)]);
            l.Action(1, new(now.AddSeconds(2), 0, RecapEventKind.Healing, "Healer", 24, 1, "Heal", 50, null, null, null, [], "Action effect", true));
            l.Action(1, new(now.AddSeconds(1), 0, RecapEventKind.Damage, "Enemy", 32, 2, "Attack", 100, null, null, null, [], "Action effect", true));
            var e = l.Snapshot(1, now.AddSeconds(3)); Check(e.Length == 3 && e[1].Kind == RecapEventKind.Damage && e[2].Kind == RecapEventKind.Healing);
            Check(e[0].Hp == 900 && e[0].ShieldPercent == 10 && e[0].Statuses[0].Stacks == 2);
            Check(DeathPolicy.DecodeAmount(7, 2, 0x40) == 131079 && DeathPolicy.DecodeAmount(7, 2, 0) == 7);
        });
        test("bounded event buffers and party roster/session cleanup", () => {
            var l = new DeathLedger(); for (uint i = 1; i <= 9; i++) l.Vitals(i, now, 100, 100, 0, []);
            Check(l.Snapshot(9, now).Length == 0); l.RemoveAbsent([1]); Check(l.Snapshot(2, now).Length == 0);
            for (var i = 0; i < 2000; i++) l.Vitals(1, now.AddMilliseconds(i), (uint)(i + 1), 3000, 0, []);
            Check(l.Snapshot(1, now.AddSeconds(3)).Length == 512 && l.Truncated); l.Clear(); Check(l.Snapshot(1, now).Length == 0);
        });
        test("unchanged HP snapshots allocate zero and 60 second retention expires", () => {
            var l = new DeathLedger(); l.Vitals(1, now, 100, 100, 0, []);
            for (var i = 0; i < 200; i++) l.Vitals(1, now, 100, 100, 0, []);
            var b = GC.GetAllocatedBytesForCurrentThread(); for (var i = 0; i < 10000; i++) l.Vitals(1, now, 100, 100, 0, []);
            Check(GC.GetAllocatedBytesForCurrentThread() == b); l.Vitals(1, now.AddSeconds(61), 100, 100, 0, []); Check(l.Snapshot(1, now.AddSeconds(61)).Length == 0);
        });
        test("PvP job aggregation, maximum focus, freshness and independent evidence", () => {
            var r = DeathPreview.Create(); var t = r.Threat!; var s = DeathPolicy.Summarize(t, 15);
            Check(s.NearDeath == 5 && s.Maximum == 5 && s.Jobs.Single(j => j.Id == 22).Count == 2 && s.Fresh);
            Check(r.Events.Count(e => e.Kind == RecapEventKind.Damage) == 1 && s.Jobs.Length == 4);
            var stale = DeathPolicy.Summarize(t with { LastSampleAt = t.At.AddSeconds(-2) }, 15); Check(!stale.Fresh && stale.NearDeath == 0);
        });
        test("15 and 30 second targeting windows keep the boundary baseline", () => {
            var t = DeathPreview.Create().Threat!; var a = t.Changes[^1].Targeters;
            t = t with { WindowStart = t.At.AddSeconds(-30), Changes = [new(t.At.AddSeconds(-30), a), new(t.At.AddSeconds(-5), a[..1])] };
            var s = DeathPolicy.Summarize(t, 15); Check(s.Maximum == 5 && s.NearDeath == 1 && s.ObservedSeconds <= 15);
        });
        test("versioned HUD contract rejects invalid or oversized data", () => {
            var t = DeathPreview.Create().Threat!; var json = JsonSerializer.Serialize(new ThreatEnvelope(1, Guid.NewGuid(), [t]));
            Check(DeathPolicy.ParseThreat(json)?.Deaths.Length == 1);
            Check(DeathPolicy.ParseThreat("{}") is null && DeathPolicy.ParseThreat(json.Replace("\"Version\":1", "\"Version\":2")) is null);
            Check(DeathPolicy.ParseThreat(new string(' ', 1_000_001)) is null);
            Check(DeathPolicy.ParseThreat(JsonSerializer.Serialize(new ThreatEnvelope(1, Guid.Empty, [t with { Changes = [new(t.At.AddSeconds(1), [])] }]))) is null);
        });
        test("history retention, deletion, exact round-trip and corrupt recovery", () => {
            var h = new DeathHistory(); var r = DeathPreview.Create(false) with { Synthetic = false };
            for (var i = 0; i < 40; i++) h.Add(r with { Id = Guid.NewGuid(), At = r.At.AddSeconds(i) }, 10);
            Check(h.Snapshot.Length == 10); var first = h.Snapshot[0]; h.Delete(first.Id); Check(h.Snapshot.Length == 9);
            var saved = h.Serialize(false); var loaded = new DeathHistory(); Check(loaded.Load(saved, 10));
            Check(loaded.Snapshot[0].Id == h.Snapshot[0].Id && loaded.Snapshot[0].Events[0].Hp == 62000);
            Check(!loaded.Load("broken", 10) && loaded.Snapshot.Length == 9); loaded.Clear(); Check(loaded.Snapshot.Length == 0);
        });
        test("privacy redacts persisted names and removes transient entity identifiers", () => {
            var r = DeathPreview.Create() with { Synthetic = false };
            r = r with { Threat = r.Threat! with { LocalEntityId = 0x1234 } };
            var hidden = DeathPolicy.Redact(r, false);
            Check(hidden.Character == "You" && hidden.Threat!.LocalEntityId == 0 && hidden.Threat.Changes[0].Targeters[0].Name.StartsWith("Enemy "));
            Check(hidden.Events.Where(e => e.SourceIsPlayer).All(e => e.Source == "Player"));
            Check(DeathPolicy.Redact(r, true).Threat!.LocalEntityId == 0);
        });
        test("synthetic previews never recover as saved deaths and settings normalize", () => {
            var h = new DeathHistory(); Check(h.Load(JsonSerializer.Serialize(new[] { DeathPreview.Create() }), 30) && h.Snapshot.Length == 0);
            var s = new DeathSettings { MaximumHistory = 10000, ReviewSeconds = 11, ThreatSeconds = -1, Popup = (DeathPopupMode)100 }; s.Normalize();
            Check(s.MaximumHistory == 100 && s.ReviewSeconds == 30 && s.ThreatSeconds == 15 && s.Popup == DeathPopupMode.Both && !s.DetailedCapture && s.HideNames && !s.SaveNames);
        });
    }
}
