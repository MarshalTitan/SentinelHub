using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using SentinelHub.Activity;
using SentinelHub.Configuration;
using SentinelHub.Ecosystem;
using SentinelHub.Storage;
namespace SentinelHub.Backups;
public sealed record BackupListItem(string Id,string Name,DateTimeOffset At,string[] Owners,bool Valid);
public sealed record BackupView(BackupListItem[] Items,string Status,bool Paused)
{
    public static BackupView Empty=>new([],"No backup operation yet.",false);
}
public sealed class BackupService
{
    private readonly string root;
    private readonly string configRoot;
    private readonly string hubConfig;
    private readonly HubSettings settings;
    private readonly Func<InstalledPlugin[]> inventory;
    private readonly Action<ActivityCode> activity;
    private readonly Func<string> ownVersion;
    private readonly Action<string,string> commitFile;
    private Action? pending;
    public BackupView View {get;private set;}=BackupView.Empty;
    public BackupService(string backupRoot,string hubConfigFile,HubSettings settings,Func<InstalledPlugin[]> inventory,Action<ActivityCode> activity,
        Func<string>? ownVersion=null,Action<string,string>? commitFile=null)
    {
        root=Path.GetFullPath(backupRoot);hubConfig=Path.GetFullPath(hubConfigFile);configRoot=Path.GetDirectoryName(hubConfig)!;
        this.settings=settings;this.inventory=inventory;this.activity=activity;
        this.ownVersion=ownVersion??(()=>typeof(BackupService).Assembly.GetName().Version?.ToString()??"Unknown");
        this.commitFile=commitFile??((temp,path)=>File.Replace(temp,path,null));
    }
    public void RequestCreate(string name)=>Queue(()=>Create(name));
    public void RequestRestore(string id,string owner)=>Queue(()=>Restore(id,owner));
    public void RequestDelete(string id)=>Queue(()=>Delete(id));
    public void RequestRefresh()=>Queue(()=>Refresh("Backup list refreshed."));
    private void Queue(Action command){if(pending is not null)return;pending=command;View=View with{Status="Operation queued for the framework thread."};}
    public void Tick()
    {
        View=View with{Paused=settings.AutomationPaused};var command=pending;pending=null;if(command is null)return;
        try{command();}catch{activity(ActivityCode.BackupFailed);View=View with{Status="Operation rejected or failed. Check owner version/schema, keep external owners unloaded for restore, and check folder access. No partial multi-owner restore is performed."};}
    }
    private string OwnerFile(BackupOwner owner)=>owner.Id=="SentinelHub"?hubConfig:Path.Combine(configRoot,owner.Id+".json");
    private static void CheckPath(string path)
    {
        var full=Path.GetFullPath(path);var current=File.Exists(full)?full:Path.GetDirectoryName(full);
        while(current is not null){if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException();current=Path.GetDirectoryName(current);}
    }
    private static byte[] ReadBounded(string path)
    {
        CheckPath(path);using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(file.Length>BackupPolicy.MaximumBytes)throw new InvalidDataException();
        using var output=new MemoryStream();var buffer=new byte[8192];int read;
        while((read=file.Read(buffer))>0){if(output.Length+read>BackupPolicy.MaximumBytes)throw new InvalidDataException();output.Write(buffer,0,read);}return output.ToArray();
    }
    private string CurrentVersion(BackupOwner owner,bool restore)
    {
        if(owner.Id=="SentinelHub")return ownVersion();
        var all=inventory().Where(p=>p.Id.Equals(owner.Id,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(all.Length==0||all.Any(p=>p.IsDev&&p.Loaded)||restore&&all.Any(p=>p.Loaded))throw new InvalidDataException();
        return all.FirstOrDefault(p=>!p.IsDev)?.Version??all[0].Version??"Unknown";
    }
    private string FileFor(string id)
    {
        if(id.Length!=32||id.Any(c=>!char.IsAsciiHexDigit(c)))throw new InvalidDataException();
        var path=Path.Combine(root,id+".sentinelbackup.json");CheckPath(path);return path;
    }
    private string Save(BackupManifest manifest)
    {
        CheckPath(root);Directory.CreateDirectory(root);
        if(Directory.EnumerateFiles(root,"*.sentinelbackup.json").Take(30).Count()>=30)throw new IOException();
        var id=Guid.NewGuid().ToString("N");var path=FileFor(id);
        var bytes=BackupPolicy.Write(manifest);AtomicFile.Write(path,bytes,(temp,destination)=>File.Move(temp,destination));return id;
    }
    private void Create(string name)
    {
        if(!BackupPolicy.ValidName(name))throw new InvalidDataException();
        var entries=new List<BackupEntry>();var skipped=new List<string>();
        foreach(var owner in BackupRegistry.Owners)try{
            var bytes=owner.Id=="SentinelHub"?JsonSerializer.SerializeToUtf8Bytes(settings):ReadBounded(OwnerFile(owner));
            entries.Add(BackupPolicy.Project(owner,CurrentVersion(owner,false),bytes));
        }catch{skipped.Add(owner.Id);}
        if(entries.Count==0)throw new InvalidDataException();
        Save(new(1,name,DateTimeOffset.UtcNow,entries.ToArray()));activity(ActivityCode.BackupSaved);
        Refresh("Created backup with "+entries.Count+" supported owner(s). "+(skipped.Count>0?"Skipped missing/unreviewed/corrupt owners: "+string.Join(", ",skipped):"All reviewed owners captured.")+" Snapshot includes reviewed fields only; external unsaved changes are not captured.");
    }
    private void Restore(string id,string ownerId)
    {
        if(!settings.AutomationPaused)throw new InvalidDataException();
        var owner=BackupRegistry.Find(ownerId)??throw new InvalidDataException();
        var manifest=BackupPolicy.Read(ReadBounded(FileFor(id)));var entry=manifest.Entries.SingleOrDefault(e=>e.Owner==owner.Id)??throw new InvalidDataException();
        var version=CurrentVersion(owner,true);var path=OwnerFile(owner);var before=ReadBounded(path);
        var candidate=BackupPolicy.Restore(entry,owner,version,before);
        HubSettings? prepared=null;
        if(owner.Id=="SentinelHub"){
            var json=BackupPolicy.Parse(candidate);json["AutomationPaused"]=true;candidate=JsonSerializer.SerializeToUtf8Bytes(json);
            prepared=JsonSerializer.Deserialize<HubSettings>(candidate)??throw new InvalidDataException();prepared.Normalize();
        }
        // Pre-restore snapshot is sanitized by the same field allowlist; raw config/credentials never enter a backup.
        Save(new(1,"pre-restore-"+owner.Id,DateTimeOffset.UtcNow,[BackupPolicy.Project(owner,version,before)]));
        var hash=SHA256.HashData(before);
        AtomicFile.Write(path,candidate,(temp,destination)=>{
            if(!settings.AutomationPaused||CurrentVersion(owner,true)!=version||!CryptographicOperations.FixedTimeEquals(SHA256.HashData(ReadBounded(destination)),hash))throw new IOException();
            commitFile(temp,destination);
        });
        if(prepared is not null){
            // The fields are reviewed public auto-properties on our own settings object. Preparation/validation occurs before the atomic write.
            foreach(var field in owner.Fields){var property=typeof(HubSettings).GetProperty(field.Path)!;property.SetValue(settings,property.GetValue(prepared));}
            settings.AutomationPaused=true;
        }
        activity(ActivityCode.BackupRestored);Refresh("Restored "+owner.Id+" supported fields. Pre-restore backup retained; newer/unrelated and excluded current fields preserved. External owners stay unloaded until you choose to load them. Hub remains paused.");
    }
    private void Delete(string id)
    {
        File.Delete(FileFor(id));activity(ActivityCode.BackupDeleted);Refresh("Selected backup deleted.");
    }
    private void Refresh(string status)
    {
        CheckPath(root);var items=new List<BackupListItem>();
        if(Directory.Exists(root))foreach(var path in Directory.EnumerateFiles(root,"*.sentinelbackup.json").Take(30)){
            var id=Path.GetFileName(path).Replace(".sentinelbackup.json","",StringComparison.Ordinal);
            if(id.Length!=32||id.Any(c=>!char.IsAsciiHexDigit(c)))continue;
            try{var manifest=BackupPolicy.Read(ReadBounded(FileFor(id)));items.Add(new(id,manifest.Name,manifest.CreatedAt,manifest.Entries.Select(e=>e.Owner).ToArray(),true));}
            catch{items.Add(new(id,"Invalid/corrupt backup",DateTimeOffset.MinValue,[],false));}
        }
        View=new(items.OrderByDescending(i=>i.At).ToArray(),status,settings.AutomationPaused);
    }
}
