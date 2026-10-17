namespace SentinelHub.Mounts;

public enum MountKnowledge { Unknown, Yes, No }
public enum MountOwnership { Unknown, Unlocked, Locked }
public enum MountCategory { Unknown, TrialCollection, Quest, Achievement, Exchange }
public enum MountLoadState { NotLoaded, Loading, Ready, Unavailable }
public enum MountSort { Name, NameDescending, Id, SeatsDescending }
public sealed record MountSource(MountCategory Category, string Description, string Evidence);
public sealed record MountUnlockItem(uint Id, string Name, bool Marketable);
public sealed record MountExchange(uint MountId, uint ItemId, string ItemName, long Required, uint ShopId);
public sealed record MountEntry(uint Id, string Name, string EnglishName, uint Icon, int Seats,
    string[] Actions, uint BgmId, int BgmMountCount, int? Expansion,
    MountKnowledge Marketable, MountKnowledge Obtainable, MountSource[] Sources, MountUnlockItem[] Items);
public sealed record MountTrackSeed(string Id, int Expansion, string Reward, string Quest, string Evidence, string[] Members);
public sealed record MountTrackMember(string EnglishName, uint? MountId);
public sealed record MountTrack(MountTrackSeed Seed, uint? RewardId, MountTrackMember[] Members);
public sealed record MountCatalog(MountEntry[] Entries, MountTrack[] Tracks, MountExchange[] Exchanges,
    IReadOnlyDictionary<int, string> ExpansionNames, int UnresolvedMetadata)
{
    public static readonly MountCatalog Empty = new([], [], [], new Dictionary<int, string>(), 0);
}
public sealed record MountCharacterSnapshot(bool Available, bool InventoryAvailable,
    IReadOnlyDictionary<uint, bool> Unlocks, IReadOnlyDictionary<uint, long> Counts)
{
    public static readonly MountCharacterSnapshot Unknown = new(false, false, new Dictionary<uint, bool>(), new Dictionary<uint, long>());
    public MountOwnership Ownership(uint id) => Available && Unlocks.TryGetValue(id, out var unlocked)
        ? unlocked ? MountOwnership.Unlocked : MountOwnership.Locked : MountOwnership.Unknown;
}
public sealed record MountCollectionView(MountLoadState State, MountCatalog Catalog, MountCharacterSnapshot Character)
{
    public static readonly MountCollectionView Empty = new(MountLoadState.NotLoaded, MountCatalog.Empty, MountCharacterSnapshot.Unknown);
}
public sealed class MountFilter
{
    public string Search = "";
    public int Ownership; // 0 all, 1 missing, 2 unlocked, 3 unknown
    public int Expansion = -2; // -2 all, -1 unknown
    public int Obtainable; // 0 all, 1 verified yes, 2 verified no, 3 unknown
    public int Marketable; // same tri-state
    public MountCategory? Category;
    public MountSort Sort;
    public void Reset() { Search = ""; Ownership = Obtainable = Marketable = 0; Expansion = -2; Category = null; Sort = MountSort.Name; }
}
public readonly record struct MountCompletion(int Unlocked, int Locked, int Unknown)
{
    public int Total => Unlocked + Locked + Unknown;
    public string Label => Unknown == 0 ? $"{Unlocked} / {Total}" : $"{Unlocked} unlocked / {Total} ({Unknown} unknown)";
}
public static class MountPolicy
{
    // A journal candidate must have a real model/icon, a name and nonnegative unlock order.
    // Do not use row-ID ranges, publication patch guesses or an English name suffix as eligibility.
    public static bool IsCollectionRow(uint id, string name, uint icon, uint model, int order) =>
        id != 0 && !string.IsNullOrWhiteSpace(name) && icon != 0 && model != 0 && order >= 0;
    public static uint? UnlockMount(uint action, uint parameter, ISet<uint> mounts) =>
        action == 1322 && parameter != 0 && mounts.Contains(parameter) ? parameter : null;
    public static MountKnowledge MarketStatus(IEnumerable<MountUnlockItem> items)
    {
        var known = items.ToArray();
        return known.Length == 0 ? MountKnowledge.Unknown : known.Any(x => x.Marketable) ? MountKnowledge.Yes : MountKnowledge.No;
    }
    public static bool KnowledgeMatches(MountKnowledge value, int filter) => filter switch
    {
        0 => true, 1 => value == MountKnowledge.Yes, 2 => value == MountKnowledge.No, 3 => value == MountKnowledge.Unknown, _ => false,
    };
    public static MountEntry[] Filter(MountCatalog catalog, MountCharacterSnapshot character, MountFilter filter)
    {
        var query = (filter.Search ?? "").Trim();
        if (query.Length > 128) query = query[..128];
        var rows = catalog.Entries.Where(m =>
            (query.Length == 0 || m.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
             || m.EnglishName.Contains(query, StringComparison.OrdinalIgnoreCase) || m.Id.ToString() == query
             || m.Items.Any(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
             || m.Sources.Any(s => s.Description.Contains(query, StringComparison.OrdinalIgnoreCase)))
            && (filter.Ownership switch {
                0 => true, 1 => character.Ownership(m.Id) == MountOwnership.Locked,
                2 => character.Ownership(m.Id) == MountOwnership.Unlocked,
                3 => character.Ownership(m.Id) == MountOwnership.Unknown, _ => false })
            && (filter.Expansion == -2 || (m.Expansion ?? -1) == filter.Expansion)
            && KnowledgeMatches(m.Obtainable, filter.Obtainable)
            && KnowledgeMatches(m.Marketable, filter.Marketable)
            && (filter.Category is null || filter.Category == MountCategory.Unknown && m.Sources.Length == 0
                || m.Sources.Any(s => s.Category == filter.Category)));
        IOrderedEnumerable<MountEntry> ordered = filter.Sort switch {
            MountSort.NameDescending => rows.OrderByDescending(m => m.Name, StringComparer.OrdinalIgnoreCase),
            MountSort.Id => rows.OrderBy(m => m.Id),
            MountSort.SeatsDescending => rows.OrderByDescending(m => m.Seats).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase),
            _ => rows.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase),
        };
        return ordered.ThenBy(m => m.Id).ToArray();
    }
    public static MountCompletion Completion(IEnumerable<uint> ids, MountCharacterSnapshot character)
    {
        int yes = 0, no = 0, unknown = 0;
        foreach (var id in ids.Distinct()) switch (character.Ownership(id))
        {
            case MountOwnership.Unlocked: yes++; break;
            case MountOwnership.Locked: no++; break;
            default: unknown++; break;
        }
        return new(yes, no, unknown);
    }
    public static MountTrack ResolveTrack(MountTrackSeed seed, IEnumerable<MountEntry> entries)
    {
        var names = entries.GroupBy(e => e.EnglishName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count() == 1 ? (uint?)g.Single().Id : null, StringComparer.OrdinalIgnoreCase);
        uint? Find(string name) => names.GetValueOrDefault(name);
        return new(seed, Find(seed.Reward), seed.Members.Select(n => new MountTrackMember(n, Find(n))).ToArray());
    }
    public static MountCompletion TrackCompletion(MountTrack track, MountCharacterSnapshot character)
    {
        var known = Completion(track.Members.Where(m => m.MountId.HasValue).Select(m => m.MountId!.Value), character);
        return known with { Unknown = known.Unknown + track.Members.Count(m => !m.MountId.HasValue) };
    }
    // Only a simple, non-HQ, direct-item exchange is countable. This does not establish vendor availability.
    // Recognized shop modes only remap compact currency aliases below 8; never count those as item IDs.
    public static bool SafeTokenOffer(bool knownTrial, byte currencyMode, uint receiveCount, bool hq,
        int nonzeroCosts, uint costType, uint itemId, long required, bool untradable, string englishName, long collectability = 0) =>
        knownTrial && currencyMode is 0 or 4 or 8 or 16 && receiveCount == 1 && !hq && nonzeroCosts == 1 && costType == 0
        && collectability == 0 && itemId >= 8 && required is > 0 and <= 9999 && untradable
        && englishName.Contains("Totem", StringComparison.OrdinalIgnoreCase);
    public static double Progress(long count, long required) =>
        required <= 0 || count < 0 ? 0 : Math.Clamp((double)count / required, 0, 1);
}
// One character's ephemeral read-only observations. No names or identifiers enter exports/configuration.
public sealed class MountObservationSession
{
    private ulong owner;
    public MountCharacterSnapshot Snapshot { get; private set; } = MountCharacterSnapshot.Unknown;
    public void Observe(ulong session, bool ready, Func<MountCharacterSnapshot> read)
    {
        if (!ready || session == 0) { Clear(); return; }
        if (owner != session) { Clear(); owner = session; }
        // Never expose the previous character/sample after a failed read.
        Snapshot = MountCharacterSnapshot.Unknown;
        try { Snapshot = read(); } catch { Snapshot = MountCharacterSnapshot.Unknown; }
    }
    public void Clear() { owner = 0; Snapshot = MountCharacterSnapshot.Unknown; }
}
public readonly record struct MountLookupTicket(ulong ObjectId, ulong Session, uint Territory);
public static class MountLookupPolicy
{
    public static bool CanOpen(MountLookupTicket ticket, ulong currentObject, ulong currentSession, uint territory,
        bool enabled, bool ready, bool player, uint mountId, bool disposed) =>
        !disposed && enabled && ready && player && ticket.ObjectId != 0 && ticket.ObjectId == currentObject
        && ticket.Session != 0 && ticket.Session == currentSession && ticket.Territory == territory && mountId != 0;
}

public readonly record struct MountBagItem(uint ItemId, int Quantity, bool Symbolic = false);
public sealed record MountBagSnapshot(int Kind, bool Loaded, int DeclaredSize, MountBagItem[] Items);
public static class MountInventoryPolicy
{
    public static Dictionary<uint, long>? Count(IReadOnlyList<MountBagSnapshot> bags, ISet<uint> wanted)
    {
        if (bags.Count != 4 || !bags.Select(b => b.Kind).Order().SequenceEqual(new[] { 0, 1, 2, 3 })) return null;
        var result = wanted.ToDictionary(id => id, _ => 0L);
        foreach (var bag in bags)
        {
            if (!bag.Loaded || bag.DeclaredSize != 35 || bag.Items.Length != 35) return null;
            foreach (var item in bag.Items)
            {
                if (item.Symbolic || item.Quantity < 0 || item.Quantity > 999999) return null;
                if (wanted.Contains(item.ItemId)) result[item.ItemId] += item.Quantity;
            }
        }
        return result;
    }
}
