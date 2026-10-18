using System.Text;
using SentinelHub.Health;
using SentinelHub.Compatibility;
using SentinelHub.Storage;
namespace SentinelHub.Activity;
public sealed class ActivityService : IDisposable
{
    private readonly string path;
    private readonly Dictionary<string,HealthState> healthStates=[];
    private HashSet<string> overlaps=[];
    private bool? healthAvailable;
    private long next;
    public ActivityJournal Journal {get;}=new();
    public ActivityService(string directory)
    {
        path=Path.Combine(directory,"recent-activity.json");
        try{if(File.Exists(path)&&new FileInfo(path).Length<=256*1024)Journal.Load(File.ReadAllText(path),DateTimeOffset.UtcNow);}catch{}
    }
    public void Add(ActivityCode code,string source="SentinelHub")=>Journal.Add(source,code,DateTimeOffset.UtcNow);
    public void Observe(HealthView health,CompatibilityView compatibility)
    {
        if(healthAvailable is {} previous&&previous!=health.Available)Add(health.Available?ActivityCode.HealthRecovered:ActivityCode.HealthUnavailable);
        healthAvailable=health.Available;
        foreach(var check in health.Checks){
            if(healthStates.TryGetValue(check.Id,out var prior)&&prior!=check.State){
                var unavailable=check.State is HealthState.Missing or HealthState.Disabled or HealthState.Degraded or HealthState.DependencyMissing or HealthState.DependencyDisabled or HealthState.IpcUnavailable or HealthState.Unknown;
                Add(unavailable?ActivityCode.HealthUnavailable:check.State==HealthState.Ready?ActivityCode.HealthRecovered:ActivityCode.HealthChanged,check.Id);
            }
            healthStates[check.Id]=check.State;
        }
        var current=compatibility.Findings.Where(f=>f.SuspendHub).Select(f=>f.Provider.ToString()).ToHashSet();
        foreach(var id in current.Except(overlaps))Add(ActivityCode.CompatibilitySuspended,id);
        foreach(var id in overlaps.Except(current))Add(ActivityCode.CompatibilityRecovered,id);
        overlaps=current;
        if(Environment.TickCount64>=next){next=Environment.TickCount64+10000;Flush();}
    }
    private void Flush(){if(!Journal.Dirty)return;try{AtomicFile.Write(path,Encoding.UTF8.GetBytes(Journal.Serialize(DateTimeOffset.UtcNow)));Journal.MarkSaved();}catch{}}
    public void Dispose()=>Flush();
}
