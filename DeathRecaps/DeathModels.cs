using System.Text.Json;
namespace SentinelHub.DeathRecaps;

public enum DeathPopupMode { Off, Both, PvE, PvP }
public sealed class DeathSettings
{
    public bool Enabled { get; set; } = true;
    public bool PartyDeaths { get; set; }
    public bool DetailedCapture { get; set; }
    public bool CaptureStatuses { get; set; } = true;
    public bool HideNames { get; set; } = true;
    public bool SaveNames { get; set; }
    public int ReviewSeconds { get; set; } = 30;
    public int ThreatSeconds { get; set; } = 15;
    public int MaximumHistory { get; set; } = 30;
    public DeathPopupMode Popup { get; set; } = DeathPopupMode.Both;
    public bool KeepPopupOpen { get; set; } = true;
    public void Normalize()
    {
        if (ReviewSeconds is not (10 or 30 or 60)) ReviewSeconds = 30;
        if (ThreatSeconds is not (15 or 30)) ThreatSeconds = 15;
        MaximumHistory = Math.Clamp(MaximumHistory, 5, 100);
        if (!Enum.IsDefined(Popup)) Popup = DeathPopupMode.Both;
    }
}
public enum RecapEventKind { Vitals, Damage, Healing, Status }
public sealed record RecapStatus(uint Id, string Name, byte Stacks);
public sealed record RecapEvent(DateTimeOffset At, long Order, RecapEventKind Kind, string Source, uint SourceJob,
    uint ActionId, string Ability, uint? Amount, uint? Hp, uint? MaxHp, byte? ShieldPercent,
    RecapStatus[] Statuses, string Evidence, bool SourceIsPlayer = false);
public sealed record RecapFatal(string Source, string Ability, uint? Amount, uint? HpBefore, uint? ShieldBefore, uint? Overkill);
public sealed record DeathRecord(Guid Id, DateTimeOffset At, uint Territory, string Duty, string Character, uint Job,
    bool Local, bool PvP, RecapEvent[] Events, RecapFatal? Fatal, ThreatDeath? Threat, string Availability, bool Synthetic = false);
public sealed record DeathView(DeathRecord[] History, DeathRecord? Selected, string Capture, string Hud, bool Preview)
{
    public static readonly DeathView Empty = new([], null, "Waiting for a duty.", "HUD unavailable", false);
}
// IPC v1: deaths only. IDs are opaque per-life tokens, never game entity/content identifiers.
public sealed record ThreatActor(int Token, string Name, uint JobId, string Job, string Classification);
public sealed record ThreatFrame(DateTimeOffset At, ThreatActor[] Targeters);
public sealed record ThreatDeath(Guid Id, uint Territory, uint LocalEntityId, DateTimeOffset At,
    DateTimeOffset LastSampleAt, DateTimeOffset WindowStart, ThreatFrame[] Changes, string Mode);
public sealed record ThreatEnvelope(int Version, Guid Session, ThreatDeath[] Deaths);
public sealed record ThreatJob(uint Id, string Job, int Count);
public sealed record ThreatSummary(int NearDeath, int Maximum, ThreatJob[] Jobs, double SampleAgeSeconds,
    double ObservedSeconds, bool Fresh, ThreatFrame[] Changes);

public static class DeathPolicy
{
    public const int MaximumEvents = 512;
    public static uint DecodeAmount(ushort value, byte high, byte flags) => value + ((flags & 0x40) != 0 ? (uint)high << 16 : 0);
    public static uint? Overkill(uint damage, uint? exactHp, uint? exactShield) => exactHp.HasValue && exactShield.HasValue
        ? (uint)Math.Max(0L, (long)damage - exactHp.Value - exactShield.Value) : null;
    public static ThreatSummary Summarize(ThreatDeath d, int seconds)
    {
        var cutoff = d.At.AddSeconds(-Math.Clamp(seconds, 15, 30));
        var frames = d.Changes.OrderBy(f => f.At).ToArray();
        var baseline = frames.LastOrDefault(f => f.At <= cutoff);
        var relevant = (baseline is null ? frames.Where(f => f.At > cutoff) : new[] { baseline }.Concat(frames.Where(f => f.At > cutoff)))
            .Where(f => f.At <= d.At).ToArray();
        var last = relevant.LastOrDefault();
        var age = Math.Max(0, (d.At - d.LastSampleAt).TotalSeconds);
        var fresh = age <= 1 && d.LastSampleAt <= d.At.AddMilliseconds(250);
        var jobs = (last?.Targeters ?? []).GroupBy(a => a.JobId).Select(g => new ThreatJob(g.Key, g.First().Job, g.Count())).ToArray();
        return new(fresh ? last?.Targeters.Length ?? 0 : 0, relevant.Select(f => f.Targeters.Length).DefaultIfEmpty().Max(), jobs,
            age, Math.Max(0, (d.LastSampleAt - (d.WindowStart > cutoff ? d.WindowStart : cutoff)).TotalSeconds), fresh, relevant);
    }
    public static ThreatEnvelope? ParseThreat(string json)
    {
        if (json.Length > 1_000_000) return null;
        try {
            var e = JsonSerializer.Deserialize<ThreatEnvelope>(json);
            if (e is null || e.Version != 1 || e.Deaths is null || e.Deaths.Length > 4) return null;
            foreach (var d in e.Deaths) {
                if (d.Changes is null || d.Changes.Length > 256 || d.At < d.LastSampleAt.AddMilliseconds(-250)
                    || d.At - d.WindowStart > TimeSpan.FromSeconds(32) || d.WindowStart > d.At || d.LastSampleAt < d.WindowStart) return null;
                DateTimeOffset? previous = null;
                foreach (var f in d.Changes) {
                    if (f.Targeters is null || f.Targeters.Length > 72 || f.At > d.At || f.At < d.WindowStart.AddMilliseconds(-250)
                        || previous > f.At || f.Targeters.Any(a => a.Token <= 0 || a.Name is null || a.Name.Length > 128 || a.Job is null || a.Job.Length > 24)) return null;
                    previous = f.At;
                }
            }
            return e;
        } catch { return null; }
    }
    public static DeathRecord Redact(DeathRecord r, bool saveNames)
    {
        var threat = r.Threat is null ? null : r.Threat with { LocalEntityId = 0, Changes = r.Threat.Changes.Select(f => f with {
            Targeters = f.Targeters.Select(a => saveNames ? a : a with { Name = "Enemy " + a.Token }).ToArray() }).ToArray() };
        return r with { Character = saveNames ? r.Character : r.Local ? "You" : "Party member", Threat = threat,
            Events = saveNames ? r.Events : r.Events.Select(e => e.SourceIsPlayer ? e with { Source = "Player" } : e).ToArray() };
    }
}
