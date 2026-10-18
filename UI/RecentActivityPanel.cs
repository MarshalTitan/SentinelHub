using Dalamud.Bindings.ImGui;
using SentinelHub.Activity;
namespace SentinelHub.UI;
public sealed class RecentActivityPanel(Func<ActivityEntry[]> entries,Action clear)
{
    private int severity;
    private int page;
    public void Draw()
    {
        ImGui.TextWrapped("Current Hub session only — up to 200 events held in memory. Clears on Hub reload or game restart. Repeated events are grouped.");
        ImGui.SetNextItemWidth(-1);if(ImGui.Combo("##ActivitySeverity",ref severity,new[]{"All severities","Info","Success","Warning","Error"},5))page=0;
        var all=entries().Where(e=>severity==0||(int)ActivityCatalog.Severity(e.Code)==severity-1).ToArray();
        page=Math.Clamp(page,0,Math.Max(0,(all.Length-1)/20));
        if(ImGui.Button("Newer events"))page=Math.Max(0,page-1);
        if(ImGui.Button("Older events"))page=Math.Min(Math.Max(0,(all.Length-1)/20),page+1);
        if(all.Length==0)ImGui.TextWrapped("No recent matching events.");
        foreach(var e in all.Skip(page*20).Take(20)){
            ImGui.Separator();ImGui.TextWrapped(e.LastAt.ToLocalTime().ToString("g")+" — "+ActivityCatalog.Severity(e.Code)+(e.Count>1?" ×"+e.Count:""));
            ImGui.TextWrapped(ActivityCatalog.Message(e));
        }
        if(ImGui.Button("Clear recent activity")){clear();page=0;}
    }
}
