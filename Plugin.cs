using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SentinelHub.Diagnostics;
using SentinelHub.Ecosystem;
using SentinelHub.UI;
using SentinelHub.Utilities.AntiAfk;
using SentinelHub.Utilities.Quest;
using SentinelHub.Utilities.Latency;
using SentinelHub.Mounts;
using SentinelHub.Fashion;
using SentinelHub.Identification;
using SentinelHub.Health;
using SentinelHub.Compatibility;
using SentinelHub.Inspection;
using SentinelHub.Coordination;
using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace SentinelHub;
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Pi { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider Interop { get; private set; } = null!;
    [PluginService] internal static IPlayerState Player { get; private set; } = null!;
    [PluginService] internal static IUnlockState Unlocks { get; private set; } = null!;
    [PluginService] internal static ITextureProvider Textures { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    private readonly MountController mounts;
    private readonly PlayerIdentificationMenu identifyMenu;
    private readonly FashionController fashion;
    private readonly LatencyController latency;
    private readonly QuestConvenienceController quest;
    private readonly AntiAfkController antiAfk;
    private readonly WindowSystem windows = new("SentinelHub");
    private readonly Configuration.Configuration settings;
    private readonly FaultJournal faults = new();
    private readonly PluginDiscovery discovery;
    private readonly MainWindow window;
    private readonly QuickOpen quickOpen;
    private readonly PublicCatalog publicCatalog=new();
    private readonly HealthService health;
    private readonly CompatibilityScanner compatibility;
    private readonly PlayerInspector inspector;
    private readonly PauseCoordinator pause;
    private bool disposed;
    public Plugin()
    {
        settings = Pi.GetPluginConfig() as Configuration.Configuration ?? new();
        settings.Normalize();
        discovery = new(Pi, faults);
        health=new(Pi,discovery.Capture,()=>discovery.Snapshot,publicCatalog);
        pause=new(Pi,settings,Save);
        compatibility=new(Pi);
        compatibility.Tick(true);
        antiAfk = new(Pi, Client, settings, faults,()=>settings.AutomationPaused||compatibility.View.Blocks(CompatibilityFeature.AntiAfk));
        quest = new(Pi, Client, GameGui, Condition, Objects, Data, settings, faults,
            ()=>settings.AutomationPaused,o=>compatibility.View.Effective(o));
        latency = new(Pi, Interop, Data, Client, Condition, Objects, settings, faults,()=>settings.AutomationPaused||compatibility.View.Blocks(CompatibilityFeature.Latency));
        mounts = new(Data, Unlocks, Client, Player, Objects, Condition, faults);
        fashion = new(Data, Unlocks, Client, Player, Objects, Condition, faults);
        inspector=new(Objects,Player,Client,Framework,settings,()=>mounts.Ready&&fashion.Ready,
            new InspectionGameData(Data,Unlocks,()=>mounts.Ready&&fashion.Ready));
        quickOpen = new(discovery.Capture, Commands.ProcessCommand, faults);
        window = new(settings, () => discovery.Snapshot, faults, Save, () => discovery.Refresh(true),
            (id, diagnostics) => quickOpen.Open(id, diagnostics), Export,
            () => Pi.UiBuilder.ShouldUseReducedMotion, antiAfkView: () => antiAfk.View, questView: () => quest.View, latencyView: () => latency.View,
            mountView: () => mounts.View, refreshMounts: mounts.RequestRefresh, retryMounts: mounts.Retry, mountIcon: DrawCollectionIcon, fashionView: () => fashion.View, refreshFashion: fashion.RequestRefresh, retryFashion: fashion.Retry,
            healthView:()=>health.View,refreshHealth:()=>{discovery.Refresh(true);health.Tick(true);},publicVersions:publicCatalog.Request,
            compatibilityView:()=>compatibility.View,inspectorView:()=>inspector.View,pauseView:()=>pause.View,pauseRequest:pause.Request);
        identifyMenu = new(ContextMenu, Objects, Client, Player, Framework, Data, settings, () => mounts.Ready && fashion.Ready, window.OpenIdentification, window.ClearIdentification, faults,
            ticket=>{inspector.Select(ticket);window.OpenPlayerInspector();});
        windows.AddWindow(window);
        Commands.AddHandler("/shub", new CommandInfo(OnCommand) { HelpMessage = "Open Sentinel Hub. /shub export saves a sanitized Sentinel diagnostics ZIP." });
        Pi.UiBuilder.Draw += windows.Draw;
        Pi.UiBuilder.OpenConfigUi += Open;
        Pi.UiBuilder.OpenMainUi += Open;
        Framework.Update += Update;
        discovery.Refresh(true);
    }
    private void Update(IFramework _) { if (!disposed) { discovery.Refresh(); health.Tick(); compatibility.Tick(); antiAfk.Tick(); quest.Tick(); latency.Tick(); pause.Tick(antiAfk.View.State!=Utilities.AntiAfkState.Active&&quest.View.State!=Utilities.QuestConvenienceState.Active&&latency.View.State is not (Utilities.LatencyState.Compensating or Utilities.LatencyState.Measuring)); mounts.Tick(window.IsOpen && !settings.Collapsed && settings.Page == Configuration.HubPage.Mounts); fashion.Tick(window.IsOpen && !settings.Collapsed && settings.Page == Configuration.HubPage.Fashion); identifyMenu.Tick(); inspector.Tick(window.IsOpen&&!settings.Collapsed&&settings.Page==Configuration.HubPage.PlayerInspector); } }
    private void DrawCollectionIcon(uint id, Vector2 size)
    {
        Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? wrap = null;
        try { if (Textures.TryGetFromGameIcon(id, out var texture)) texture.TryGetWrap(out wrap, out _); }
        catch { faults.Record(FaultCode.CollectionIconUnavailable); }
        if (wrap is not null) ImGui.Image(wrap.Handle, size);
        else ImGui.Dummy(size);
    }
    private void Open() => window.OpenAndExpand();
    private void OnCommand(string _, string arguments)
    {
        if (arguments.Trim().Equals("export", StringComparison.OrdinalIgnoreCase)) Export();
        else Open();
    }
    private void Save()
    {
        try { Pi.SavePluginConfig(settings); }
        catch { faults.Record(FaultCode.ConfigurationSaveUnavailable); }
    }
    private void Export()
    {
        try
        {
            discovery.Refresh(true);
            var directory = Path.Combine(Pi.GetPluginConfigDirectory(), "diagnostics");
            Directory.CreateDirectory(directory);
            health.Tick(true);
            var name = "sentinel-diagnostics-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".zip";
            var bundle = DiagnosticsBundle.Create(discovery.Snapshot,health.View,faults.Snapshot(),
                new ProviderDiagnosticsReader(Pi).Read(),settings.Version,Client.TerritoryType,Objects.LocalPlayer?.CurrentWorld.RowId??0,
                antiAfk.View,quest.View,latency.View);
            var path=Path.Combine(directory,name);
            using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))file.Write(bundle);
            window.Notice = "Saved " + name + " in Hub's configuration diagnostics folder.";
            Chat.Print("[Sentinel Hub] " + window.Notice);
        }
        catch
        {
            faults.Record(FaultCode.ExportUnavailable);
            window.Notice = "Export could not be saved. Check configuration-folder access.";
        }
    }
    public void Dispose()
    {
        disposed = true;
        publicCatalog.Dispose();
        Framework.Update -= Update;
        identifyMenu.Dispose();
        inspector.Clear();
        fashion.Dispose();
        mounts.Dispose();
        latency.Dispose();
        quest.Dispose();
        antiAfk.Dispose();
        Pi.UiBuilder.Draw -= windows.Draw;
        Pi.UiBuilder.OpenConfigUi -= Open;
        Pi.UiBuilder.OpenMainUi -= Open;
        Commands.RemoveHandler("/shub");
        discovery.Dispose();
        windows.RemoveAllWindows();
        window.Dispose();
    }
}

