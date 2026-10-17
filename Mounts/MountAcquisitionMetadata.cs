using System.Globalization;
using System.Text.Json;
namespace SentinelHub.Mounts;

public sealed record AcquisitionFact(string Type, string Text);
public sealed record AcquisitionMount(uint Id, string EnglishName, string Patch, AcquisitionFact[] Sources);
public sealed record AcquisitionSnapshot(string Provider, string Endpoint, string RetrievedUtc, string ResponseSha256,
    string Permission, AcquisitionMount[] Mounts);
public sealed record StoreMount(uint Id, string EnglishName, string ProductName, string Url);
public sealed record StoreSnapshot(string CheckedUtc, string Method, StoreMount[] Mounts);

// Frozen public acquisition facts only. This class never connects to the network.
public static class MountAcquisitionMetadata
{
    public static readonly AcquisitionSnapshot Snapshot = Read<AcquisitionSnapshot>("SentinelHub.MountSources.json");
    public static readonly StoreSnapshot Store = Read<StoreSnapshot>("SentinelHub.MountStoreLinks.json");
    private static readonly Dictionary<uint, AcquisitionMount> ById = Snapshot.Mounts.ToDictionary(m => m.Id);
    private static readonly Dictionary<uint, StoreMount> StoreById = Store.Mounts.ToDictionary(m => m.Id);
    private static T Read<T>(string resource)
    {
        using var stream = typeof(MountAcquisitionMetadata).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidDataException("Missing mount metadata resource");
        return JsonSerializer.Deserialize<T>(stream) ?? throw new InvalidDataException("Invalid mount metadata resource");
    }
    public static AcquisitionMount? Find(uint id, string english) =>
        ById.TryGetValue(id, out var row) && IdentityMatches(row.Id, row.EnglishName, id, english) ? row : null;
    public static StoreMount? FindStore(uint id, string english) =>
        StoreById.TryGetValue(id, out var row) && IdentityMatches(row.Id, row.EnglishName, id, english) ? row : null;
    public static bool IdentityMatches(uint expectedId, string expectedName, uint id, string english) =>
        id != 0 && id == expectedId && !string.IsNullOrWhiteSpace(english)
        && expectedName.Equals(english, StringComparison.OrdinalIgnoreCase);
    public static int? Expansion(string patch)
    {
        var parts = patch.Split('.');
        if (parts.Length != 2) return null;
        var minor = parts[1];
        if (minor.EndsWith('a') || minor.EndsWith('b')) minor = minor[..^1];
        return minor.Length is > 0 and <= 2 && minor.All(char.IsAsciiDigit)
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major) && major is >= 2 and <= 7
            ? major - 2 : null;
    }
    public static MountCategory Category(AcquisitionFact fact) => fact.Type switch
    {
        "Quest" => MountCategory.Quest, "Achievement" => MountCategory.Achievement,
        "Purchase" => MountCategory.GilVendor,
        "Premium" => fact.Text.Equals("Online Store", StringComparison.OrdinalIgnoreCase) ? MountCategory.OnlineStore : MountCategory.Premium,
        "Trial" => MountCategory.Trial, "Raid" => MountCategory.Raid, "Dungeon" => MountCategory.Dungeon,
        "Deep Dungeon" => MountCategory.DeepDungeon, "V&C Dungeon" => MountCategory.VariantCriterion,
        "Tribal" => MountCategory.Tribal, "Gold Saucer" => MountCategory.GoldSaucer, "PvP" => MountCategory.Pvp,
        "Event" => MountCategory.Event, "Hunts" => MountCategory.Hunts, "Skybuilders" => MountCategory.Skybuilders,
        "Voyages" => MountCategory.Voyages, "FATE" => MountCategory.Fate, "Wondrous Tails" => MountCategory.WondrousTails,
        "Crafting" => MountCategory.Crafting, "Gathering" => MountCategory.Gathering, "Eureka" => MountCategory.Eureka,
        "Bozja" => MountCategory.Bozja, "Occult Crescent" => MountCategory.OccultCrescent,
        "Cosmic Exploration" => MountCategory.CosmicExploration, "Island Sanctuary" => MountCategory.IslandSanctuary,
        "Treasure Hunt" => MountCategory.TreasureHunt, "Chaotic Raid" => MountCategory.ChaoticRaid,
        _ => MountCategory.Other,
    };
    public static MountSource[] Sources(AcquisitionMount row)
    {
        var store = FindStore(row.Id, row.EnglishName);
        return row.Sources.Select(f => {
            var category = Category(f);
            var text = category == MountCategory.OnlineStore
                ? "Online Store purchase: " + (store?.ProductName ?? row.EnglishName) + ". Check regional price and availability."
                : f.Text;
            if (category == MountCategory.Event) text += " (event source; may be historical or limited)";
            return new MountSource(category, text, "Community source: FFXIV Collect; snapshot " + Snapshot.RetrievedUtc[..10],
                category == MountCategory.OnlineStore ? store?.Url ?? "https://store.finalfantasyxiv.com/ffxivstore/en-us/category/11"
                    : "https://ffxivcollect.com/mounts/" + row.Id, true);
        }).ToArray();
    }
    // Independently recorded factual vendor lookup, checked against the official Eorzea Database.
    public static MountSource? OfficialVendor(uint id, string english) =>
        IdentityMatches(317, "Chrysomallos", id, english) || IdentityMatches(252, "Gilded Mikoshi", id, english)
        || IdentityMatches(316, "Magitek Avenger G1", id, english)
        ? new(MountCategory.GilVendor, "Edelina (Luxury Trader), Mor Dhona (X:22.1, Y:4.8): buy the unlock item for 50,000,000 gil.",
            "Sentinel factual record; official Eorzea Database checked 2026-10-17. Vendor conditions are not checked.",
            "https://eu.finalfantasyxiv.com/lodestone/playguide/db/shop/7fa3c777312/") : null;
    public static MountSource GilSource(uint shopId, ushort subrow, string shopName, string itemName, long price, bool hq, string[] requirements) =>
        new(MountCategory.GilVendor, "Gil shop: " + (string.IsNullOrWhiteSpace(shopName) ? "#" + shopId : shopName)
            + "; " + itemName + (hq ? " (HQ; price not resolved)" : price > 0 ? "; base price " + price.ToString("N0", CultureInfo.InvariantCulture) + " gil" : "; price unknown")
            + (requirements.Length > 0 ? "; requires " + string.Join(", ", requirements) : ""),
            "Game data: GilShop #" + shopId + ", GilShopItem subrow " + subrow + "; unlock/reputation/festival conditions may apply.");
    public static bool SafeSourceUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
        && uri.UserInfo.Length == 0 && uri.IsDefaultPort
        && (uri.Host == "ffxivcollect.com" && uri.AbsolutePath.StartsWith("/mounts/", StringComparison.Ordinal)
            || uri.Host == "store.finalfantasyxiv.com" && uri.AbsolutePath.StartsWith("/ffxivstore/", StringComparison.Ordinal)
            || uri.Host == "eu.finalfantasyxiv.com" && uri.AbsolutePath.StartsWith("/lodestone/playguide/db/", StringComparison.Ordinal));
    public static bool DirectCost(byte mode, uint itemId, uint costType, long collectability) =>
        mode is 0 or 4 or 8 or 16 && itemId >= 8 && costType == 0 && collectability == 0;
}
