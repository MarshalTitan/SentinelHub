using Dalamud.Plugin.Services;
using Dalamud.Game;
using Lumina.Excel.Sheets;
namespace SentinelHub.Inspection;

// Local identity/unlock rows plus identity-gated, attributed source facts; current availability remains unknown.
public sealed class InspectionGameData(IDataManager data,IUnlockState unlocks,Func<bool> ownReady)
{
    private readonly Dictionary<(InspectionKind,uint),InspectionEntry> entries=new();
    private readonly Dictionary<uint,uint> modes=new();
    private IEnumerator<int>? build;
    private bool attempted;
    public bool Loaded {get;private set;}
    public bool Failed {get;private set;}
    public void Tick(bool requested)
    {
        if(!requested||Loaded||Failed)return;
        try{if(!attempted){attempted=true;build=Build();}if(build is not null&&!build.MoveNext()){build.Dispose();build=null;Loaded=true;}}
        catch{build?.Dispose();build=null;entries.Clear();modes.Clear();Failed=true;}
    }
    public InspectionEntry? Find(InspectionKind kind,uint id)
    {
        if(kind==InspectionKind.Mount){var r=data.GetExcelSheet<Mount>().GetRowOrDefault(id);return r is {} row&&!string.IsNullOrWhiteSpace(row.Singular.ToString())?new(kind,id,row.Singular.ToString(),row.Icon,"",null,[],"See Mount Collection"):null;}
        if(kind==InspectionKind.Fashion){var r=data.GetExcelSheet<Ornament>().GetRowOrDefault(id);return r is {} row&&SentinelHub.Fashion.FashionPolicy.Candidate(row.RowId,row.Singular.ToString(),row.Icon,row.Model,row.Order)?new(kind,id,row.Singular.ToString(),row.Icon,"",null,[],"See Fashion Accessories"):null;}
        if(!entries.TryGetValue((kind,id),out var entry))return null;
        bool? owned=null;
        if(ownReady())try{
            if(kind==InspectionKind.Emote&&data.GetExcelSheet<Emote>().GetRowOrDefault(id) is {} e)owned=unlocks.IsEmoteUnlocked(e);
        }catch{owned=null;}
        return entry with{Owned=owned};
    }
    public uint? EmoteMode(uint id)=>modes.TryGetValue(id,out var mode)?mode:null;
    private IEnumerator<int> Build()
    {
        var links=new Dictionary<uint,List<(InspectionKind,uint)>>();
        var itemLinks=new Dictionary<uint,List<(InspectionKind,uint)>>();
        var sources=new Dictionary<(InspectionKind,uint),List<string>>();int work=0;
        void AddLink(uint key,(InspectionKind,uint) value){if(key==0)return;if(!links.TryGetValue(key,out var list))links[key]=list=[];list.Add(value);}
        var englishEmotes=data.GetExcelSheet<Emote>(ClientLanguage.English);
        foreach(var e in data.GetExcelSheet<Emote>()){
            var name=e.Name.ToString();var command=e.TextCommand.IsValid?e.TextCommand.Value.Command.ToString():"";
            if(e.RowId>0&&e.RowId<=ushort.MaxValue&&!string.IsNullOrWhiteSpace(name)&&e.Icon>0&&command.StartsWith('/')&&command.Length<=80){
                var key=(InspectionKind.Emote,e.RowId);entries.Add(key,new(key.Item1,key.RowId,name,e.Icon,command,null,[],"Unknown"));
                var fact=InspectionMetadata.Find(InspectionKind.Emote,e.RowId,englishEmotes.GetRowOrDefault(e.RowId)?.Name.ToString()??"");
                entries[key]=entries[key] with{Patch=InspectionMetadata.Patch(fact?.Patch),Expansion=InspectionMetadata.Expansion(fact?.Patch),Category=fact is not null&&fact.Sources.Length>0?string.Join(", ",fact.Sources.Select(InspectionMetadata.Category).Distinct().Take(6)):"Unknown"};
                modes.Add(e.RowId,e.EmoteMode.RowId);sources[key]=fact is null?[]:[..InspectionMetadata.Sources(InspectionKind.Emote,fact)];AddLink(e.UnlockLink,key);
            }
            if(++work%128==0)yield return 1;
        }
        foreach(var item in data.GetExcelSheet<Item>()){
            if(item.ItemAction.IsValid&&item.ItemAction.RowId!=0){
                var action=item.ItemAction.Value;List<(InspectionKind,uint)> targets=[];
                // Current game action 2633 maps an unlock-link item to its local emote.
                if(action.Action.RowId==2633&&links.TryGetValue(action.Data[0],out var emotes))targets.AddRange(emotes);
                if(targets.Count>0)itemLinks[item.RowId]=targets;
            }
            if(++work%256==0)yield return 2;
        }
        foreach(var a in data.GetExcelSheet<Achievement>()){
            if(itemLinks.TryGetValue(a.Item.RowId,out var targets))foreach(var key in targets)sources[key].Add("Achievement reward: "+a.Name.ToString()+" (game data; current access not verified)");
            if(++work%128==0)yield return 3;
        }
        foreach(var q in data.GetExcelSheet<Quest>()){
            if(q.ItemRewardType is 1 or 3 or 5)foreach(var reward in q.Reward)if(itemLinks.TryGetValue(reward.RowId,out var targets))foreach(var key in targets)sources[key].Add("Quest reward: "+q.Name.ToString()+" (game data)");
            foreach(var reward in q.OptionalItemReward)if(itemLinks.TryGetValue(reward.RowId,out var targets))foreach(var key in targets)sources[key].Add("Optional quest reward: "+q.Name.ToString()+" (game data)");
            if(++work%128==0)yield return 4;
        }
        var shops=data.GetExcelSheet<GilShop>();
        foreach(var item in data.GetSubrowExcelSheet<GilShopItem>().Flatten()){
            if(itemLinks.TryGetValue(item.Item.RowId,out var targets)&&item.Item.IsValid){
                var shop=shops.GetRowOrDefault(item.RowId);var price=item.IsHQ?"HQ price unresolved":item.Item.Value.PriceMid>0?item.Item.Value.PriceMid+" gil (NQ base price)":"price unknown";
                foreach(var key in targets)sources[key].Add("Gil shop: "+(shop?.Name.ToString()??("#"+item.RowId))+"; "+price+"; vendor unlock/quest/festival conditions must be checked in game");
            }
            if(++work%192==0)yield return 5;
        }
        foreach(var key in entries.Keys.ToArray()){
            var s=sources[key].Distinct(StringComparer.Ordinal).Take(16).ToArray();
            entries[key]=entries[key] with{Sources=s,Category=entries[key].Category=="Unknown"&&s.Length>0?"Game reward/shop evidence":entries[key].Category};
        }
    }
}
