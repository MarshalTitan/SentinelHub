using Dalamud.Plugin;
using SentinelHub.Ecosystem;
using SentinelHub.Utilities;
using SentinelHub.Utilities.Latency;
namespace SentinelHub.Compatibility;

public sealed class CompatibilityScanner
{
    private readonly IDalamudPluginInterface pi;
    private readonly Func<InstalledPlugin[]> inventory;
    private readonly Func<bool> available;
    private readonly LatencyConflictProbe latency;
    private readonly Func<bool,LatencyState> readNative;
    private Overlap? nativeFinding;
    private InstalledPlugin[]? previousInventory;
    private bool previousLatency,previousQuest,previousAvailable;
    private long next;
    public CompatibilityView View {get;private set;}=CompatibilityView.Empty;
    public CompatibilityScanner(IDalamudPluginInterface pi,Func<InstalledPlugin[]> inventory,Func<bool> available,Func<bool,LatencyState>? nativeProbe=null)
    {
        this.pi=pi;this.inventory=inventory;this.available=available;latency=new(pi,inventory);
        readNative=nativeProbe??latency.Read;
    }
    public LatencyState LatencyReadiness()=>!available()?LatencyState.ConflictCheckUnavailable:latency.ReadCached();
    public bool Tick(bool force=false,bool latencyEnabled=false,bool questEnabled=false,bool visible=false)
    {
        var all=inventory();var valid=available();var now=Environment.TickCount64;
        var changed=!ReferenceEquals(previousInventory,all)||previousAvailable!=valid;
        var enabled=previousLatency!=latencyEnabled||previousQuest!=questEnabled;
        if(!force&&!changed&&!enabled&&now<next)return false;
        previousInventory=all;previousAvailable=valid;previousLatency=latencyEnabled;previousQuest=questEnabled;
        // Active Quest retains the reviewed 500 ms provider-state protection. These providers
        // expose no supported change notification, so a bounded status poll remains necessary.
        next=now+(questEnabled?500:latencyEnabled?1000:visible?5000:30000);
        var findings=new List<Overlap>();
        bool Loaded(params string[] ids)=>all.Any(p=>p.Loaded&&ids.Contains(p.Id,StringComparer.OrdinalIgnoreCase));
        if(!valid)
        {
            foreach(var feature in new[]{CompatibilityFeature.Dialogue,CompatibilityFeature.Cutscene,CompatibilityFeature.Rewards,CompatibilityFeature.Latency})
                findings.Add(new(OverlapProvider.TextAdvance,feature,OverlapKind.Possible,true,"Provider discovery unavailable; affected Hub automation is suspended until discovery recovers."));
        }
        else
        {
            if(all.Any(p=>LatencyConflictPolicy.IsNoClippy(p.Id,p.Loaded)))findings.Add(new(OverlapProvider.NoClippy,CompatibilityFeature.Latency,OverlapKind.Possible,true,"NoClippy is loaded; module activity is not exposed. Hub latency compensation is suspended to prevent double compensation."));
            // No process enumeration while latency is Off. Mandatory scan before activation,
            // on inventory changes, and every ten seconds while active; callbacks use cache only.
            if(latencyEnabled)
            {
                var native=readNative(force||changed||enabled);
                nativeFinding=native==LatencyState.XivAlexanderConflict?new(OverlapProvider.XivAlexander,CompatibilityFeature.Latency,OverlapKind.Definite,true,"XivAlexander module/export marker detected. Hub latency compensation is suspended; the module is not modified."):
                    native==LatencyState.ConflictCheckUnavailable?new(OverlapProvider.XivAlexander,CompatibilityFeature.Latency,OverlapKind.Possible,true,"Native conflict check unavailable; latency compensation is suspended."):null;
            }
            if(nativeFinding is not null)findings.Add(latencyEnabled?nativeFinding:nativeFinding with{Explanation="Cached native conflict observation; latency is Off. A fresh check is required before activation."});
            if(Loaded("TextAdvance"))findings.AddRange(CompatibilityPolicy.TextAdvance(true,Read("TextAdvance.IsEnabled"),Read("TextAdvance.IsPaused"),Read("TextAdvance.GetEnableTalkSkip"),Either("TextAdvance.GetEnableCutsceneEsc","TextAdvance.GetEnableCutsceneSkipConfirm"),Read("TextAdvance.GetEnableRewardPick")));
            if(Loaded("YesAlready"))findings.Add(new(OverlapProvider.YesAlready,CompatibilityFeature.Dialogue,Read("YesAlready.IsPluginEnabled")==false?OverlapKind.Information:OverlapKind.Possible,false,"YesAlready's individual features are not reliably mapped. Review overlapping options; Hub does not modify YesAlready."));
            foreach(var (id,provider) in Multipurpose)
                if(Loaded(id))findings.Add(new(provider,CompatibilityFeature.AntiAfk,OverlapKind.Information,false,"Loaded multipurpose plugin; active modules are not exposed by a reviewed contract. Installation alone does not establish a conflict."));
        }
        var values=findings.ToArray();
        if(View.Findings.SequenceEqual(values))return false;
        View=new(values);return true;
    }
    private static readonly (string,OverlapProvider)[] Multipurpose=[("SimpleTweaksPlugin",OverlapProvider.SimpleTweaks),("PandorasBox",OverlapProvider.PandorasBox),("DailyRoutines",OverlapProvider.DailyRoutines)];
    private bool? Read(string endpoint){try{return pi.GetIpcSubscriber<bool>(endpoint).InvokeFunc();}catch{return null;}}
    private bool? Either(string a,string b){var x=Read(a);var y=Read(b);return x==true||y==true?true:x==false&&y==false?false:null;}
}
