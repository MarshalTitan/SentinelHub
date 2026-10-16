using System.Text.Json;
using System.Text.Json.Serialization;
using SentinelHub.Ecosystem;
namespace SentinelHub.Diagnostics;
public static class DiagnosticExport
{
    // Build a projection from allowlisted IDs/versions/enums. No config, paths, chat, names,
    // exceptions, IPC payload strings, credentials or arbitrary installed-plugin inventory.
    public static string Serialize(HubSnapshot snapshot, IEnumerable<Fault> faults, SentinelHub.Utilities.AntiAfkView? antiAfk = null, SentinelHub.Utilities.QuestConvenienceView? quest = null, SentinelHub.Utilities.LatencyView? latency = null)
    {
        var known = snapshot.Plugins.Where(p => Catalog.Find(p.Component.Id) is not null)
            .Select(p => new { Id = p.Component.Id, p.State, Version = StatusPolicy.NumericVersion(p.Version) });
        var dependencies = snapshot.Dependencies.Where(d => Catalog.Dependencies.Any(c => c.Id == d.Dependency.Id))
            .Select(d => new { Id = d.Dependency.Id, d.State, d.Response, Version = StatusPolicy.NumericVersion(d.Version) });
        return JsonSerializer.Serialize(new
        {
            Schema = "sentinel.hub.v1", HubVersion = StatusPolicy.NumericVersion(typeof(DiagnosticExport).Assembly.GetName().Version?.ToString()), snapshot.CapturedAt, snapshot.DiscoveryAvailable,
            Plugins = known, Dependencies = dependencies,
            AntiAfk = new { State = antiAfk is not null && Enum.IsDefined(antiAfk.State) ? antiAfk.State.ToString() : "Unknown", Cycles = Math.Clamp(antiAfk?.Cycles ?? 0, 0, long.MaxValue) },
            QuestConvenience = new { State = quest is not null && Enum.IsDefined(quest.State) ? quest.State.ToString() : "Unknown" },
            Latency = new {
                State = latency is not null && Enum.IsDefined(latency.State) ? latency.State.ToString() : "Unknown",
                Samples = Math.Clamp(latency?.Samples ?? 0, 0, long.MaxValue),
                Adjustments = Math.Clamp(latency?.Adjustments ?? 0, 0, long.MaxValue),
                LastResponseMs = SafeNumber(latency?.LastResponseMs ?? 0, 500),
                AverageResponseMs = SafeNumber(latency?.AverageResponseMs ?? 0, 500),
                LastReductionMs = SafeNumber(latency?.LastReductionMs ?? 0, 250),
                TotalReductionMs = SafeNumber(latency?.TotalReductionMs ?? 0, 1_000_000_000_000),
            },
            Profiles = new { Status = "Unknown", Reason = "No public status contract" },
            Faults = faults.TakeLast(64).Where(f => Enum.IsDefined(f.Code)),
        }, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } });
    }
    private static double SafeNumber(double value, double maximum) => double.IsFinite(value) ? Math.Clamp(value, 0, maximum) : 0;
}
