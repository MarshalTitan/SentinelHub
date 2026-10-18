using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using SentinelHub.Configuration;
using SentinelHub.Fashion;
namespace SentinelHub.UI;
public sealed class FashionAccessoriesPanel
{
    private readonly HubSettings settings;
    private readonly Func<FashionView> view;
    private readonly Action save,refresh,retry;
    private readonly Action<uint,Vector2> icon;
    private uint? selected;
    private int page;
    public FashionFilter Filter {get;}=new();
    public FashionAccessoriesPanel(HubSettings settings,Func<FashionView> view,Action save,Action refresh,Action retry,Action<uint,Vector2>? icon=null)
    {this.settings=settings;this.view=view;this.save=save;this.refresh=refresh;this.retry=retry;this.icon=icon??((_,size)=>ImGui.Dummy(size));}
    public void Select(uint? id){selected=id;page=0;refresh();}
    private static bool Button(string label)=>ImGui.Button(label,new(-1,28*ImGuiHelpers.GlobalScale));
    private static bool Combo(string id,ref int value,string[] values){ImGui.SetNextItemWidth(-1);return ImGui.Combo(id,ref value,values,values.Length);}
    public void Draw()
    {
        ImGui.TextWrapped("Read-only accessory collection. Wings, parasols, packs and other ornaments; Facewear is separate.");
        var enabled=settings.FashionContextMenuEnabled;
        if(ImGui.Checkbox("Include fashion accessories in player identification",ref enabled)){settings.FashionContextMenuEnabled=enabled;save();}
        var v=view();
        if(v.State is FashionLoad.NotLoaded or FashionLoad.Loading){ImGui.TextWrapped("Reading the local accessory database...");return;}
        if(v.State==FashionLoad.Unavailable){ImGui.TextWrapped("Fashion accessory data unavailable. Other Hub pages remain available.");if(Button("Retry accessory database"))retry();return;}
        if(!v.Character.Available)ImGui.TextWrapped("Own unlock state unavailable. Log in and finish zoning; Unknown is not Locked.");
        if(Button("Refresh accessory unlocks"))refresh();
        if(selected is {} id){
            if(Button("Back to accessory database"))selected=null;
            var entry=v.Catalog.Entries.FirstOrDefault(e=>e.Id==id);
            if(entry is null)ImGui.TextWrapped("Accessory ID "+id+" is not in this client's collection catalog.");
            else Details(entry,v);
            return;
        }
        var counts=FashionPolicy.Completion(v.Catalog.Entries,v.Character);
        ImGui.TextWrapped($"Collection: {counts.Unlocked} unlocked, {counts.Locked} locked, {counts.Unknown} unknown / {v.Catalog.Entries.Length}");
        ImGui.TextWrapped($"Acquisition source coverage: {v.Catalog.Entries.Count(e=>e.Sources.Length>0)} / {v.Catalog.Entries.Length}.");
        if(v.Catalog.MetadataDate is {} date)ImGui.TextWrapped($"FFXIV Collect snapshot {date}; {v.Catalog.MetadataMatched} identities matched, {v.Catalog.MetadataUnmatched} not applied.");
        ImGui.TextWrapped("Four retired spectacle ornaments were converted to Facewear and are excluded. A catalog row does not establish current obtainability.");
        if(ImGui.CollapsingHeader("Expansion completion")){
            foreach(var group in v.Catalog.Entries.GroupBy(e=>e.Expansion).OrderBy(g=>g.Key??-1)){
                var count=FashionPolicy.Completion(group,v.Character);
                ImGui.TextWrapped((group.Key is {} x?v.Catalog.Expansions.GetValueOrDefault(x,"Expansion #"+x):"Unknown expansion")
                    +$": {count.Unlocked} unlocked / {group.Count()} ({count.Unknown} unknown)");
            }
        }
        ImGui.TextWrapped("Search accessories, items and sources");ImGui.SetNextItemWidth(-1);
        var dirty=ImGui.InputText("##fashion-search",ref Filter.Search,129);
        ImGui.TextWrapped("Own collection");dirty|=Combo("##fashion-own",ref Filter.Ownership,["All states","Missing / locked","Unlocked","Unknown"]);
        ImGui.TextWrapped("Sort");var sort=(int)Filter.Sort;
        if(Combo("##fashion-sort",ref sort,["Name A-Z","Name Z-A","Ornament ID","Patch (newest first)"])){Filter.Sort=(FashionSort)sort;dirty=true;}
        if(ImGui.CollapsingHeader("Source, expansion and availability filters",ImGuiTreeNodeFlags.DefaultOpen)){
            var categories=new[]{"Any category","Unknown"}.Concat(v.Catalog.Entries.SelectMany(e=>e.Sources).Select(s=>s.Category).Distinct().Order()).ToArray();
            var category=Filter.Category is null?0:Math.Max(0,Array.IndexOf(categories,Filter.Category));
            ImGui.TextWrapped("Acquisition category");
            if(Combo("##fashion-category",ref category,categories)){Filter.Category=category==0?null:categories[category];dirty=true;}
            var expansionIds=new[]{-2,-1}.Concat(v.Catalog.Expansions.Keys.Order()).ToArray();
            var ex=Math.Max(0,Array.IndexOf(expansionIds,Filter.Expansion));
            ImGui.TextWrapped("Release expansion (metadata)");
            if(Combo("##fashion-expansion",ref ex,new[]{"All expansions","Unknown"}.Concat(expansionIds.Skip(2).Select(x=>v.Catalog.Expansions[x])).ToArray())){Filter.Expansion=expansionIds[ex];dirty=true;}
            ImGui.TextWrapped("Current obtainability (dated verified evidence)");
            dirty|=Combo("##fashion-obtainable",ref Filter.Obtainable,["All / include unknown","Verified Yes","Verified No","Unknown"]);
            ImGui.TextWrapped("Market-board eligibility of known unlock items");
            dirty|=Combo("##fashion-market",ref Filter.Marketable,["All / include unknown","Marketable","Not marketable","Unknown"]);
            ImGui.TextWrapped("Recorded Online Store source (not current sale status)");
            dirty|=Combo("##fashion-cash",ref Filter.CashShop,["All / include unknown","Recorded Yes","Verified No","Unknown"]);
        }
        if(Button("Clear accessory filters")){Filter.Reset();dirty=true;}
        if(dirty)page=0;
        var rows=FashionPolicy.Filter(v.Catalog,v.Character,Filter);var pages=Math.Max(1,(rows.Length+19)/20);page=Math.Clamp(page,0,pages-1);
        ImGui.TextWrapped($"{rows.Length} matching accessories; page {page+1} / {pages}");
        ImGui.BeginDisabled(page==0);if(Button("Previous accessories"))page--;ImGui.EndDisabled();
        ImGui.BeginDisabled(page>=pages-1);if(Button("Next accessories"))page++;ImGui.EndDisabled();
        if(rows.Length==0)ImGui.TextWrapped("No accessories match these filters.");
        foreach(var row in rows.Skip(page*20).Take(20)){
            ImGui.PushID((int)row.Id);
            if(ImGui.BeginTable("fashion-row",2,ImGuiTableFlags.SizingFixedFit|ImGuiTableFlags.NoSavedSettings)){
                ImGui.TableSetupColumn("Icon",ImGuiTableColumnFlags.WidthFixed,36*ImGuiHelpers.GlobalScale);ImGui.TableSetupColumn("Accessory",ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableNextRow();ImGui.TableNextColumn();icon(row.Icon,new Vector2(32)*ImGuiHelpers.GlobalScale);ImGui.TableNextColumn();
                ImGui.TextWrapped(row.Name);ImGui.TextWrapped(v.Character.Ownership(row.Id).ToString());
                if(ImGui.SmallButton("View accessory"))selected=row.Id;
                ImGui.EndTable();
            }
            ImGui.Separator();ImGui.PopID();
        }
    }
    private void Details(FashionEntry e,FashionView v)
    {
        icon(e.Icon,new Vector2(64)*ImGuiHelpers.GlobalScale);
        ImGui.TextWrapped(e.Name);
        ImGui.TextWrapped("Ornament ID: "+e.Id+" | Own unlock: "+v.Character.Ownership(e.Id));
        ImGui.TextWrapped("Catalog patch: "+(e.Patch??"Unknown")+" (FFXIV Collect)");
        ImGui.TextWrapped("Expansion: "+(e.Expansion is {} x?v.Catalog.Expansions.GetValueOrDefault(x,"Expansion #"+x):"Unknown"));
        ImGui.TextWrapped("Tradeable unlock item: "+e.Tradeable+" | Market-board eligible: "+e.Marketable+". Eligibility does not mean a live listing.");
        ImGui.TextWrapped("Recorded Online Store source: "+e.CashShop+". No recorded source is not a verified No.");
        ImGui.TextWrapped("Currently obtainable: "+FashionPolicy.CurrentAvailability(e,DateTimeOffset.UtcNow)+". "+e.AvailabilityEvidence);
        if(e.AvailabilityExpiresUtc is {} expiry)ImGui.TextWrapped("Availability record expires: "+expiry.ToString("yyyy-MM-dd")+" UTC; stale records become Unknown.");
        if(e.Note is {} note)ImGui.TextWrapped(note);
        ImGui.Separator();ImGui.TextWrapped("Where to obtain");
        if(e.Sources.Length==0)ImGui.TextWrapped("Unknown. No verified acquisition source was found.");
        for(int i=0;i<e.Sources.Length;i++){
            var source=e.Sources[i];ImGui.PushID(i);ImGui.TextWrapped(source.Category+": "+source.Description);ImGui.TextWrapped(source.Evidence);
            if(FashionMetadata.SafeUrl(source.Url)){
                if(ImGui.SmallButton("Open source page"))Dalamud.Utility.Util.OpenLink(source.Url!);
                ImGui.SameLine();if(ImGui.SmallButton("Copy URL"))ImGui.SetClipboardText(source.Url!);
            }
            ImGui.Spacing();ImGui.PopID();
        }
        var correction=FashionMetadata.Correction(e.Id,e.EnglishName);
        if(FashionMetadata.SafeUrl(correction?.SourceUrl)&&ImGui.SmallButton("Open official evidence"))Dalamud.Utility.Util.OpenLink(correction!.SourceUrl!);
        ImGui.Separator();ImGui.TextWrapped("Game unlock item mappings");
        if(e.Items.Length==0)ImGui.TextWrapped("No direct item mapping found; trade/market flags remain Unknown.");
        foreach(var item in e.Items)ImGui.TextWrapped(item.Name+" (#"+item.Id+"), "+(item.Tradeable?"tradeable":"not tradeable")+", "+(item.Marketable?"marketable":"not marketable"));
    }
}
