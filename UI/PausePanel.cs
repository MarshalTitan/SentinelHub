using Dalamud.Bindings.ImGui;
using SentinelHub.Coordination;
namespace SentinelHub.UI;
public sealed class PausePanel(Func<PauseView> view,Action<bool> request)
{
    public void Draw()
    {
        var v=view();ImGui.TextWrapped("Pause Sentinel Automation / Safe Mode");
        if(ImGui.Button(v.Requested?"Resume participating automation":"Pause Sentinel automation"))request(!v.Requested);
        if(v.Requested&&!v.FullyAcknowledged)ImGui.TextWrapped("Partial pause: some loaded components have not acknowledged and may still automate. Read each result.");
        foreach(var p in v.Participants){ImGui.TextWrapped(p.Id+": "+p.State);ImGui.TextWrapped(p.Explanation);}
        ImGui.TextWrapped("HUD rendering, settings, Profiles, Relay receiving and collection/inspection remain available. Older consumers require their own stop controls; Hub never assumes they stopped.");
        ImGui.Separator();
    }
}
