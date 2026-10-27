namespace SentinelHub.TreasureMaps;

// One character at a time. Undo is a single short-lived RAM-only slot, not a history.
public sealed class TreasureQueue
{
    private CharacterTreasureQueue state = new();
    private TreasureEntry[] snapshot = [];
    private (TreasureEntry Entry, int Index, long Expires)? undo;
    private (TreasureLocation Location, TreasureOwner Self, long Expires)? repost;
    public TreasureEntry[] Entries => snapshot;
    public OwnTreasureMap? Current { get => state.Current; set => state.Current = value; }
    public bool CanUndo(long now) => undo is { } u && now < u.Expires;
    public void Bind(CharacterTreasureQueue value) { state = value; undo = null; repost = null; Rebuild(); }
    public void Detach() { state = new(); snapshot = []; undo = null; repost = null; }
    public bool Add(TreasureOwner owner, TreasureLocation location, DateTimeOffset received, int maximum, bool additional = false)
    {
        if (!owner.Valid || !location.Valid || state.Entries.Count >= maximum) return false;
        if (!additional && state.Entries.Any(e => e.Owner.Key == owner.Key && e.Location.SameFlag(location))) return false;
        state.Entries.Add(new(Guid.NewGuid(), owner, location, received)); Rebuild(); return true;
    }
    public bool Remove(Guid id, bool complete, long now)
    {
        var index = state.Entries.FindIndex(e => e.Id == id); if (index < 0) return false;
        var value = state.Entries[index]; state.Entries.RemoveAt(index);
        if (complete) undo = (value, index, now + 15_000);
        else if (undo?.Entry.Id == id) undo = null;
        Rebuild(); return true;
    }
    public bool Undo(long now, int maximum)
    {
        if (!CanUndo(now) || state.Entries.Count >= maximum) return false;
        var u = undo!.Value; undo = null;
        if (state.Entries.Any(e => e.Id == u.Entry.Id)) return false;
        state.Entries.Insert(Math.Clamp(u.Index, 0, state.Entries.Count), u.Entry); Rebuild(); return true;
    }
    public bool Move(Guid id, int delta)
    {
        var from = state.Entries.FindIndex(e => e.Id == id); var to = from + delta;
        if (from < 0 || to < 0 || to >= state.Entries.Count || Math.Abs(delta) != 1) return false;
        (state.Entries[from], state.Entries[to]) = (state.Entries[to], state.Entries[from]); Rebuild(); return true;
    }
    public void Clear() { state.Entries.Clear(); undo = null; repost = null; Rebuild(); }
    public void ClearTransient() { undo = null; repost = null; }
    public void Shared(TreasureLocation location, TreasureOwner self, long now) => repost = (location, self, now + 30_000);
    public bool IsRepost(TreasureOwner owner, TreasureLocation location, long now)
        => repost is { } r && now < r.Expires && owner.Key == r.Self.Key && location.SameFlag(r.Location);
    public bool Expire(long now)
    {
        var changed = undo is { } u && now >= u.Expires;
        if (changed) undo = null;
        if (repost is { } r && now >= r.Expires) repost = null;
        return changed;
    }
    private void Rebuild() => snapshot = state.Entries.ToArray();
}

public static class TreasurePolicy
{
    public static bool Capture(int channel, bool enabled, bool party, bool alliance)
        => enabled && (channel == 14 && party || channel == 15 && alliance);
    public static int Raw(float world) => checked((int)(world * 1000f));
    public static float Display(int raw, ushort size, short offset)
    {
        if (size == 0) throw new ArgumentOutOfRangeException(nameof(size));
        var scale = size / 100f;
        return 1 + 41f / scale * (((raw / 1000f + offset) * scale + 1024f) / 2048f);
    }
}

public static class TreasureNativeBoundary
{
    public static T Forward<T>(Func<T> original, Action observe)
    {
        var result = original(); // Exactly once, before any observation, including on failure.
        try { observe(); } catch { }
        return result;
    }
}
