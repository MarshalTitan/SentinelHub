using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using SentinelHub.Configuration;
using SentinelHub.Mounts;
namespace SentinelHub.UI;

public sealed class MountCollectionPanel
{
    private readonly HubSettings settings;
    private readonly Func<MountCollectionView> view;
    private readonly Action save, refresh, retry;
    private readonly Action<uint, Vector2> icon;
    public MountFilter Filter { get; } = new();
    private uint? selected;
    private int page;
    private const int PageSize = 20;
    public MountCollectionPanel(HubSettings settings, Func<MountCollectionView> view, Action save,
        Action refresh, Action retry, Action<uint, Vector2>? icon = null)
    {
        this.settings = settings; this.view = view; this.save = save; this.refresh = refresh; this.retry = retry;
        this.icon = icon ?? ((_, size) => ImGui.Dummy(size));
    }
    public void Select(uint? mount) { selected = mount; refresh(); }
    private static bool FullButton(string label) => ImGui.Button(label, new Vector2(-1, 28 * ImGuiHelpers.GlobalScale));
    private static bool Combo(string id, ref int value, string[] names)
    {
        ImGui.SetNextItemWidth(-1);
        return ImGui.Combo(id, ref value, names, names.Length);
    }
    public void Draw()
    {
        ImGui.TextWrapped("Passive collection browser for your current character.");
        var enabled = settings.MountContextMenuEnabled;
        if (ImGui.Checkbox("Include mounts in player identification", ref enabled))
        { settings.MountContextMenuEnabled = enabled; save(); }
        var v = view();
        if (v.State is MountLoadState.NotLoaded or MountLoadState.Loading)
        {
            ImGui.TextWrapped("Reading the local mount database...");
            return;
        }
        if (v.State == MountLoadState.Unavailable)
        {
            ImGui.TextWrapped("Mount data is unavailable. Other Hub pages remain available.");
            if (FullButton("Retry mount database")) retry();
            return;
        }
        if (!v.Character.Available) ImGui.TextWrapped("Character collection unavailable. Log in and finish zoning to read unlocks.");
        if (FullButton("Refresh character counts")) refresh();
        if (selected is { } id)
        {
            if (FullButton("Back to mount database")) selected = null;
            var mount = v.Catalog.Entries.FirstOrDefault(e => e.Id == id);
            if (mount is null) ImGui.TextWrapped("This mount is not in the current collection database (ID " + id + ").");
            else Details(mount, v);
            return;
        }
        var all = MountPolicy.Completion(v.Catalog.Entries.Select(e => e.Id), v.Character);
        ImGui.TextWrapped("Overall completion: " + all.Label);
        var coverage = v.Catalog.Entries.Count(e => e.Sources.Length > 0);
        ImGui.TextWrapped($"Acquisition source coverage: {coverage} / {v.Catalog.Entries.Length}. Sources may have conditions or refer to past events.");
        if (v.Catalog.MetadataDate is not null)
            ImGui.TextWrapped($"FFXIV Collect snapshot: {v.Catalog.MetadataDate}; {v.Catalog.MetadataMatched} mount identities matched. Sources and release patches are community-maintained.");
        if (v.Catalog.MetadataRejected > 0)
            ImGui.TextWrapped($"{v.Catalog.MetadataRejected} snapshot identities were not matched to this client and were not applied.");
        var mapped = v.Catalog.Entries.Count(e => e.Expansion is not null);
        ImGui.TextWrapped($"Expansion metadata: {mapped} / {v.Catalog.Entries.Length} mounts mapped. Remaining mounts stay in Unknown expansion.");
        if (ImGui.CollapsingHeader("Expansion completion (mapped mounts)"))
        {
            foreach (var expansion in v.Catalog.ExpansionNames.OrderBy(e => e.Key))
            {
                var counts = MountPolicy.Completion(v.Catalog.Entries.Where(e => e.Expansion == expansion.Key).Select(e => e.Id), v.Character);
                ImGui.TextWrapped(expansion.Value + ": " + counts.Label + " mapped");
            }
            var unknown = MountPolicy.Completion(v.Catalog.Entries.Where(e => e.Expansion is null).Select(e => e.Id), v.Character);
            ImGui.TextWrapped("Unknown expansion: " + unknown.Label);
        }
        if (ImGui.CollapsingHeader("Expansion / meta-mount collection tracks"))
            foreach (var track in v.Catalog.Tracks) Track(track, v);
        ImGui.Separator(); ImGui.Spacing();
        var dirty = false;
        ImGui.TextWrapped("Search mounts, unlock items or sources");
        ImGui.SetNextItemWidth(-1);
        dirty |= ImGui.InputText("##mount-search", ref Filter.Search, 129);
        ImGui.TextWrapped("Collection state");
        dirty |= Combo("##mount-ownership", ref Filter.Ownership, ["All states", "Missing / locked", "Unlocked", "Unknown"]);
        var expansionIds = new[] { -2, -1 }.Concat(v.Catalog.ExpansionNames.Keys.Order()).ToArray();
        var expansionNames = new[] { "All expansions", "Unknown expansion" }
            .Concat(expansionIds.Skip(2).Select(i => v.Catalog.ExpansionNames[i])).ToArray();
        var expansionIndex = Math.Max(0, Array.IndexOf(expansionIds, Filter.Expansion));
        ImGui.TextWrapped("Release expansion (metadata)");
        if (Combo("##mount-expansion", ref expansionIndex, expansionNames))
        { Filter.Expansion = expansionIds[expansionIndex]; dirty = true; }
        ImGui.TextWrapped("Current availability (separate from source)");
        dirty |= Combo("##mount-obtainable", ref Filter.Obtainable, ["All / include unknown", "Verified obtainable", "Verified unavailable", "Unknown"]);
        ImGui.TextWrapped("Market-board eligibility of known unlock items");
        dirty |= Combo("##mount-market", ref Filter.Marketable, ["All / include unknown", "Marketable", "Not marketable", "Unknown"]);
        var categories = Enum.GetValues<MountCategory>();
        var category = Filter.Category is { } c ? Array.IndexOf(categories, c) + 1 : 0;
        ImGui.TextWrapped("Acquisition category");
        if (Combo("##mount-category", ref category, new[] { "Any category" }.Concat(categories.Select(c => CategoryName(c))).ToArray()))
        { Filter.Category = category == 0 ? null : categories[category - 1]; dirty = true; }
        var sort = (int)Filter.Sort;
        ImGui.TextWrapped("Sort");
        if (Combo("##mount-sort", ref sort, ["Name A-Z", "Name Z-A", "Mount ID", "Seats (most first)"]))
        { Filter.Sort = (MountSort)sort; dirty = true; }
        if (FullButton("Clear mount filters")) { Filter.Reset(); dirty = true; }
        if (dirty) page = 0;
        ImGui.TextWrapped("Unknown metadata is not treated as unavailable or locked. A shop/reward entry alone does not verify current obtainability. Marketable means game-item eligibility, not a live listing.");
        var rows = MountPolicy.Filter(v.Catalog, v.Character, Filter);
        var pages = Math.Max(1, (rows.Length + PageSize - 1) / PageSize);
        page = Math.Clamp(page, 0, pages - 1);
        ImGui.TextWrapped($"{rows.Length} matching mounts; page {page + 1} / {pages}");
        ImGui.BeginDisabled(page == 0);
        if (FullButton("Previous mounts")) page--;
        ImGui.EndDisabled();
        ImGui.BeginDisabled(page >= pages - 1);
        if (FullButton("Next mounts")) page++;
        ImGui.EndDisabled();
        if (rows.Length == 0) ImGui.TextWrapped("No mounts match these filters.");
        foreach (var mount in rows.Skip(page * PageSize).Take(PageSize)) Row(mount, v.Character);
    }
    private void Row(MountEntry mount, MountCharacterSnapshot character)
    {
        ImGui.PushID((int)mount.Id);
        if (ImGui.BeginTable("mount-row", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoSavedSettings))
        {
            ImGui.TableSetupColumn("Icon", ImGuiTableColumnFlags.WidthFixed, 36 * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("Mount", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            icon(mount.Icon, new Vector2(32) * ImGuiHelpers.GlobalScale);
            ImGui.TableNextColumn();
            ImGui.TextWrapped(mount.Name);
            ImGui.TextWrapped(character.Ownership(mount.Id).ToString());
            if (ImGui.SmallButton("View details")) selected = mount.Id;
            ImGui.EndTable();
        }
        ImGui.Separator(); ImGui.PopID();
    }
    private void Track(MountTrack track, MountCollectionView v)
    {
        ImGui.PushID(track.Seed.Id);
        if (ImGui.TreeNode(track.Seed.Reward))
        {
            ImGui.TextWrapped("Prerequisite mounts: " + MountPolicy.TrackCompletion(track, v.Character).Label);
            ImGui.TextWrapped("Collection quest: " + track.Seed.Quest + ". Other quest/story requirements are not checked.");
            ImGui.TextWrapped("Reward: " + (track.RewardId is { } id ? v.Character.Ownership(id).ToString() : "Unknown (not resolved in this game data)"));
            foreach (var member in track.Members)
            {
                var row = v.Catalog.Entries.FirstOrDefault(e => e.Id == member.MountId);
                ImGui.TextWrapped((row?.Name ?? member.EnglishName) + ": " +
                    (member.MountId is { } m ? v.Character.Ownership(m).ToString() : "Unknown (metadata not resolved)"));
                if (member.MountId is { } mount)
                {
                    if (ImGui.SmallButton("View##" + mount)) selected = mount;
                    Totems(mount, v);
                }
            }
            if (track.RewardId is { } reward && FullButton("View reward mount")) selected = reward;
            ImGui.TreePop();
        }
        ImGui.PopID();
    }
    private void Details(MountEntry m, MountCollectionView v)
    {
        icon(m.Icon, new Vector2(64) * ImGuiHelpers.GlobalScale);
        ImGui.TextWrapped(m.Name);
        ImGui.TextWrapped("Mount ID: " + m.Id + " | " + v.Character.Ownership(m.Id));
        if (m.ReleasePatch is not null) ImGui.TextWrapped("Introduced in patch: " + m.ReleasePatch + " (FFXIV Collect)");
        ImGui.TextWrapped("Expansion: " + (m.Expansion is { } expansion ? v.Catalog.ExpansionNames.GetValueOrDefault(expansion, "Expansion #" + expansion) : "Unknown"));
        ImGui.TextWrapped("Seats: " + m.Seats);
        ImGui.TextWrapped(m.Actions.Length == 0 ? "Mount actions: none listed in game data" : "Mount actions: " + string.Join(", ", m.Actions));
        ImGui.TextWrapped(m.BgmId == 0 ? "Mount BGM: no dedicated override in game data" :
            $"Mount BGM: game track #{m.BgmId}; used by {m.BgmMountCount} mounts in this database.");
        if (m.BgmId != 0) ImGui.TextWrapped("Unique among these mounts: " + (m.BgmMountCount == 1 ? "Yes" : "No") + ". This does not mean exclusive to mounts across the whole game.");
        ImGui.TextWrapped("Current availability: " + (m.Obtainable == MountKnowledge.Unknown ? "Not checked" : m.Obtainable.ToString()) + ". Source records do not certify active events, vendor access or regional store availability.");
        ImGui.TextWrapped("Marketable unlock item: " + m.Marketable + " (eligibility, not live market listings)");
        ImGui.Separator();
        ImGui.TextWrapped("Where to obtain");
        ImGui.TextWrapped("Official records and community reports appear first. Additional game entries can include conditional or historical rewards.");
        if (m.Sources.Length == 0) ImGui.TextWrapped("Unknown. No verified acquisition metadata is available for this mount.");
        for (var i = 0; i < m.Sources.Length; i++)
        {
            var source = m.Sources[i];
            ImGui.PushID(i);
            ImGui.TextWrapped(CategoryName(source.Category) + ": " + source.Description);
            ImGui.TextWrapped(source.Evidence);
            if (MountAcquisitionMetadata.SafeSourceUrl(source.Url))
            {
                if (ImGui.SmallButton("Open source page")) Dalamud.Utility.Util.OpenLink(source.Url!);
                ImGui.SameLine();
                if (ImGui.SmallButton("Copy URL")) ImGui.SetClipboardText(source.Url!);
            }
            ImGui.Spacing();
            ImGui.PopID();
        }
        ImGui.Separator();
        ImGui.TextWrapped("Known unlock items");
        if (m.Items.Length == 0) ImGui.TextWrapped("No direct mount-unlock item relationship found.");
        foreach (var item in m.Items) ImGui.TextWrapped(item.Name + " (#" + item.Id + ") - " + (item.Marketable ? "marketable" : "not marketable"));
        Totems(m.Id, v);
        var track = v.Catalog.Tracks.FirstOrDefault(t => t.RewardId == m.Id || t.Members.Any(x => x.MountId == m.Id));
        if (track is not null) Track(track, v);
    }
    private static void Totems(uint mount, MountCollectionView v)
    {
        foreach (var exchange in v.Catalog.Exchanges.Where(e => e.MountId == mount))
        {
            if (!v.Character.Available || !v.Character.InventoryAvailable || !v.Character.Counts.TryGetValue(exchange.ItemId, out var count))
                ImGui.TextWrapped(exchange.ItemName + ": count unavailable / " + exchange.Required + " required");
            else
            {
                ImGui.TextWrapped(exchange.ItemName + ": " + count + " / " + exchange.Required + " (main bags only)");
                ImGui.ProgressBar((float)MountPolicy.Progress(count, exchange.Required), new(-1, 0));
            }
        }
        if (v.Catalog.Exchanges.Any(e => e.MountId == mount))
            ImGui.TextWrapped("Exchange entry only; vendor unlock/availability is not checked. Retainers, saddlebag and other characters are excluded.");
    }
    private static string CategoryName(MountCategory c) => c switch
    {
        MountCategory.TrialCollection => "Trial collection", MountCategory.GilVendor => "Gil vendor",
        MountCategory.OnlineStore => "Online Store", MountCategory.Premium => "Collector / promotion / other paid",
        MountCategory.DeepDungeon => "Deep dungeon", MountCategory.VariantCriterion => "Variant / criterion dungeon",
        MountCategory.GoldSaucer => "Gold Saucer", MountCategory.Pvp => "PvP", MountCategory.Fate => "FATE",
        MountCategory.WondrousTails => "Wondrous Tails", MountCategory.OccultCrescent => "Occult Crescent",
        MountCategory.CosmicExploration => "Cosmic Exploration", MountCategory.IslandSanctuary => "Island Sanctuary",
        MountCategory.TreasureHunt => "Treasure hunt", MountCategory.ChaoticRaid => "Chaotic raid",
        _ => c.ToString(),
    };
}
