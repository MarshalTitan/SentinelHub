using SentinelHub.Health;
using SentinelHub.Compatibility;
namespace SentinelHub.Activity;
public sealed class ActivityService
{
    private readonly Dictionary<string,HealthState> healthStates=[];
    private HashSet<string> overlaps=[];
    private bool? healthAvailable;
    private HealthView? previousHealth;
    private CompatibilityView? previousCompatibility;
    public ActivityJournal Journal {get;}=new();
    public void Add(ActivityCode code,string source="SentinelHub")=>Journal.Add(source,code,DateTimeOffset.UtcNow);
    public void Observe(HealthView health,CompatibilityView compatibility)
    {
        if(ReferenceEquals(previousHealth,health)&&ReferenceEquals(previousCompatibility,compatibility))return;
        var compatibilityChanged=!ReferenceEquals(previousCompatibility,compatibility);
        previousHealth=health;previousCompatibility=compatibility;
        if(healthAvailable is {} previous&&previous!=health.Available)Add(health.Available?ActivityCode.HealthRecovered:ActivityCode.HealthUnavailable);
        healthAvailable=health.Available;
        foreach(var check in health.Checks){
            if(healthStates.TryGetValue(check.Id,out var prior)&&prior!=check.State){
                var unavailable=check.State is HealthState.Missing or HealthState.Disabled or HealthState.Degraded or HealthState.DependencyMissing or HealthState.DependencyDisabled or HealthState.IpcUnavailable or HealthState.Unknown;
                Add(unavailable?ActivityCode.HealthUnavailable:check.State==HealthState.Ready?ActivityCode.HealthRecovered:ActivityCode.HealthChanged,check.Id);
            }
            healthStates[check.Id]=check.State;
        }
        if(!compatibilityChanged)return;
        var current=compatibility.Findings.Where(f=>f.SuspendHub).Select(f=>f.Provider.ToString()).ToHashSet();
        foreach(var id in current.Except(overlaps))Add(ActivityCode.CompatibilitySuspended,id);
        foreach(var id in overlaps.Except(current))Add(ActivityCode.CompatibilityRecovered,id);
        overlaps=current;
    }
}
