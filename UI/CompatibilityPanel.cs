using Dalamud.Bindings.ImGui;
using SentinelHub.Compatibility;
namespace SentinelHub.UI;
public sealed class CompatibilityPanel(Func<CompatibilityView> view)
{
    public void Draw()
    {
        ImGui.TextWrapped("Compatibility / Hub self-protection");
        var findings=view().Findings;
        if(findings.Length==0)ImGui.TextWrapped("No reviewed overlap currently detected. This is not a scan of every third-party module or private configuration.");
        foreach(var f in findings){ImGui.Separator();ImGui.TextWrapped(f.Provider+" / "+f.Feature+" — "+f.Kind+(f.SuspendHub?" / Hub suspended":""));ImGui.TextWrapped(f.Explanation);}
        ImGui.Separator();
    }
}
