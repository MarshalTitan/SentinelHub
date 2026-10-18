using System.Text.Json;
using SentinelHub.Ecosystem;
namespace SentinelHub.Activity;
public enum ActivitySeverity { Info, Success, Warning, Error }
// Explicit values preserve retained event identities; retired candidate codes are discarded on load.
public enum ActivityCode { HealthUnavailable=0, HealthRecovered=1, HealthChanged=2, CompatibilitySuspended=3, CompatibilityRecovered=4,
    DiagnosticsSaved=8, DiagnosticsFailed=9, ProfileApplied=14, ProfileVerificationFailed=15, HuntTagged=16, HuntCredited=17,
    RelayDisconnected=18, RelayRecovered=19 }
public sealed record ActivityEntry(DateTimeOffset At,DateTimeOffset LastAt,string Source,ActivityCode Code,int Count);
public static class ActivityCatalog
{
    public static readonly string[] Sources=[..Catalog.Components.Select(c=>c.Id),..Catalog.Dependencies.Select(d=>d.Id),"SentinelCore","Faloop",
        "NoClippy","XivAlexander","TextAdvance","YesAlready","SimpleTweaks","PandorasBox","DailyRoutines"];
    public static bool Allowed(string source,ActivityCode code)=>Enum.IsDefined(code)&&Sources.Contains(source,StringComparer.Ordinal)&&(code switch {
        ActivityCode.ProfileApplied or ActivityCode.ProfileVerificationFailed=>source=="SentinelProfiles",
        ActivityCode.HuntTagged or ActivityCode.HuntCredited=>source=="SRankSentinel",
        ActivityCode.RelayDisconnected or ActivityCode.RelayRecovered=>source=="SentinelRelay",
        ActivityCode.DiagnosticsSaved or ActivityCode.DiagnosticsFailed=>source=="SentinelHub",
        _=>true,
    });
    public static ActivitySeverity Severity(ActivityCode code)=>code switch{
        ActivityCode.DiagnosticsFailed or ActivityCode.ProfileVerificationFailed=>ActivitySeverity.Error,
        ActivityCode.HealthUnavailable or ActivityCode.CompatibilitySuspended or ActivityCode.RelayDisconnected=>ActivitySeverity.Warning,
        ActivityCode.HealthRecovered or ActivityCode.CompatibilityRecovered or ActivityCode.DiagnosticsSaved
            or ActivityCode.ProfileApplied or ActivityCode.HuntTagged or ActivityCode.HuntCredited or ActivityCode.RelayRecovered=>ActivitySeverity.Success,
        _=>ActivitySeverity.Info,
    };
    public static string Message(ActivityEntry entry)=>entry.Code switch{
        ActivityCode.HealthUnavailable=>entry.Source+" became unavailable or degraded.",ActivityCode.HealthRecovered=>entry.Source+" recovered.",
        ActivityCode.HealthChanged=>entry.Source+" health observation changed.",
        ActivityCode.CompatibilitySuspended=>entry.Source+" overlap detected; affected Hub behavior is suspended.",
        ActivityCode.CompatibilityRecovered=>entry.Source+" overlap cleared; Hub's own preferences/readiness apply.",
        ActivityCode.DiagnosticsSaved=>"Sanitized Sentinel diagnostics package saved.",ActivityCode.DiagnosticsFailed=>"Diagnostics package could not be saved.",
        ActivityCode.ProfileApplied=>"Sentinel Profiles reports a profile applied.",ActivityCode.ProfileVerificationFailed=>"Sentinel Profiles reports verification failed.",
        ActivityCode.HuntTagged=>"S Rank Sentinel reports a hunt tagged.",ActivityCode.HuntCredited=>"S Rank Sentinel reports hunt credit confirmed.",
        ActivityCode.RelayDisconnected=>"Sentinel Relay reports disconnected.",ActivityCode.RelayRecovered=>"Sentinel Relay reports recovered.",_=>"State changed.",
    };
}
public sealed class ActivityJournal
{
    public const int Capacity=200;
    public static readonly TimeSpan Retention=TimeSpan.FromDays(7);
    private readonly List<ActivityEntry> entries=[];
    public bool Dirty {get;private set;}
    public ActivityEntry[] Snapshot(DateTimeOffset now){Prune(now);return entries.OrderByDescending(e=>e.LastAt).ToArray();}
    public bool Add(string source,ActivityCode code,DateTimeOffset now)
    {
        if(!ActivityCatalog.Allowed(source,code))return false;Prune(now);
        var index=entries.FindLastIndex(e=>e.Source==source&&e.Code==code&&now>=e.LastAt&&now-e.LastAt<TimeSpan.FromSeconds(60));
        if(index>=0)entries[index]=entries[index] with{LastAt=now,Count=Math.Min(9999,entries[index].Count+1)};
        else{if(entries.Count>=Capacity)entries.RemoveAt(entries.FindIndex(e=>e.LastAt==entries.Min(v=>v.LastAt)));entries.Add(new(now,now,source,code,1));}
        Dirty=true;return true;
    }
    private void Prune(DateTimeOffset now){if(entries.RemoveAll(e=>e.LastAt<now-Retention||e.LastAt>now.AddMinutes(5))>0)Dirty=true;}
    public void Load(string json,DateTimeOffset now)
    {
        entries.Clear();try{
            if(json.Length>256*1024)throw new InvalidDataException();
            var parsed=JsonSerializer.Deserialize<ActivityEntry[]>(json,new JsonSerializerOptions{MaxDepth=8})??[];
            if(parsed.Length>Capacity)throw new InvalidDataException();
            entries.AddRange(parsed.Where(e=>e is not null&&ActivityCatalog.Allowed(e.Source,e.Code)&&e.Count is >=1 and <=9999
                &&e.At<=e.LastAt&&e.LastAt>=now-Retention&&e.LastAt<=now.AddMinutes(5)).OrderBy(e=>e.LastAt));
        }catch{entries.Clear();}Dirty=false;
    }
    public string Serialize(DateTimeOffset now)=>JsonSerializer.Serialize(Snapshot(now));
    public void MarkSaved()=>Dirty=false;
    public void Clear(){entries.Clear();Dirty=true;}
}
