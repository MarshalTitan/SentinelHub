namespace SentinelHub.TreasureMaps;

public sealed record TreasureLocation(uint TerritoryId, uint MapId, int RawX, int RawY,
    string Zone, float X, float Y, byte[] Payload)
{
    public string Label => $"{Zone} ({Math.Truncate((X + .02) * 10) / 10:0.0}, {Math.Truncate((Y + .02) * 10) / 10:0.0})";
    public bool SameFlag(TreasureLocation other) => TerritoryId == other.TerritoryId && MapId == other.MapId
        && Math.Abs((long)RawX - other.RawX) <= 2 && Math.Abs((long)RawY - other.RawY) <= 2;
    public bool Valid => TerritoryId > 0 && MapId > 0 && Math.Abs((long)RawX) <= 100_000_000
        && Math.Abs((long)RawY) <= 100_000_000 && float.IsFinite(X) && float.IsFinite(Y)
        && Zone is { Length: > 0 and <= 256 } && Payload is { Length: <= 2048 };
}
public sealed record TreasureOwner(string Name, uint HomeWorld)
{
    public string Key => Name.Trim().ToUpperInvariant() + "@" + HomeWorld;
    public bool Valid => !string.IsNullOrWhiteSpace(Name) && Name.Length <= 128 && !Name.Any(char.IsControl);
}
public sealed record TreasureEntry(Guid Id, TreasureOwner Owner, TreasureLocation Location, DateTimeOffset ReceivedAt);
public sealed record OwnTreasureMap(uint RankId, ushort SpotId, uint EventItemId, TreasureLocation Location);
public sealed class CharacterTreasureQueue
{
    public List<TreasureEntry> Entries { get; set; } = [];
    public OwnTreasureMap? Current { get; set; }
}
public sealed class TreasureSettings
{
    public bool Enabled { get; set; }
    public bool DetectDeciphered { get; set; } = true;
    public bool OpenAfterDecipher { get; set; } = true;
    public bool OpenWhenViewed { get; set; } = true;
    public bool CaptureParty { get; set; } = true;
    public bool CaptureAlliance { get; set; }
    public bool AddOwnMap { get; set; } = true;
    public bool FloatingOpen { get; set; }
    public int MaximumEntries { get; set; } = 50;
    public float FloatingX { get; set; } = 100;
    public float FloatingY { get; set; } = 100;
    public float FloatingWidth { get; set; } = 420;
    public float FloatingHeight { get; set; } = 420;
    public Dictionary<string, CharacterTreasureQueue> Characters { get; set; } = [];
    public void Normalize()
    {
        MaximumEntries = Math.Clamp(MaximumEntries, 5, 200);
        if (!float.IsFinite(FloatingX)) FloatingX = 100;
        if (!float.IsFinite(FloatingY)) FloatingY = 100;
        FloatingWidth = float.IsFinite(FloatingWidth) ? Math.Clamp(FloatingWidth, 300, 2000) : 420;
        FloatingHeight = float.IsFinite(FloatingHeight) ? Math.Clamp(FloatingHeight, 220, 2000) : 420;
        Characters ??= [];
        foreach (var key in Characters.Keys.ToArray())
        {
            if (!ulong.TryParse(key, System.Globalization.NumberStyles.HexNumber, null, out var id) || id == 0
                || Characters[key] is null) { Characters.Remove(key); continue; }
            var q = Characters[key]; q.Entries ??= [];
            var ids = new HashSet<Guid>();
            q.Entries.RemoveAll(e => e is null || e.Id == Guid.Empty || !ids.Add(e.Id)
                || e.Owner is null || !e.Owner.Valid || e.Location is null || !e.Location.Valid);
            // Never silently trim unfinished maps when the configured maximum is lowered.
            if (q.Current is { } current && (current.Location is null || !current.Location.Valid
                || current.RankId == 0 || current.EventItemId == 0)) q.Current = null;
        }
    }
}
public sealed record TreasureView(bool Available, TreasureEntry[] Entries, OwnTreasureMap? Current,
    bool CanUndo, string Detection, string Notice)
{
    public static readonly TreasureView Empty = new(false, [], null, false, "Disabled", "");
}
public enum TreasureAction { Open, Share, Complete, Delete, Up, Down, Undo, Clear, OpenMine, AddMine, AddCopy, Next }
public readonly record struct TreasureRequest(TreasureAction Action, Guid EntryId = default);
