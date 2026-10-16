using Dalamud.Plugin.Services;
using SentinelHub.Diagnostics;
namespace SentinelHub.Ecosystem;
public sealed class QuickOpen(PluginDiscovery discovery, ICommandManager commands, FaultJournal faults)
{
    public bool Open(string id, bool diagnostics = false)
    {
        var c = Catalog.Find(id);
        try
        {
            // Fresh loaded-state check closes the unload-between-refresh-and-click race.
            if (!StatusPolicy.CanOpen(c, discovery.Capture(), true)) return false;
            var command = diagnostics ? c!.DiagnosticsCommand : c!.SettingsCommand;
            if (command is null || !commands.ProcessCommand(command)) return false;
            return true;
        }
        catch { faults.Record(FaultCode.OpenSettingsUnavailable); return false; }
    }
}
