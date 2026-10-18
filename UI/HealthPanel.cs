using Dalamud.Bindings.ImGui;
using SentinelHub.Health;
namespace SentinelHub.UI;

public sealed class HealthPanel(Func<HealthView> view,Action refresh,Action publicVersions)
{
    public void Draw()
    {
        var v=view();
        ImGui.TextWrapped("Sentinel Health / Dependency Doctor");
        ImGui.TextWrapped("Loaded, responding and ready are different observations. Optional dependencies apply only to their owning features.");
        if(ImGui.Button("Refresh health"))refresh();
        if(ImGui.Button("Fetch public catalog versions"))publicVersions();
        ImGui.TextWrapped(v.CatalogStatus+(v.CatalogAt is {} at?" — "+at.ToLocalTime().ToString("g"):""));
        if(!v.Available)ImGui.TextWrapped("Current inventory unavailable. No cached state is presented as current.");
        foreach(var c in v.Checks){
            ImGui.PushID(c.Id);
            ImGui.Separator();ImGui.TextWrapped(c.Id+" — "+c.State);
            ImGui.TextWrapped("Runtime: "+c.RuntimeVersion+(c.DevelopmentOverride?" DEV":"")+" | Public catalog: "+c.PublicVersion);
            ImGui.TextWrapped(c.Explanation);ImGui.PopID();
        }
    }
}
