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
    private readonly Func<bool> suspended;
    private readonly Func<QuestConvenienceOptions,QuestConvenienceOptions> effective;
    private bool claimed;
    private bool disposed;
    public QuestConvenienceView View { get; private set; } = QuestConvenienceView.Off;
    public QuestConvenienceController(IDalamudPluginInterface pi, IClientState client, IGameGui gui, ICondition condition,
        IObjectTable objects, IDataManager data, HubSettings settings, FaultJournal faults, Func<bool>? suspended=null, Func<QuestConvenienceOptions,QuestConvenienceOptions>? effective=null)
    {
        this.pi = pi; this.client = client; this.settings = settings; this.faults = faults; this.suspended=suspended??(()=>false);this.effective=effective??(o=>o);
        engine = new(gui, condition, objects, data, new DiagnosticBuffer(64));
        session = new(options => engine.Update(options, true, client.TerritoryType), engine.Dispose);
        requested = pi.GetIpcProvider<bool>("SentinelHub.Quest.Enabled");
        requested.RegisterFunc(() => !disposed && settings.QuestConvenienceEnabled && !this.suspended());
    }
    public void Tick()
    {
        if (disposed) return;
        if(suspended()){
            try{session.Stop(QuestConvenienceState.Paused);Release();View=new(QuestConvenienceState.Paused,null,"Paused","Pending actions cleared","Paused");}
            catch{faults.Record(FaultCode.QuestConvenienceOwnershipUnavailable);View=new(QuestConvenienceState.Faulted,null,"Suspension failed","Pending work not confirmed stopped","Unknown");}
            return;
        }
        int? saved = null;
        try
        {
            var hud = pi.InstalledPlugins.FirstOrDefault(p => p.InternalName == "SentinelHUD");
            var compatible = false;
            var acknowledged = false;
            if (hud?.IsLoaded == true)
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
            if (settings.QuestConvenienceEnabled && client.IsLoggedIn && hud?.IsLoaded == true && compatible)
            {
                try { acknowledged = pi.GetIpcSubscriber<string,bool>("SentinelHUD.Quest.TryClaim").InvokeFunc(token); }
                catch { faults.Record(FaultCode.QuestConvenienceOwnershipUnavailable); }
                claimed = claimed || acknowledged;
            }
            var state = QuestConvenienceOwnershipPolicy.Resolve(settings.QuestConvenienceEnabled, client.IsLoggedIn,
                hud is not null, hud?.IsLoaded == true, hud?.Manifest.AssemblyVersion.ToString(), acknowledged);
            session.Tick(state, QuestPreferenceCodec.Encode(effective(settings.QuestOptions())), Environment.TickCount64);
            if (session.State != QuestConvenienceState.Active) Release();
            if (session.State == QuestConvenienceState.Faulted) faults.Record(FaultCode.QuestConvenienceOwnershipUnavailable);
            View = new(session.State, saved, engine.DialogueState, engine.CutsceneState, engine.RewardState);
        }
        catch
        {
            session.Stop(QuestConvenienceState.Faulted);
            Release();
            faults.Record(FaultCode.QuestConvenienceOwnershipUnavailable);
            View = new(session.State, saved, "Paused", "Pending actions cleared", "Paused");
        }
    }
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
        View = QuestConvenienceView.Off;
    }
}

