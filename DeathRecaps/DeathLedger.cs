namespace SentinelHub.DeathRecaps;

// Framework-owned, bounded per-observed-party-member rings. Unchanged samples allocate nothing.
public sealed class DeathLedger
{
    private sealed class Track { public readonly Queue<RecapEvent> Events = new(); public uint Hp, Max; public byte Shield; public bool Alive; public bool Initialized; public RecapStatus[] Statuses = []; }
    private readonly Dictionary<uint, Track> tracks = new(8);
    private long order;
    public bool Truncated { get; private set; }
    public void Clear() { tracks.Clear(); Truncated = false; }
    public void RemoveAbsent(ReadOnlySpan<uint> ids)
    {
        // Party membership changes are rare; no allocations in the ordinary sample path.
        if (tracks.Count == 0) return;
        Span<uint> remove = stackalloc uint[8]; var count = 0;
        foreach (var id in tracks.Keys) if (!ids.Contains(id) && count < remove.Length) remove[count++] = id;
        for (var i = 0; i < count; i++) tracks.Remove(remove[i]);
    }
    public bool Vitals(uint id, DateTimeOffset now, uint hp, uint maximum, byte shield, RecapStatus[] statuses)
    {
        if (!tracks.TryGetValue(id, out var t)) { if (tracks.Count >= 8) return false; tracks[id] = t = new(); }
        var died = t.Initialized && t.Alive && hp == 0;
        if (hp > 0 && t.Initialized && !t.Alive) t.Events.Clear();
        var statusChange = !t.Statuses.AsSpan().SequenceEqual(statuses);
        if (!t.Initialized || hp != t.Hp || maximum != t.Max || shield != t.Shield || statusChange)
            Add(t, new(now, ++order, statusChange && hp == t.Hp ? RecapEventKind.Status : RecapEventKind.Vitals,
                "", 0, 0, "Observed HP / shield / statuses", null, hp, maximum, shield, statusChange ? statuses : [],
                "Client snapshot; HP changes are not attributed damage or healing."));
        t.Hp = hp; t.Max = maximum; t.Shield = shield; t.Statuses = statuses; t.Alive = hp > 0; t.Initialized = true;
        Prune(t, now); return died;
    }
    public void Action(uint target, RecapEvent e)
    {
        if (tracks.TryGetValue(target, out var t)) Add(t, e with { Order = ++order });
    }
    public RecapEvent[] Snapshot(uint id, DateTimeOffset now) => tracks.TryGetValue(id, out var t)
        ? t.Events.Where(e => e.At <= now && e.At >= now.AddSeconds(-60)).OrderBy(e => e.At).ThenBy(e => e.Order).ToArray() : [];
    private void Add(Track t, RecapEvent e)
    {
        while (t.Events.Count >= DeathPolicy.MaximumEvents) { t.Events.Dequeue(); Truncated = true; }
        t.Events.Enqueue(e); Prune(t, e.At);
    }
    private static void Prune(Track t, DateTimeOffset now) { while (t.Events.TryPeek(out var e) && now - e.At > TimeSpan.FromSeconds(60)) t.Events.Dequeue(); }
}
