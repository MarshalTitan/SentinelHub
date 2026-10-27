using System.Text.Json;
using SentinelHub.Configuration;
using SentinelHub.TreasureMaps;

internal static class TreasureTests
{
    internal static TreasureLocation Location(int x = 123456, uint map = 400) => new(100, map, x, -34567, "A multi-map zone", 24.2f, 18.5f, [2, 39, 1, 3]);
    public static void Run(Action<string, Action> test, Action<bool> check)
    {
        var a = new TreasureOwner("First Player", 20); var b = new TreasureOwner("Second Player", 30);
        var time = DateTimeOffset.Parse("2026-10-27T12:00:00Z");
        test("Treasure coordinates preserve raw world positions, map scaling and offsets", () => {
            check(TreasurePolicy.Raw(123.456f) == 123456);
            check(Math.Abs(TreasurePolicy.Display(0, 100, 0) - 21.5f) < .001f);
            check(Math.Abs(TreasurePolicy.Display(0, 200, 0) - 11.25f) < .001f);
            check(Math.Abs(TreasurePolicy.Display(0, 100, 100) - TreasurePolicy.Display(100000, 100, 0)) < .001f);
            var variant = Location(map: 401); check(!Location().SameFlag(variant));
            check(Location().TerritoryId == variant.TerritoryId && Location().RawX == variant.RawX);
        });
        test("Treasure capture allowlist defaults to Party and independently opts in Alliance", () => {
            foreach (var channel in Enumerable.Range(0, 1200))
                check(TreasurePolicy.Capture(channel, true, true, false) == (channel == 14));
            check(TreasurePolicy.Capture(15, true, false, true));
            check(!TreasurePolicy.Capture(14, false, true, true));
            check(!TreasurePolicy.Capture(14, true, false, true));
        });
        test("Treasure duplicate protection retains independent owners and additional maps", () => {
            var q = new TreasureQueue(); q.Bind(new());
            check(q.Add(a, Location(), time, 50));
            check(!q.Add(a, Location(), time.AddMinutes(1), 50));
            check(q.Add(b, Location(), time, 50));
            check(q.Add(a, Location(234567), time, 50));
            check(q.Add(new("First Player", 99), Location(), time, 50));
            check(q.Add(a, Location(), time, 50, additional: true));
            check(q.Entries.Length == 5 && q.Entries.Select(e => e.Id).Distinct().Count() == 5);
        });
        test("Treasure reordering and Complete promote next while Undo restores the prior position", () => {
            var q = new TreasureQueue(); q.Bind(new());
            q.Add(a, Location(), time, 50); q.Add(b, Location(), time, 50); q.Add(a, Location(234567), time, 50);
            var original = q.Entries.ToArray();
            check(q.Move(original[2].Id, -1)); check(q.Entries[1].Id == original[2].Id);
            check(q.Move(original[2].Id, 1)); check(q.Entries.SequenceEqual(original));
            check(!q.Move(original[0].Id, -1) && !q.Move(original[2].Id, 1));
            check(q.Remove(original[1].Id, true, 100)); check(q.Entries.Length == 2 && q.Entries[1].Id == original[2].Id);
            check(q.Undo(101, 50)); check(q.Entries.SequenceEqual(original));
            check(q.Remove(original[0].Id, true, 200)); check(q.Entries[0].Id == original[1].Id);
            check(q.Undo(201, 50)); check(q.Entries[0].Id == original[0].Id);
        });
        test("Treasure Undo is bounded, does not evict a full queue and Delete creates no completion", () => {
            var q = new TreasureQueue(); q.Bind(new()); q.Add(a, Location(), time, 5);
            var id = q.Entries[0].Id; check(q.Remove(id, true, 0)); check(q.CanUndo(14999));
            check(!q.CanUndo(15000)); q.Expire(15000); check(!q.Undo(15001, 5));
            q.Add(a, Location(), time, 5); id = q.Entries[0].Id; check(q.Remove(id, false, 0)); check(!q.CanUndo(1));
            q.Add(a, Location(), time, 5); id = q.Entries[0].Id; q.Remove(id, true, 1);
            for (var i = 0; i < 5; i++) q.Add(b, Location(i * 1000), time, 5);
            check(!q.Undo(2, 5) && q.Entries.Length == 5 && q.CanUndo(2));
            q.Clear(); check(q.Entries.Length == 0 && !q.CanUndo(3));
        });
        test("Treasure explicit repost guard ignores only the local echo and expires", () => {
            var q = new TreasureQueue(); q.Bind(new()); q.Shared(Location(), a, 100);
            check(q.IsRepost(a, Location(), 101)); check(!q.IsRepost(b, Location(), 101));
            check(!q.IsRepost(a, Location(1), 101)); check(!q.IsRepost(a, Location(), 30100));
            q.ClearTransient(); check(!q.IsRepost(a, Location(), 102));
        });
        test("Treasure unfinished queues round-trip by character without persisting Undo", () => {
            var settings = new HubSettings(); var first = new CharacterTreasureQueue(); var second = new CharacterTreasureQueue();
            var q = new TreasureQueue(); q.Bind(first); q.Add(a, Location(), time, 50);
            first.Current = new(3, 0, 2001000, Location());
            settings.TreasureMaps.Characters["0000000000000001"] = first;
            settings.TreasureMaps.Characters["0000000000000002"] = second;
            var restored = JsonSerializer.Deserialize<HubSettings>(JsonSerializer.Serialize(settings))!; restored.Normalize();
            var one = restored.TreasureMaps.Characters["0000000000000001"];
            check(one.Entries.Count == 1 && one.Entries[0].Location.Payload.SequenceEqual(Location().Payload));
            check(one.Current?.MapIdForTest() == 400 && restored.TreasureMaps.Characters["0000000000000002"].Entries.Count == 0);
            q.Bind(one); check(!q.CanUndo(0)); q.Detach(); check(q.Entries.Length == 0 && one.Entries.Count == 1);
        });
        test("Treasure saved-data normalization rejects corrupt entries without trimming unfinished maps", () => {
            var s = new TreasureSettings { MaximumEntries = -1, FloatingWidth = float.NaN, FloatingY = float.PositiveInfinity };
            var state = new CharacterTreasureQueue(); var q = new TreasureQueue(); q.Bind(state);
            for (var i = 0; i < 7; i++) q.Add(a, Location(i * 1000), time, 50);
            state.Entries.Add(new(Guid.NewGuid(), a, Location() with { MapId = 0 }, time));
            s.Characters["0000000000000001"] = state; s.Characters["invalid"] = new(); s.Normalize();
            check(s.MaximumEntries == 5 && state.Entries.Count == 7 && s.Characters.Count == 1);
            check(float.IsFinite(s.FloatingWidth) && float.IsFinite(s.FloatingY));
        });
        test("Treasure idle queue reads allocate nothing and preserve cached snapshots", () => {
            var q = new TreasureQueue(); q.Bind(new()); q.Add(a, Location(), time, 50); var view = q.Entries;
            var start = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10000; i++) { _ = q.Entries; _ = q.CanUndo(i); }
            check(GC.GetAllocatedBytesForCurrentThread() == start && ReferenceEquals(view, q.Entries));
            check((int)HubPage.RecentActivity == 11 && (int)HubPage.TreasureMaps == 16);
        });
        test("Treasure native observation forwards Original once and preserves its result on observer failure", () => {
            var calls = 0; var observations = 0;
            var result = TreasureNativeBoundary.Forward(() => { calls++; return 123; }, () => { observations++; check(calls == 1); throw new Exception(); });
            check(result == 123 && calls == 1 && observations == 1);
            try { TreasureNativeBoundary.Forward<int>(() => { calls++; throw new InvalidOperationException(); }, () => observations++); }
            catch (InvalidOperationException) { }
            check(calls == 2 && observations == 1);
        });
    }
    private static uint MapIdForTest(this OwnTreasureMap map) => map.Location.MapId;
}
