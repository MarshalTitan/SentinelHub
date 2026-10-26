using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using SentinelCore.Diagnostics;
using SentinelHub.Configuration;
using SentinelHub.Diagnostics;
namespace SentinelHub.Utilities.Quest;
public sealed class QuestConvenienceController : IDisposable
{
    private readonly IDalamudPluginInterface pi;
    private readonly IClientState client;
    private readonly HubSettings settings;
    private readonly FaultJournal faults;
    private readonly QuestConvenienceService engine;
    private readonly QuestExecutionSession session;
    private readonly ICallGateProvider<bool> requested;
    private readonly string token = Guid.NewGuid().ToString("N");
    private readonly Func<QuestConvenienceOptions,QuestConvenienceOptions> effective;
    private readonly Func<SentinelHub.Ecosystem.InstalledPlugin[]> inventory;
    private readonly QuestActivityMonitor monitor;
    private SentinelHub.Ecosystem.InstalledPlugin[]? previousInventory;
    private QuestConvenienceState ownership;
    private QuestConvenienceOptions options=new();
    private int preferences=-1;
    private int? savedPreferences;
    private bool? previousEnabled,previousLogin;
    private long nextOwnership;
    private int savedOptions=-1;
    private object? previousEffectiveVersion;
    private readonly Func<object?>? effectiveVersion;
    private bool claimed;
    private bool disposed;
    public QuestConvenienceView View { get; private set; } = QuestConvenienceView.Off;
    public QuestConvenienceController(IDalamudPluginInterface pi, IClientState client, IGameGui gui, ICondition condition,
        IObjectTable objects, IDataManager data, HubSettings settings, FaultJournal faults, Func<QuestConvenienceOptions,QuestConvenienceOptions>? effective=null,
        Func<SentinelHub.Ecosystem.InstalledPlugin[]>? inventory=null,IAddonLifecycle? lifecycle=null,Func<object?>? effectiveVersion=null)
    {
        this.pi = pi; this.client = client; this.settings = settings; this.faults = faults;this.effective=effective??(o=>o);
        this.inventory=inventory??(()=>pi.InstalledPlugins.Select(p=>new SentinelHub.Ecosystem.InstalledPlugin(p.InternalName,p.IsLoaded,p.Version.ToString())).ToArray());
        this.effectiveVersion=effectiveVersion;
        monitor=new(gui,condition,lifecycle);
        engine = new(gui, condition, objects, data, new DiagnosticBuffer(64));
        session = new(options => engine.Update(options, true, client.TerritoryType), engine.Dispose);
        requested = pi.GetIpcProvider<bool>("SentinelHub.Quest.Enabled");
        requested.RegisterFunc(() => !disposed && settings.QuestConvenienceEnabled);
    }
    public void Tick()
    {
        if (disposed) return;
        var enabled=settings.QuestConvenienceEnabled;
        if(!enabled&&previousEnabled==false)return;
        if(!enabled)
        {
            previousEnabled=false;session.Stop();monitor.Reset();Release();View=QuestConvenienceView.Off;return;
        }
        int? saved = null;
        try
        {
            var now=Environment.TickCount64;var loggedIn=client.IsLoggedIn;
            if(!loggedIn){
                if(previousLogin!=false||previousEnabled!=true){session.Stop(QuestConvenienceState.WaitingForLogin);monitor.Reset();Release();
                    View=new(QuestConvenienceState.WaitingForLogin,null,"Waiting for login","Waiting for login","Waiting for login");}
                previousEnabled=true;previousLogin=false;nextOwnership=0;return;
            }
            var all=inventory();
            var version=effectiveVersion?.Invoke();
            var raw=(settings.QuestDialogue?1:0)|(settings.QuestCutscenes?2:0)|((Enum.IsDefined(settings.QuestRewardMode)?(int)settings.QuestRewardMode:0)<<2);
            var packedOptions=preferences;
            if(raw!=savedOptions||!ReferenceEquals(version,previousEffectiveVersion)||effectiveVersion is null&&now>=nextOwnership)
            {savedOptions=raw;previousEffectiveVersion=version;packedOptions=QuestPreferenceCodec.Encode(effective(settings.QuestOptions()));}
            var optionsChanged=packedOptions!=preferences;
            if(optionsChanged){preferences=packedOptions;QuestPreferenceCodec.TryDecode(preferences,out options);monitor.Reset();}
            var recheck=previousEnabled!=enabled||previousLogin!=loggedIn||!ReferenceEquals(previousInventory,all)||now>=nextOwnership;
            previousEnabled=enabled;previousLogin=loggedIn;previousInventory=all;
            if(recheck){
            nextOwnership=now+1000;
            var hud = HudUtilityOwnership.Select(all,new Version(0,8,4,7));
            var compatible = false;
            var acknowledged = false;
            var needsHandoff=HudUtilityOwnership.RequiresLease(hud?.Version);
            if (hud?.Loaded == true && needsHandoff)
            {
                try
                {
                    compatible = pi.GetIpcSubscriber<int>("SentinelHUD.Quest.ProtocolVersion").InvokeFunc() == 1;
                    if (compatible)
                    {
                        var packed = pi.GetIpcSubscriber<int>("SentinelHUD.Quest.Preference").InvokeFunc();
                        compatible = QuestPreferenceCodec.TryDecode(packed, out _);
                        if (compatible) saved = packed;
                    }
                }
                catch { compatible = false; }
            }
            if (settings.QuestConvenienceEnabled && client.IsLoggedIn && hud?.Loaded == true && needsHandoff && compatible)
            {
                try { acknowledged = pi.GetIpcSubscriber<string,bool>("SentinelHUD.Quest.TryClaim").InvokeFunc(token); }
                catch { faults.Record(FaultCode.QuestConvenienceOwnershipUnavailable); }
                claimed = claimed || acknowledged;
            }
            var state = QuestConvenienceOwnershipPolicy.Resolve(settings.QuestConvenienceEnabled, client.IsLoggedIn,
                hud is not null, hud?.Loaded == true, hud?.Version, acknowledged);
            ownership=state;savedPreferences=saved;
            }
            var process=ownership==QuestConvenienceState.Active&&monitor.ShouldProcess(options,engine.AwaitingCutsceneSkipConfirmation);
            session.Tick(ownership, preferences, now,process);
            if (session.State != QuestConvenienceState.Active) Release();
            if (session.State == QuestConvenienceState.Faulted) faults.Record(FaultCode.QuestConvenienceOwnershipUnavailable);
            if(recheck||optionsChanged||process)View = new(session.State, savedPreferences, engine.DialogueState, engine.CutsceneState, engine.RewardState);
        }
        catch
        {
            session.Stop(QuestConvenienceState.Faulted);
            Release();
            faults.Record(FaultCode.QuestConvenienceOwnershipUnavailable);
            View = new(session.State, saved, "Paused", "Pending actions cleared", "Paused");
        }
    }
    public void Invalidate(){session.Stop();monitor.Reset();nextOwnership=0;}
    private void Release()
    {
        if (!claimed) return;
        try { pi.GetIpcSubscriber<string,bool>("SentinelHUD.Quest.Release").InvokeFunc(token); }
        catch { /* A missing HUD cannot retain native work; its lease is independently bounded. */ }
        claimed = false;
    }
    public void Dispose()
    {
        disposed = true;
        session.Stop();
        engine.Dispose();
        Release();
        requested.UnregisterFunc();
        monitor.Dispose();
        View = QuestConvenienceView.Off;
    }
}

