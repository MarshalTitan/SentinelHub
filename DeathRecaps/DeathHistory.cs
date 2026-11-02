using System.Text.Json;
using SentinelHub.Storage;
namespace SentinelHub.DeathRecaps;

public sealed class DeathHistory
{
    private readonly List<DeathRecord> records = new();
    public DeathRecord[] Snapshot { get; private set; } = [];
    public bool Add(DeathRecord record, int limit)
    {
        if (records.Any(r => r.Id == record.Id)) return false;
        records.Insert(0, record); Trim(limit); return true;
    }
    public void Trim(int limit) { if (records.Count > Math.Clamp(limit, 5, 100)) records.RemoveRange(Math.Clamp(limit, 5, 100), records.Count - Math.Clamp(limit, 5, 100)); Snapshot = records.ToArray(); }
    public void Delete(Guid id) { records.RemoveAll(r => r.Id == id); Snapshot = records.ToArray(); }
    public void Clear() { records.Clear(); Snapshot = []; }
    public string Serialize(bool names) => JsonSerializer.Serialize(records.Select(r => DeathPolicy.Redact(r, names)).ToArray());
    public bool Load(string json, int limit)
    {
        if (json.Length > 8_000_000) return false;
        try {
            var read = JsonSerializer.Deserialize<DeathRecord[]>(json);
            if (read is null || read.Length > 100 || read.Any(r => r.Events is null || r.Events.Length > DeathPolicy.MaximumEvents
                || r.Character is null || r.Duty is null || r.Character.Length > 128 || r.Duty.Length > 256
                || r.Events.Any(e => e is null || e.Statuses is null || e.Statuses.Length > 60 || e.Source is null || e.Ability is null || e.Evidence is null
                    || e.Source.Length > 128 || e.Ability.Length > 256 || e.Statuses.Any(s => s is null || s.Name is null || s.Name.Length > 128))
                || r.Threat is { } t && DeathPolicy.ParseThreat(JsonSerializer.Serialize(new ThreatEnvelope(1, Guid.Empty, [t]))) is null)) return false;
            records.Clear(); records.AddRange(read.Where(r => !r.Synthetic).DistinctBy(r => r.Id).OrderByDescending(r => r.At)); Trim(limit); return true;
        } catch { return false; }
    }
    public void Save(string path, bool names) => AtomicFile.Write(path, System.Text.Encoding.UTF8.GetBytes(Serialize(names)));
}
