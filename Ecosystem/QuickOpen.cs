using SentinelHub.Diagnostics;
namespace SentinelHub.Ecosystem;
public sealed class QuickOpen(Func<IEnumerable<InstalledPlugin>> capture, Func<string, bool> process, FaultJournal faults)
{
    public bool Open(string id, bool diagnostics = false)
    {
        var c = Catalog.Find(id);
        try
        {
            // Fresh observation; never replay a stale UI snapshot after consumer unload.
            if (!StatusPolicy.CanOpen(c, capture(), true)) return false;
            var command = diagnostics ? c!.DiagnosticsCommand : c!.SettingsCommand;
            if (command is null) return false;
            if (process(command)) return true;
            faults.Record(FaultCode.OpenSettingsUnavailable);
            return false;
        }
        catch { faults.Record(FaultCode.OpenSettingsUnavailable); return false; }
    }
}
