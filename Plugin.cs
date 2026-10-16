using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SentinelHub.Diagnostics;
using SentinelHub.Ecosystem;
using SentinelHub.UI;
namespace SentinelHub;
public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Pi { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    private readonly WindowSystem windows = new("SentinelHub");
    private readonly Configuration.Configuration settings;
    private readonly FaultJournal faults = new();
    private readonly PluginDiscovery discovery;
    private readonly MainWindow window;
    private readonly QuickOpen quickOpen;
    private bool disposed;
    public Plugin()
    {
        settings = Pi.GetPluginConfig() as Configuration.Configuration ?? new();
        settings.Normalize();
        discovery = new(Pi, faults);
        quickOpen = new(discovery, Commands, faults);
        window = new(settings, () => discovery.Snapshot, faults, Save, () => discovery.Refresh(true),
            (id, diagnostics) => quickOpen.Open(id, diagnostics), Export,
            () => Pi.UiBuilder.ShouldUseReducedMotion);
        windows.AddWindow(window);
        Commands.AddHandler("/shub", new CommandInfo(OnCommand) { HelpMessage = "Open Sentinel Hub. /shub export saves sanitized Hub diagnostics." });
        Pi.UiBuilder.Draw += windows.Draw;
        Pi.UiBuilder.OpenConfigUi += Open;
        Pi.UiBuilder.OpenMainUi += Open;
        Framework.Update += Update;
        discovery.Refresh(true);
    }
    private void Update(IFramework _) { if (!disposed) discovery.Refresh(); }
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
            var name = "sentinel-hub-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json";
            File.WriteAllText(Path.Combine(directory, name), DiagnosticExport.Serialize(discovery.Snapshot, faults.Snapshot()));
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
        Framework.Update -= Update;
        Pi.UiBuilder.Draw -= windows.Draw;
        Pi.UiBuilder.OpenConfigUi -= Open;
        Pi.UiBuilder.OpenMainUi -= Open;
        Commands.RemoveHandler("/shub");
        discovery.Dispose();
        windows.RemoveAllWindows();
        window.Dispose();
    }
}
