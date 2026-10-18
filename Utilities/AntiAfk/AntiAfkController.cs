using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using SentinelHub.Configuration;
using SentinelHub.Diagnostics;
namespace SentinelHub.Utilities.AntiAfk;
public sealed class AntiAfkController : IDisposable
{
    private readonly IDalamudPluginInterface pi;
    private readonly IClientState client;
    private readonly HubSettings settings;
    private readonly FaultJournal faults;
    private readonly AntiAfkEngine engine = new(NativeInactivityTimers.TryReset);
    private readonly ICallGateProvider<bool> requested;
    private readonly string token = Guid.NewGuid().ToString("N"); // In-memory lease identity; never exported.
    private long nextOwnershipCheck;
    private bool? previousEnabled;
    private bool? previousLogin;
    private bool claimed;
    private bool disposed;
    public AntiAfkView View { get; private set; } = AntiAfkView.Off;
    public AntiAfkController(IDalamudPluginInterface pi, IClientState client, HubSettings settings, FaultJournal faults)
    {
        this.pi = pi; this.client = client; this.settings = settings; this.faults = faults;
        requested = pi.GetIpcProvider<bool>("SentinelHub.AntiAfk.Enabled");
        requested.RegisterFunc(() => !disposed && settings.AntiAfkEnabled);
    }
    public void Tick()
    {
        if (disposed) return;
        try
        {
            var now=Environment.TickCount64;
            // Ownership changes need a prompt response, not an inventory/IPC scan on every frame.
            // Off and login changes bypass the one-second poll delay.
            var enabled=settings.AntiAfkEnabled;var loggedIn=client.IsLoggedIn;
            if(previousEnabled==enabled&&previousLogin==loggedIn&&now<nextOwnershipCheck)return;
            previousEnabled=enabled;previousLogin=loggedIn;nextOwnershipCheck=now+1000;
            var hud = HudUtilityOwnership.Select(pi.InstalledPlugins.Where(p=>p.InternalName=="SentinelHUD")
                .Select(p=>new SentinelHub.Ecosystem.InstalledPlugin(p.InternalName,p.IsLoaded,p.Manifest.AssemblyVersion.ToString())),new Version(0,8,4,6));
            bool? saved = null;
            var acknowledged = false;
            var compatible = false;
            var needsHandoff=HudUtilityOwnership.RequiresLease(hud?.Version);
            if (hud?.Loaded == true && needsHandoff)
            {
                try
                {
                    compatible = pi.GetIpcSubscriber<int>("SentinelHUD.AntiAfk.ProtocolVersion").InvokeFunc() == 1;
                    if (compatible) saved = pi.GetIpcSubscriber<bool>("SentinelHUD.AntiAfk.Preference").InvokeFunc();
                }
                catch { compatible = false; }
            }
            if (settings.AntiAfkEnabled && client.IsLoggedIn && hud?.Loaded == true && needsHandoff && compatible)
            {
                try { acknowledged = pi.GetIpcSubscriber<string, bool>("SentinelHUD.AntiAfk.TryClaim").InvokeFunc(token); }
                catch { faults.Record(FaultCode.AntiAfkOwnershipUnavailable); }
                claimed = acknowledged || claimed;
            }
            var state = AntiAfkOwnershipPolicy.Resolve(settings.AntiAfkEnabled, client.IsLoggedIn,
                hud is not null, hud?.Loaded == true, hud?.Version, acknowledged);
            if (state != AntiAfkState.Active)
            {
                engine.Suspend(state); // Stop the writer before returning a lease.
                Release();
            }
            else engine.Tick(state, Environment.TickCount64);
            View = new(engine.State, engine.Cycles, saved);
        }
        catch
        {
            engine.Suspend(AntiAfkState.Faulted);
            Release();
            faults.Record(FaultCode.AntiAfkOwnershipUnavailable);
            View = new(engine.State, engine.Cycles, null);
        }
    }
    private void Release()
    {
        if (!claimed) return;
        try { pi.GetIpcSubscriber<string, bool>("SentinelHUD.AntiAfk.Release").InvokeFunc(token); }
        catch { /* HUD independently expires a lost lease within 3 seconds. */ }
        claimed = false;
    }
    public void Dispose()
    {
        disposed = true;
        engine.Suspend();
        Release();
        requested.UnregisterFunc();
        View = new(AntiAfkState.Off, engine.Cycles, null);
    }
}

