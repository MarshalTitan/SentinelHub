using Dalamud.Bindings.ImGui;
using SentinelHub.Configuration;
using SentinelHub.Inspection;
using System.Numerics;
using Dalamud.Interface.Utility;
namespace SentinelHub.UI;
public sealed class PlayerInspectorPanel(HubSettings settings,Action save,Func<InspectorView> view,Action<uint> mount,Action<uint?> fashion,Action<uint,Vector2>? icon)
{
    public void Draw()
    {
        var enabled=settings.PlayerInspectorEnabled;
        if(ImGui.Checkbox("Enable shared Player Inspector",ref enabled)){settings.PlayerInspectorEnabled=enabled;save();}
        ImGui.TextWrapped("Opt in to the single player context action for mount, accessory and supported explicit emote information. Right-click a loaded player → Sentinel Hub: Inspect player.");
        var v=view();if(!v.Tracking)ImGui.TextWrapped("No current player observation. Inspect a loaded player; observations clear when this page closes, the actor unloads, you zone, or five minutes elapse.");
        else ImGui.TextWrapped("Live observation: "+v.At.ToLocalTime().ToString("T")+". Own unlock state always describes your character.");
        Part("Mount",v.Mount);
        if(v.Mount.Entry is {} m&&ImGui.Button("Open Mount Collection entry"))mount(m.Id);
        Part("Fashion accessory",v.Fashion);
        if(v.Fashion.Entry is {} f&&ImGui.Button("Open Fashion Accessories entry"))fashion(f.Id);
        Part("Current emote",v.Emote);
        ImGui.Separator();ImGui.TextWrapped("Chocobo barding: "+v.Barding.State);ImGui.TextWrapped(v.Barding.Detail);
        var labels=new[]{"Head barding","Body barding","Leg barding"};
        for(var i=0;i<v.BardingSlots.Length&&i<labels.Length;i++)Part(labels[i],v.BardingSlots[i]);
        ImGui.TextWrapped("Only a currently loaded owned chocobo or Company Chocobo mount is examined. Other mounts and ambiguous model matches are not treated as barding. One-shot emotes and pose states are intentionally not guessed.");
    }
    private void Part(string title,InspectionPart p)
    {
        ImGui.Separator();ImGui.TextWrapped(title+": "+(p.State==InspectionState.None?"None identifiable currently active":p.State.ToString()));
        if(p.Entry is {} e){
            icon?.Invoke(e.Icon,new Vector2(32*ImGuiHelpers.GlobalScale));ImGui.TextWrapped(e.Name+(e.Command.Length>0?" ("+e.Command+")":""));
            if(e.Kind is InspectionKind.Emote or InspectionKind.Barding){
                ImGui.TextWrapped("Your unlock: "+(e.Owned is true?"Unlocked":e.Owned is false?"Locked":"Unknown"));
                ImGui.TextWrapped("Source category: "+e.Category);
                if(e.Sources.Length==0)ImGui.TextWrapped("Acquisition source: Unknown");
                foreach(var s in e.Sources)ImGui.TextWrapped(s);
                ImGui.TextWrapped("Patch: "+(e.Patch??"Unknown")+" | Expansion: "+(e.Expansion??"Unknown")+" | Currently obtainable: "+(e.Obtainable is true?"Verified yes":e.Obtainable is false?"Verified no":"Unknown"));
            }
        }
        if(p.Detail.Length>0)ImGui.TextWrapped(p.Detail);
    }
}
