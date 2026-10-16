using System.Text.Json;
using System.Text.Json.Serialization;
using SentinelHub.Ecosystem;
namespace SentinelHub.Diagnostics;
public static class DiagnosticExport
{
    // Build a projection from allowlisted IDs/versions/enums. No config, paths, chat, names,
    // exceptions, IPC payload strings, credentials or arbitrary installed-plugin inventory.
    public static string Serialize(HubSnapshot snapshot, IEnumerable<Fault> faults, SentinelHub.Utilities.AntiAfkView? antiAfk = null, SentinelHub.Utilities.QuestConvenienceView? quest = null)
    {
        var known = snapshot.Plugins.Where(p => Catalog.Find(p.Component.Id) is not null)
            .Select(p => new { Id = p.Component.Id, p.State, Version = StatusPolicy.NumericVersion(p.Version) });
        var dependencies = snapshot.Dependencies.Where(d => Catalog.Dependencies.Any(c => c.Id == d.Dependency.Id))
            .Select(d => new { Id = d.Dependency.Id, d.State, d.Response, Version = StatusPolicy.NumericVersion(d.Version) });
        return JsonSerializer.Serialize(new
        {
            Schema = "sentinel.hub.v1", HubVersion = "0.3.0.0", snapshot.CapturedAt, snapshot.DiscoveryAvailable,
            Plugins = known, Dependencies = dependencies,
            AntiAfk = new { State = antiAfk is not null && Enum.IsDefined(antiAfk.State) ? antiAfk.State.ToString() : "Unknown", Cycles = Math.Clamp(antiAfk?.Cycles ?? 0, 0, long.MaxValue) },
            QuestConvenience = new { State = quest is not null && Enum.IsDefined(quest.State) ? quest.State.ToString() : "Unknown" },
            Profiles = new { Status = "Unknown", Reason = "No public status contract" },
            Faults = faults.TakeLast(64).Where(f => Enum.IsDefined(f.Code)),
        }, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } });
    }
}
