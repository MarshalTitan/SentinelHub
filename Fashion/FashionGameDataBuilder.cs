using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
namespace SentinelHub.Fashion;
// Framework-thread, bounded sheet traversal. Native calls/game actions/network are absent.
public sealed class FashionGameDataBuilder(IDataManager data)
{
    public FashionCatalog? Result {get;private set;}
    public IEnumerator<int> Build()
    {
        var english=data.GetExcelSheet<Ornament>(ClientLanguage.English);
        var entries=new Dictionary<uint,FashionEntry>();
        var sources=new Dictionary<uint,List<FashionSource>>();
        var items=new Dictionary<uint,List<FashionItem>>();
        var itemOrnaments=new Dictionary<uint,uint>();
        var metadataMatched=0;
        int work=0;
        foreach(var row in data.GetExcelSheet<Ornament>())
        {
            var name=row.Singular.ToString();
            if(FashionPolicy.Candidate(row.RowId,name,row.Icon,row.Model,row.Order))
            {
                var en=english.GetRowOrDefault(row.RowId)?.Singular.ToString()??"";
                var fact=FashionMetadata.Find(row.RowId,en);
                var correction=FashionMetadata.Correction(row.RowId,en);
                if(fact is not null)metadataMatched++;
                entries.Add(row.RowId,new(row.RowId,name,en,row.Icon,row.Model,row.Order,fact?.Patch,FashionMetadata.Expansion(fact?.Patch),
                    [],[],FashionKnowledge.Unknown,FashionKnowledge.Unknown,
                    fact?.Sources.Any(s=>s.Type=="Premium"&&s.Text=="Online Store")==true?FashionKnowledge.Yes:FashionKnowledge.Unknown,
                    FashionMetadata.Availability(correction,DateTimeOffset.UtcNow),correction?.AvailabilityEvidence??"Not verified",correction?.Note)
                    {AvailabilityExpiresUtc=DateTimeOffset.TryParse(correction?.ExpiresUtc,out var expiry)?expiry:null});
                sources[row.RowId]=fact is null?[]:[..FashionMetadata.Sources(fact)];
                items[row.RowId]=[];
            }
            if(++work%64==0)yield return 1;
        }
        var ids=entries.Keys.ToHashSet();
        foreach(var item in data.GetExcelSheet<Item>())
        {
            if(item.ItemAction.RowId!=0&&item.ItemAction.IsValid
                &&FashionPolicy.Unlock(item.ItemAction.Value.Action.RowId,item.ItemAction.Value.Data[0],ids) is {} id)
            {
                items[id].Add(new(item.RowId,item.Name.ToString(),!item.IsUntradable,!item.IsUntradable&&item.ItemSearchCategory.RowId!=0));
                itemOrnaments.Add(item.RowId,id);
            }
            if(++work%256==0)yield return 2;
        }
        foreach (var achievement in data.GetExcelSheet<Achievement>())
        {
            if (itemOrnaments.TryGetValue(achievement.Item.RowId, out var ornament))
                sources[ornament].Add(new("Achievement", "Achievement reward: " + achievement.Name.ToString(),
                    "Game data: Achievement #" + achievement.RowId));
            if (++work % 128 == 0) yield return 3;
        }
        foreach (var quest in data.GetExcelSheet<Quest>())
        {
            if (quest.ItemRewardType is 1 or 3 or 5)
                foreach (var item in quest.Reward)
                    if (itemOrnaments.TryGetValue(item.RowId, out var ornament))
                        sources[ornament].Add(new("Quest", "Quest reward: " + quest.Name.ToString(),
                            "Game data: Quest #" + quest.RowId));
            foreach (var item in quest.OptionalItemReward)
                if (itemOrnaments.TryGetValue(item.RowId, out var ornament))
                    sources[ornament].Add(new("Quest", "Optional quest reward: " + quest.Name.ToString(),
                        "Game data: Quest #" + quest.RowId));
            if (++work % 128 == 0) yield return 4;
        }
        var gilShops = data.GetExcelSheet<GilShop>();
        foreach (var listing in data.GetSubrowExcelSheet<GilShopItem>().Flatten())
        {
            if (itemOrnaments.TryGetValue(listing.Item.RowId, out var ornament) && listing.Item.IsValid)
            {
                var shop = gilShops.GetRowOrDefault(listing.RowId);
                var requirements = new List<string>();
                foreach (var quest in listing.QuestRequired)
                    if (quest.RowId != 0) requirements.Add(quest.IsValid ? "quest " + quest.Value.Name.ToString() : "quest #" + quest.RowId);
                if (listing.AchievementRequired.RowId != 0)
                    requirements.Add(listing.AchievementRequired.IsValid ? "achievement " + listing.AchievementRequired.Value.Name.ToString() : "achievement #" + listing.AchievementRequired.RowId);
                if (listing.StateRequired != 0) requirements.Add("additional game state #" + listing.StateRequired);
                if (shop is { } gilShop)
                {
                    if (gilShop.Quest.RowId != 0) requirements.Add(gilShop.Quest.IsValid ? "quest " + gilShop.Quest.Value.Name.ToString() : "quest #" + gilShop.Quest.RowId);
                    if (gilShop.FestivalId != 0) requirements.Add("festival #" + gilShop.FestivalId + " phase " + gilShop.FestivalPhase);
                }
                sources[ornament].Add(FashionMetadata.GilSource(listing.RowId, listing.SubrowId,
                    shop?.Name.ToString() ?? "", listing.Item.Value.Name.ToString(), listing.Item.Value.PriceMid, listing.IsHQ, requirements.Distinct().ToArray()));
            }
            if (++work % 192 == 0) yield return 5;
        }
        
        foreach (var shop in data.GetExcelSheet<SpecialShop>())
        {
            foreach (var offer in shop.Item)
            {
                for (int r = 0; r < offer.ReceiveItems.Count; r++)
                {
                    if (offer.ReceiveItems[r].ReceiveCount == 0 || !itemOrnaments.TryGetValue(offer.ReceiveItems[r].Item.RowId, out var ornament)) continue;
                    var costs = new List<string>();
                    foreach (var cost in offer.ItemCosts)
                    {
                        if (cost.CurrencyCost == 0) continue;
                        var direct = FashionMetadata.DirectCost(shop.UseCurrencyType, cost.ItemCost.RowId, cost.CostType, cost.CollectabilityCost)
                            && cost.ItemCost.IsValid;
                        costs.Add(cost.CurrencyCost.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " x "
                            + (direct ? cost.ItemCost.Value.Name.ToString() : "unresolved currency/condition #" + cost.ItemCost.RowId));
                    }
                    sources[ornament].Add(new("Exchange",
                        "Exchange: " + (string.IsNullOrWhiteSpace(shop.Name.ToString()) ? "Special shop #" + shop.RowId : shop.Name.ToString())
                        + "; receive " + offer.ReceiveItems[r].ReceiveCount + " x " + offer.ReceiveItems[r].Item.Value.Name.ToString()
                        + (costs.Count > 0 ? "; cost " + string.Join(" + ", costs) : "; cost not resolved"),
                        "Game data: SpecialShop #" + shop.RowId + "; unlock/availability conditions are not checked"));

                }
            }
            if (++work % 4 == 0) yield return 5;
        }

        var final=entries.Values.Select(e=>e with{Items=items[e.Id].OrderBy(i=>i.Id).ToArray(),
            Sources=sources[e.Id].Distinct().ToArray(),Tradeable=FashionPolicy.ItemStatus(items[e.Id].ToArray(),false),
            Marketable=FashionPolicy.ItemStatus(items[e.Id].ToArray(),true)}).ToArray();
        var expansions=data.GetExcelSheet<ExVersion>().ToDictionary(e=>(int)e.RowId,e=>string.IsNullOrWhiteSpace(e.Name.ToString())?e.RowId==0?"A Realm Reborn":"Expansion #"+e.RowId:e.Name.ToString());
        Result=new(FashionPolicy.Validate(final),expansions,metadataMatched,FashionMetadata.Snapshot.Accessories.Length-metadataMatched,FashionMetadata.Snapshot.RetrievedUtc[..10]);
    }
}
