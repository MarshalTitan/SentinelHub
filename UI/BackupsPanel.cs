using Dalamud.Bindings.ImGui;
using SentinelHub.Backups;
namespace SentinelHub.UI;
public sealed class BackupsPanel(Func<BackupView> view,Action<string> create,Action refresh,Action<string,string> restore,Action<string> delete)
{
    private string name="Sentinel settings";
    private string? selected;
    private int ownerIndex;
    private bool confirmRestore;
    private bool confirmDelete;
    public void Select(string? id){selected=id;ownerIndex=0;confirmDelete=false;confirmRestore=false;}
    public void Draw()
    {
        var v=view();ImGui.TextWrapped("Reviewed Sentinel settings backups — local only, up to 30 files. This is a selective settings backup, not a full plugin-directory copy.");
        ImGui.SetNextItemWidth(-1);ImGui.InputText("##BackupName",ref name,60);
        if(ImGui.Button("Create named backup"))create(name);
        if(ImGui.Button("Refresh backup list"))refresh();
        ImGui.TextWrapped(v.Status);
        foreach(var b in v.Items){
            ImGui.PushID(b.Id);if(ImGui.Selectable(b.Name+"##Backup",selected==b.Id))Select(b.Id);
            ImGui.TextWrapped(b.Valid?b.At.ToLocalTime().ToString("g")+" — "+string.Join(", ",b.Owners):"Invalid format/checksum/schema; restore unavailable.");ImGui.PopID();
        }
        var item=v.Items.FirstOrDefault(i=>i.Id==selected);
        if(item is not null){
            ImGui.Separator();
            if(item.Valid&&item.Owners.Length>0){
                ownerIndex=Math.Clamp(ownerIndex,0,item.Owners.Length-1);ImGui.SetNextItemWidth(-1);
                if(ImGui.Combo("##RestoreOwner",ref ownerIndex,item.Owners,item.Owners.Length))confirmRestore=false;
                var owner=BackupRegistry.Find(item.Owners[ownerIndex])!;
                ImGui.TextWrapped("Restore target: "+owner.Id+"; reviewed schema "+owner.Schema+", build "+owner.Version+". Exactly one owner's reviewed fields are restored per operation.");
                ImGui.TextWrapped(owner.Exclusions);
                ImGui.TextWrapped("Pause Hub automation first. External owners must remain unloaded until restore completes. A sanitized pre-restore backup is required; changed files/newer schemas/versions reject the operation.");
                ImGui.Checkbox("Confirm selected owner restore",ref confirmRestore);
                ImGui.BeginDisabled(!v.Paused||!confirmRestore);
                if(ImGui.Button("Restore selected owner's settings")){restore(item.Id,owner.Id);confirmRestore=false;}
                ImGui.EndDisabled();
            }
            ImGui.Checkbox("Confirm selected backup deletion",ref confirmDelete);ImGui.BeginDisabled(!confirmDelete);
            if(ImGui.Button("Delete selected backup")){delete(item.Id);selected=null;confirmDelete=false;}ImGui.EndDisabled();
        }
        if(ImGui.CollapsingHeader("Reviewed backup contents and exclusions"))foreach(var owner in BackupRegistry.Owners){
            ImGui.Separator();ImGui.TextWrapped(owner.Id+" — "+owner.Fields.Length+" reviewed scalar fields; schema "+owner.Schema+" / "+owner.Version);
            ImGui.TextWrapped("Fields: "+string.Join(", ",owner.Fields.Select(f=>f.Path)));ImGui.TextWrapped(owner.Exclusions);
        }
    }
}
