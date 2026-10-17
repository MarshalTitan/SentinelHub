using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
namespace SentinelHub.Mounts;

// Bounded, framework-thread sheet traversal. No files, remote requests or game actions.
public sealed class MountGameDataBuilder(IDataManager data)
{
    public MountCatalog? Result { get; private set; }
    public IEnumerator<int> Build()
    {
        var englishMounts = data.GetExcelSheet<Mount>(ClientLanguage.English);
        var englishItems = data.GetExcelSheet<Item>(ClientLanguage.English);
        var expansions = data.GetExcelSheet<ExVersion>().ToDictionary(x => (int)x.RowId, x => x.Name.ToString());
        var entries = new Dictionary<uint, MountEntry>();
        var sources = new Dictionary<uint, List<MountSource>>();
        var items = new Dictionary<uint, List<MountUnlockItem>>();
        var itemMounts = new Dictionary<uint, uint>();
        var trials = new HashSet<uint>();
        int work = 0;
        foreach (var row in data.GetExcelSheet<Mount>())
        {
            var name = row.Singular.ToString();
            if (MountPolicy.IsCollectionRow(row.RowId, name, row.Icon, row.ModelChara.RowId, row.Order))
            {
                var english = englishMounts.GetRowOrDefault(row.RowId)?.Singular.ToString() ?? "";
                var seed = SentinelMountMetadata.Find(english);
                var actions = new List<string>();
                if (row.MountAction.RowId != 0 && row.MountAction.IsValid)
                    foreach (var action in row.MountAction.Value.Action)
                        if (action.RowId != 0 && action.IsValid && !string.IsNullOrWhiteSpace(action.Value.Name.ToString()))
                            actions.Add(action.Value.Name.ToString());
                entries.Add(row.RowId, new(row.RowId, name, english, row.Icon, row.ExtraSeats + 1,
                    actions.Distinct().ToArray(), row.RideBGM.RowId, 0, seed?.Expansion,
                    MountKnowledge.Unknown, MountKnowledge.Unknown, [], []));
                sources[row.RowId] = [];
                items[row.RowId] = [];
                if (seed is not null)
                {
                    var reward = seed.Reward.Equals(english, StringComparison.OrdinalIgnoreCase);
                    sources[row.RowId].Add(new(reward ? MountCategory.Quest : MountCategory.TrialCollection,
                        reward ? "Collection quest: " + seed.Quest : "Expansion trial collection: " + seed.Reward,
                        "Sentinel metadata " + SentinelMountMetadata.Revision + "; official patch notes"));
                    if (!reward) trials.Add(row.RowId);
                }
            }
            if (++work % 64 == 0) yield return 1;
        }
        var ids = entries.Keys.ToHashSet();
        foreach (var item in data.GetExcelSheet<Item>())
        {
            if (item.ItemAction.RowId != 0 && item.ItemAction.IsValid)
            {
                var action = item.ItemAction.Value;
                var id = MountPolicy.UnlockMount(action.Action.RowId, action.Data[0], ids);
                if (id is { } mount)
                {
                    var marketable = !item.IsUntradable && item.ItemSearchCategory.RowId != 0;
                    items[mount].Add(new(item.RowId, item.Name.ToString(), marketable));
                    itemMounts.Add(item.RowId, mount);
                }
            }
            if (++work % 256 == 0) yield return 2;
        }
        foreach (var achievement in data.GetExcelSheet<Achievement>())
        {
            if (itemMounts.TryGetValue(achievement.Item.RowId, out var mount))
                sources[mount].Add(new(MountCategory.Achievement, "Achievement reward: " + achievement.Name.ToString(),
                    "Game data: Achievement #" + achievement.RowId));
            if (++work % 128 == 0) yield return 3;
        }
        foreach (var quest in data.GetExcelSheet<Quest>())
        {
            if (quest.ItemRewardType is 1 or 3 or 5)
                foreach (var item in quest.Reward)
                    if (itemMounts.TryGetValue(item.RowId, out var mount))
                        sources[mount].Add(new(MountCategory.Quest, "Quest reward: " + quest.Name.ToString(),
                            "Game data: Quest #" + quest.RowId));
            foreach (var item in quest.OptionalItemReward)
                if (itemMounts.TryGetValue(item.RowId, out var mount))
                    sources[mount].Add(new(MountCategory.Quest, "Optional quest reward: " + quest.Name.ToString(),
                        "Game data: Quest #" + quest.RowId));
            if (++work % 128 == 0) yield return 4;
        }
        var exchanges = new List<MountExchange>();
        foreach (var shop in data.GetExcelSheet<SpecialShop>())
        {
            foreach (var offer in shop.Item)
            {
                for (int r = 0; r < offer.Item.Count; r++)
                {
                    if (offer.ReceiveCount[r] == 0 || !itemMounts.TryGetValue(offer.Item[r].RowId, out var mount)) continue;
                    sources[mount].Add(new(MountCategory.Exchange,
                        "Exchange entry: " + (string.IsNullOrWhiteSpace(shop.Name.ToString()) ? "Special shop #" + shop.RowId : shop.Name.ToString()),
                        "Game data: SpecialShop #" + shop.RowId + "; conditions/availability not verified"));
                    var nonzeroCosts = 0;
                    for (int c = 0; c < offer.CurrencyCost.Count; c++) if (offer.CurrencyCost[c] != 0) nonzeroCosts++;
                    for (int c = 0; c < offer.ItemCost.Count; c++)
                    {
                        var tokenId = offer.ItemCost[c].RowId;
                        if (tokenId == 0 || !offer.ItemCost[c].IsValid) continue;
                        var token = offer.ItemCost[c].Value;
                        var english = englishItems.GetRowOrDefault(tokenId)?.Name.ToString() ?? "";
                        if (MountPolicy.SafeTokenOffer(trials.Contains(mount), shop.UseCurrencyType,
                            offer.ReceiveCount[r], offer.ReceiveHq[r], nonzeroCosts, offer.CostType[c], tokenId,
                            offer.CurrencyCost[c], token.IsUntradable, english))
                            exchanges.Add(new(mount, tokenId, token.Name.ToString(), offer.CurrencyCost[c], shop.RowId));
                    }
                }
            }
            if (++work % 4 == 0) yield return 5;
        }
        var bgmCounts = entries.Values.Where(e => e.BgmId != 0).GroupBy(e => e.BgmId).ToDictionary(g => g.Key, g => g.Count());
        var final = entries.Values.Select(e => e with {
            Items = items[e.Id].OrderBy(i => i.Id).ToArray(),
            Marketable = MountPolicy.MarketStatus(items[e.Id]),
            Sources = sources[e.Id].Distinct().OrderBy(s => s.Category).ThenBy(s => s.Description).ToArray(),
            BgmMountCount = bgmCounts.GetValueOrDefault(e.BgmId),
        }).OrderBy(e => e.Id).ToArray();
        var tracks = SentinelMountMetadata.Tracks.Select(t => MountPolicy.ResolveTrack(t, final)).ToArray();
        var unresolved = tracks.Sum(t => t.Members.Count(m => m.MountId is null) + (t.RewardId is null ? 1 : 0));
        Result = new(final, tracks, exchanges.Distinct().OrderBy(e => e.MountId).ThenBy(e => e.ItemId).ToArray(), expansions, unresolved);
    }
}
