namespace SentinelHub.Fashion;
public enum FashionKnowledge { Unknown, Yes, No }
public enum FashionOwnership { Unknown, Unlocked, Locked }
public enum FashionLoad { NotLoaded, Loading, Ready, Unavailable }
public enum FashionSort { Name, NameDescending, Id, PatchDescending }
public sealed record FashionSource(string Category, string Description, string Evidence, string? Url = null);
public sealed record FashionItem(uint Id, string Name, bool Tradeable, bool Marketable);
public sealed record FashionEntry(uint Id, string Name, string EnglishName, uint Icon, uint Model, int Order,
    string? Patch, int? Expansion, FashionSource[] Sources, FashionItem[] Items,
    FashionKnowledge Tradeable, FashionKnowledge Marketable, FashionKnowledge CashShop,
    FashionKnowledge Obtainable, string AvailabilityEvidence, string? Note = null)
{
    public DateTimeOffset? AvailabilityExpiresUtc {get;init;}
}
public sealed record FashionCatalog(FashionEntry[] Entries, IReadOnlyDictionary<int,string> Expansions,
    int MetadataMatched, int MetadataUnmatched, string? MetadataDate)
{
    public static readonly FashionCatalog Empty = new([], new Dictionary<int,string>(), 0, 0, null);
}
public sealed record FashionCharacter(bool Available, IReadOnlyDictionary<uint,bool> Unlocks)
{
    public static readonly FashionCharacter Unknown = new(false,new Dictionary<uint,bool>());
    public FashionOwnership Ownership(uint id) => Available && Unlocks.TryGetValue(id,out var unlocked)
        ? unlocked ? FashionOwnership.Unlocked : FashionOwnership.Locked : FashionOwnership.Unknown;
}
public sealed record FashionView(FashionLoad State, FashionCatalog Catalog, FashionCharacter Character)
{
    public static readonly FashionView Empty = new(FashionLoad.NotLoaded,FashionCatalog.Empty,FashionCharacter.Unknown);
}
public sealed class FashionFilter
{
    public string Search = "";
    public int Ownership, Marketable, Obtainable, CashShop;
    public int Expansion = -2;
    public string? Category;
    public FashionSort Sort;
    public void Reset(){Search="";Ownership=Marketable=Obtainable=CashShop=0;Expansion=-2;Category=null;Sort=FashionSort.Name;}
}
public static class FashionPolicy
{
    // Retired ornament rows retained by the game after the official 7.0 Facewear conversion.
    public static bool Retired(uint id) => id is 22 or 25 or 26 or 32;
    public static bool Candidate(uint id,string name,uint icon,uint model,int order) =>
        id is >0 and <=ushort.MaxValue && !Retired(id) && !string.IsNullOrWhiteSpace(name) && icon>0 && model>0 && order>=0;
    public static FashionEntry[] Validate(IEnumerable<FashionEntry> rows)
    {
        var result=rows.ToArray();
        if(result.Any(r=>!Candidate(r.Id,r.Name,r.Icon,r.Model,r.Order))||result.Select(r=>r.Id).Distinct().Count()!=result.Length)
            throw new InvalidDataException("Invalid or duplicate fashion accessory identities");
        return result.OrderBy(r=>r.Id).ToArray();
    }
    public static uint? Unlock(uint action,uint parameter,ISet<uint> known) =>
        action==20086&&known.Contains(parameter)&&parameter!=0 ? parameter : null;
    public static FashionKnowledge ItemStatus(FashionItem[] items,bool market) =>
        items.Length==0?FashionKnowledge.Unknown:items.Any(i=>market?i.Marketable:i.Tradeable)?FashionKnowledge.Yes:FashionKnowledge.No;
    public static bool Knowledge(FashionKnowledge value,int filter) => filter switch
    {0=>true,1=>value==FashionKnowledge.Yes,2=>value==FashionKnowledge.No,3=>value==FashionKnowledge.Unknown,_=>false};
    public static FashionKnowledge CurrentAvailability(FashionEntry entry,DateTimeOffset now) =>
        entry.AvailabilityExpiresUtc is {} expiry&&now>=expiry?FashionKnowledge.Unknown:entry.Obtainable;
    public static FashionEntry[] Filter(FashionCatalog catalog,FashionCharacter character,FashionFilter f)
    {
        var term=(f.Search??"").Trim();if(term.Length>128)term=term[..128];
        var rows=catalog.Entries.Where(e=>
            (term.Length==0||e.Name.Contains(term,StringComparison.OrdinalIgnoreCase)||e.EnglishName.Contains(term,StringComparison.OrdinalIgnoreCase)
                ||e.Id.ToString()==term||e.Items.Any(i=>i.Name.Contains(term,StringComparison.OrdinalIgnoreCase))
                ||e.Sources.Any(s=>s.Description.Contains(term,StringComparison.OrdinalIgnoreCase)))
            &&(f.Ownership switch{0=>true,1=>character.Ownership(e.Id)==FashionOwnership.Locked,2=>character.Ownership(e.Id)==FashionOwnership.Unlocked,
                3=>character.Ownership(e.Id)==FashionOwnership.Unknown,_=>false})
            &&(f.Expansion==-2||(e.Expansion??-1)==f.Expansion)
            &&Knowledge(e.Marketable,f.Marketable)&&Knowledge(CurrentAvailability(e,DateTimeOffset.UtcNow),f.Obtainable)&&Knowledge(e.CashShop,f.CashShop)
            &&(f.Category==null||f.Category=="Unknown"&&e.Sources.Length==0||e.Sources.Any(s=>s.Category==f.Category)));
        return (f.Sort switch{
            FashionSort.NameDescending=>rows.OrderByDescending(e=>e.Name,StringComparer.OrdinalIgnoreCase),
            FashionSort.Id=>rows.OrderBy(e=>e.Id),
            FashionSort.PatchDescending=>rows.OrderByDescending(e=>PatchRank(e.Patch)).ThenBy(e=>e.Name,StringComparer.OrdinalIgnoreCase),
            _=>rows.OrderBy(e=>e.Name,StringComparer.OrdinalIgnoreCase)
        }).ThenBy(e=>e.Id).ToArray();
    }
    private static decimal PatchRank(string? value) => decimal.TryParse(value,System.Globalization.NumberStyles.AllowDecimalPoint,
        System.Globalization.CultureInfo.InvariantCulture,out var n)?n:-1;
    public static (int Unlocked,int Locked,int Unknown) Completion(IEnumerable<FashionEntry> rows,FashionCharacter character)
    {
        int yes=0,no=0,unknown=0;
        foreach(var row in rows) switch(character.Ownership(row.Id)){case FashionOwnership.Unlocked:yes++;break;case FashionOwnership.Locked:no++;break;default:unknown++;break;}
        return(yes,no,unknown);
    }
}
public sealed class FashionObservation
{
    private ulong owner;
    public FashionCharacter Snapshot {get;private set;}=FashionCharacter.Unknown;
    public void Clear(){owner=0;Snapshot=FashionCharacter.Unknown;}
    public void Observe(ulong session,bool ready,Func<FashionCharacter> read)
    {
        if(!ready||session==0){Clear();return;}
        if(owner!=session){Clear();owner=session;}
        Snapshot=FashionCharacter.Unknown;
        try{Snapshot=read();}catch{Snapshot=FashionCharacter.Unknown;}
    }
}
