using Dalamud.Plugin;
using SentinelHub.Utilities;
using SentinelHub.Utilities.Latency;
namespace SentinelHub.Compatibility;

public sealed class CompatibilityScanner(IDalamudPluginInterface pi)
{
    private readonly LatencyConflictProbe latency=new(pi);
    private long next;
    public CompatibilityView View {get;private set;}=CompatibilityView.Empty;
    public void Tick(bool force=false)
    {
        if(!force&&Environment.TickCount64<next)return;next=Environment.TickCount64+500;
        var findings=new List<Overlap>();
        try{
            var all=pi.InstalledPlugins.ToArray();bool Loaded(params string[] ids)=>all.Any(p=>p.IsLoaded&&ids.Contains(p.InternalName,StringComparer.OrdinalIgnoreCase));
            // NoClippy has no supported module-active query here. A loaded copy is a conservative overlap, not a claim it is writing.
            if(Loaded("NoClippy"))findings.Add(new(OverlapProvider.NoClippy,CompatibilityFeature.Latency,OverlapKind.Possible,true,"NoClippy is loaded; module activity is not exposed. Hub latency compensation is suspended to prevent double compensation."));
            var native=latency.Read(force);
            if(native==LatencyState.XivAlexanderConflict)findings.Add(new(OverlapProvider.XivAlexander,CompatibilityFeature.Latency,OverlapKind.Definite,true,"XivAlexander module/export marker detected. Hub latency compensation is suspended; the module is not modified."));
            if(Loaded("TextAdvance"))findings.AddRange(CompatibilityPolicy.TextAdvance(true,Read("TextAdvance.IsEnabled"),Read("TextAdvance.IsPaused"),
                Read("TextAdvance.GetEnableTalkSkip"),Either("TextAdvance.GetEnableCutsceneEsc","TextAdvance.GetEnableCutsceneSkipConfirm"),Read("TextAdvance.GetEnableRewardPick")));
            if(Loaded("YesAlready"))findings.Add(new(OverlapProvider.YesAlready,CompatibilityFeature.Dialogue,
                Read("YesAlready.IsPluginEnabled")==false?OverlapKind.Information:OverlapKind.Possible,false,
                "YesAlready's configured individual features are not reliably mapped here. Review overlapping dialogue/reward options; Hub does not infer a definite conflict or modify YesAlready."));
            foreach(var (id,provider) in new[]{("SimpleTweaksPlugin",OverlapProvider.SimpleTweaks),("PandorasBox",OverlapProvider.PandorasBox),("DailyRoutines",OverlapProvider.DailyRoutines)})
                if(Loaded(id))findings.Add(new(provider,CompatibilityFeature.AntiAfk,OverlapKind.Information,false,
                    "Loaded multipurpose plugin; active Anti-AFK/quest modules are not exposed by a reviewed status contract. Installation alone does not establish a conflict."));
        }catch{findings.Add(new(OverlapProvider.TextAdvance,CompatibilityFeature.Dialogue,OverlapKind.Possible,true,"Installed-provider discovery unavailable; Hub dialogue automation is suspended until discovery recovers."));
            findings.Add(new(OverlapProvider.TextAdvance,CompatibilityFeature.Cutscene,OverlapKind.Possible,true,"Provider discovery unavailable; Hub cutscene automation is suspended."));
            findings.Add(new(OverlapProvider.TextAdvance,CompatibilityFeature.Rewards,OverlapKind.Possible,true,"Provider discovery unavailable; Hub reward automation is suspended."));}
        View=new(findings.ToArray());
    }
    private bool? Read(string endpoint){try{return pi.GetIpcSubscriber<bool>(endpoint).InvokeFunc();}catch{return null;}}
    private bool? Either(string a,string b){var x=Read(a);var y=Read(b);return x==true||y==true?true:x==false&&y==false?false:null;}
}
