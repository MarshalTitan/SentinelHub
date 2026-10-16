using System.Text.Json;
using System.Text.Json.Serialization;
using SentinelHub.Ecosystem;
namespace SentinelHub.Diagnostics;
public static class DiagnosticExport
{
    // Build a projection from allowlisted IDs/versions/enums. No config, paths, chat, names,
    // exceptions, IPC payload strings, credentials or arbitrary installed-plugin inventory.
    public static string Serialize(HubSnapshot snapshot, IEnumerable<Fault> faults)
    {
        var known = snapshot.Plugins.Where(p => Catalog.Find(p.Component.Id) is not null)
            .Select(p => new { Id = p.Component.Id, p.State, Version = StatusPolicy.NumericVersion(p.Version) });
        var dependencies = snapshot.Dependencies.Where(d => Catalog.Dependencies.Any(c => c.Id == d.Dependency.Id))
            .Select(d => new { Id = d.Dependency.Id, d.State, d.Response, Version = StatusPolicy.NumericVersion(d.Version) });
        return JsonSerializer.Serialize(new
        {
            Schema = "sentinel.hub.v1", HubVersion = "0.1.0.0", snapshot.CapturedAt, snapshot.DiscoveryAvailable,
            Plugins = known, Dependencies = dependencies,
            Profiles = new { Status = "Unknown", Reason = "No public status contract" },
            Faults = faults.TakeLast(64).Where(f => Enum.IsDefined(f.Code)),
        }, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } });
    }
}
