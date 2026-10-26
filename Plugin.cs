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
using SentinelHub.Activity;
using SentinelHub.Profiles;
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
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static ITargetManager Targets { get; private set; } = null!;
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
    private readonly ActivityService activity;
    private readonly HubOperationsIpc operationsIpc;
    private readonly ProfilesIntegration profiles;
    private readonly FrameworkProfiler performance=new();
    private volatile bool sessionInvalidated;
    private bool wasExpanded;
    private bool disposed;
    private volatile bool exportRequested;
    private volatile bool healthRefreshRequested;
    public Plugin()
    {
        settings = Pi.GetPluginConfig() as Configuration.Configuration ?? new();
        settings.Normalize();
        if(!LegacyActivityCleanup.TryRemove(Pi.GetPluginConfigDirectory()))faults.Record(FaultCode.ActivityHistoryCleanupUnavailable);
        activity=new();
        operationsIpc=new(Pi,Framework,activity.Journal,faults);
        discovery = new(Pi, faults);
        discovery.Refresh(true);
        health=new(Pi,()=>discovery.Inventory,()=>discovery.Snapshot,publicCatalog,OwnCharacterKey);
        compatibility=new(Pi,()=>discovery.Inventory,()=>discovery.Available&&!discovery.PendingChange);
        compatibility.Tick(true);
        antiAfk = new(Pi, Client, settings, faults,ReadInventory);
        quest = new(Pi, Client, GameGui, Condition, Objects, Data, settings, faults,
            o=>compatibility.View.Effective(o),ReadInventory,AddonLifecycle,()=>compatibility.View);
        latency = new(Pi, Interop, Data, Client, Condition, Objects, settings, faults,()=>compatibility.View.Blocks(CompatibilityFeature.Latency),compatibility.LatencyReadiness);
        mounts = new(Data, Unlocks, Client, Player, Objects, Condition, faults);
        fashion = new(Data, Unlocks, Client, Player, Objects, Condition, faults);
        inspector=new(Objects,Player,Client,Framework,settings,()=>mounts.Ready&&fashion.Ready,
            new InspectionGameData(Data,Unlocks,()=>mounts.Ready&&fashion.Ready),()=>Targets.Target?.GameObjectId??0);
        profiles=new(Pi,()=>discovery.Inventory,()=>discovery.Available);
        quickOpen = new(discovery.Capture, Commands.ProcessCommand, faults);
        window = new(settings, () => discovery.Snapshot, faults, Save, () => healthRefreshRequested=true,
            (id, diagnostics) => quickOpen.Open(id, diagnostics), Export,
            () => Pi.UiBuilder.ShouldUseReducedMotion, antiAfkView: () => antiAfk.View, questView: () => quest.View, latencyView: () => latency.View,
            mountView: () => mounts.View, refreshMounts: mounts.RequestRefresh, retryMounts: mounts.Retry, mountIcon: DrawCollectionIcon, fashionView: () => fashion.View, refreshFashion: fashion.RequestRefresh, retryFashion: fashion.Retry,
            healthView:()=>health.View,refreshHealth:()=>healthRefreshRequested=true,publicVersions:publicCatalog.Request,
            compatibilityView:()=>compatibility.View,inspectorView:()=>inspector.View,
            activityEntries:()=>activity.Journal.Snapshot(DateTimeOffset.UtcNow),clearActivity:activity.Journal.Clear,
            profilesView:()=>profiles.View,openProfile:profiles.RequestOpen,refreshProfiles:profiles.Refresh,profilesNotice:()=>profiles.Notice,
            dismissInspector:inspector.Clear);
        identifyMenu = new(ContextMenu, Objects, Client, Player, Framework, Data, settings, () => mounts.Ready && fashion.Ready, window.OpenIdentification, ()=>{window.ClearIdentification();inspector.Clear();}, faults,
            ticket=>{inspector.Select(ticket);window.OpenPlayerInspector();});
        windows.AddWindow(window);
        Commands.AddHandler("/shub", new CommandInfo(OnCommand) { HelpMessage = "Open Sentinel Hub. /shub export saves a sanitized Sentinel diagnostics ZIP." });
        Pi.UiBuilder.Draw += Draw;
        Pi.UiBuilder.OpenConfigUi += Open;
        Pi.UiBuilder.OpenMainUi += Open;
        Framework.Update += Update;
        Client.Login+=InvalidateSession;
        Client.Logout+=OnLogout;
        Client.TerritoryChanged+=OnTerritory;
    }
    private InstalledPlugin[] ReadInventory()=>discovery.Available?discovery.Inventory:throw new InvalidOperationException("Plugin inventory unavailable.");
    private void InvalidateSession()=>sessionInvalidated=true;
    private void OnLogout(int _,int __)=>InvalidateSession();
    private void OnTerritory(uint _)=>InvalidateSession();
    private void Draw(){performance.Context(window.IsOpen);using var timing=performance.Measure(HubService.Draw);windows.Draw();}
    private void Update(IFramework _)
    {
        if(disposed)return;
        performance.Context(window.IsOpen);using var updateTiming=performance.Measure(HubService.Update);
        if(sessionInvalidated){sessionInvalidated=false;mounts.Invalidate();fashion.Invalidate();identifyMenu.Invalidate();inspector.Clear();quest.Invalidate();}
        var expanded=window.IsOpen&&!settings.Collapsed;
        var forceHealth=healthRefreshRequested;healthRefreshRequested=false;
        var opened=expanded&&!wasExpanded;wasExpanded=expanded;
        using(performance.Measure(HubService.Discovery))discovery.Refresh(forceHealth||opened,expanded);
        bool healthChanged,compatibilityChanged;
        using(performance.Measure(HubService.Health))healthChanged=health.Tick(forceHealth||opened,expanded);
        using(performance.Measure(HubService.Compatibility))compatibilityChanged=compatibility.Tick(forceHealth,settings.LatencyMode!=Utilities.LatencyMode.Off,settings.QuestConvenienceEnabled,expanded&&settings.Page==Configuration.HubPage.Utilities);
        using(performance.Measure(HubService.AntiAfk))antiAfk.Tick();
        using(performance.Measure(HubService.Quest))quest.Tick();
        using(performance.Measure(HubService.Latency))latency.Tick();
        if(healthChanged||compatibilityChanged)using(performance.Measure(HubService.Activity))activity.Observe(health.View,compatibility.View);
        using(performance.Measure(HubService.Mounts))mounts.Tick(expanded&&settings.Page==Configuration.HubPage.Mounts);
        using(performance.Measure(HubService.Fashion))fashion.Tick(expanded&&settings.Page==Configuration.HubPage.Fashion);
        using(performance.Measure(HubService.Menu))identifyMenu.Tick();
        // The menu may have opened/restored Hub during this tick; evaluate visibility after consuming it.
        using(performance.Measure(HubService.Inspector))inspector.Tick(window.IsOpen&&!settings.Collapsed&&settings.Page==Configuration.HubPage.PlayerInspector);
        using(performance.Measure(HubService.Profiles))profiles.Tick(expanded&&settings.Page==Configuration.HubPage.Profiles);
        if(exportRequested){exportRequested=false;using(performance.Measure(HubService.Export))WriteExport();}
    }
    private void DrawCollectionIcon(uint id, Vector2 size)
    {
        Dalamud.Interface.Textures.TextureWraps.IDalamudTextureWrap? wrap = null;
        try { if (Textures.TryGetFromGameIcon(id, out var texture)) texture.TryGetWrap(out wrap, out _); }
        catch { faults.Record(FaultCode.CollectionIconUnavailable); }
        if (wrap is not null) ImGui.Image(wrap.Handle, size);
        else ImGui.Dummy(size);
    }
    private void Open() => window.OpenAndExpand();
    private static string OwnCharacterKey()=>Player.IsLoaded&&Player.ContentId!=0?$"cid:{Player.ContentId:X16}":"";
    private void OnCommand(string _, string arguments)
    {
        var command=arguments.Trim();
        if(command.Equals("perf start",StringComparison.OrdinalIgnoreCase)){performance.Start();Chat.Print("[Sentinel Hub] Performance recording started (RAM only, ten-minute limit).");return;}
        if(command.Equals("perf report",StringComparison.OrdinalIgnoreCase)||command.Equals("perf stop",StringComparison.OrdinalIgnoreCase)){
            if(command.EndsWith("stop",StringComparison.OrdinalIgnoreCase))performance.Stop();
            foreach(var line in performance.Report())Chat.Print("[Sentinel Hub] "+line);return;
        }
        if (arguments.Trim().Equals("export", StringComparison.OrdinalIgnoreCase)) Export();
        else Open();
    }
    private void Save()
    {
        try { Pi.SavePluginConfig(settings); }
        catch { faults.Record(FaultCode.ConfigurationSaveUnavailable); }
    }
    private void Export(){exportRequested=true;window.Notice="Sentinel diagnostics export queued.";}
    private void WriteExport()
    {
        try
        {
            discovery.Refresh(true);
            var directory = Path.Combine(Pi.GetPluginConfigDirectory(), "diagnostics");
            health.Tick(true);
            var bundle = DiagnosticsBundle.Create(discovery.Snapshot,health.View,faults.Snapshot(),
                new ProviderDiagnosticsReader(Pi,OwnCharacterKey).Read(),settings.Version,Client.TerritoryType,Objects.LocalPlayer?.CurrentWorld.RowId??0,
                antiAfk.View,quest.View,latency.View,compatibility.View,activity.Journal.Snapshot(DateTimeOffset.UtcNow));
            DiagnosticsFile.Save(directory,bundle);
            activity.Add(ActivityCode.DiagnosticsSaved);
            window.Notice = "Saved " + DiagnosticsFile.FileName + " in Hub's diagnostics folder (replaces the previous export).";
            Chat.Print("[Sentinel Hub] " + window.Notice);
        }
        catch
        {
            faults.Record(FaultCode.ExportUnavailable);
            activity.Add(ActivityCode.DiagnosticsFailed);
            window.Notice = "Export could not be saved. Check configuration-folder access.";
        }
    }
    public void Dispose()
    {
        disposed = true;
        Framework.Update -= Update;
        Client.Login-=InvalidateSession;Client.Logout-=OnLogout;Client.TerritoryChanged-=OnTerritory;
        performance.Stop();profiles.Dispose();
        identifyMenu.Dispose();
        inspector.Clear();
        latency.Dispose();
        quest.Dispose();
        antiAfk.Dispose();
        operationsIpc.Dispose();
        publicCatalog.Dispose();
        fashion.Dispose();
        mounts.Dispose();
        Pi.UiBuilder.Draw -= Draw;
        Pi.UiBuilder.OpenConfigUi -= Open;
        Pi.UiBuilder.OpenMainUi -= Open;
        Commands.RemoveHandler("/shub");
        discovery.Dispose();
        windows.RemoveAllWindows();
        window.Dispose();
    }
}

