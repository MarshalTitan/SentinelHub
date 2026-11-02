using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using SentinelCore.UI;
using SentinelHub.DeathRecaps;
namespace SentinelHub.UI;

public sealed class DeathRecapsPanel(DeathSettings settings, Func<DeathView> view, Action<string, Guid> request,
    Action changed, Action save, Action<uint, Vector2>? icon = null)
{
    private DeathRecord? presented;
    private DeathRecord[]? history;
    private bool privacy, confirmClear;
    private int seconds, threatSeconds;
    private string title = "", fatal = "", context = "", totals = "";
    private string[] timeline = [], damage = [], intervals = [], historyLabels = [];
    private ThreatSummary? threat;
    public void Draw()
    {
        Toggle("Enable Death Recaps", settings.Enabled, v => settings.Enabled = v);
        var v = view();
        ImGui.TextWrapped(v.Capture); ImGui.TextWrapped(v.Hud);
        if (ImGui.Button("Preview PvP recap")) request("preview-pvp", default);
        if (ImGui.Button("Preview PvE recap")) request("preview-pve", default);
        if (v.Preview && ImGui.Button("End preview")) request("end-preview", default);
        ImGui.TextDisabled("Preview data is synthetic and is never saved.");
        ImGui.Separator();
        var choices = new[] { "Last 10 seconds", "Last 30 seconds", "Last 60 seconds" };
        var review = settings.ReviewSeconds == 10 ? 0 : settings.ReviewSeconds == 30 ? 1 : 2;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.Combo("##RecapReview", ref review, choices, choices.Length)) { settings.ReviewSeconds = review == 0 ? 10 : review == 1 ? 30 : 60; save(); }
        Refresh(v);
        Summary(v, false);
        if (v.Selected is { } selected) {
            if (ImGui.CollapsingHeader("Damage and healing timeline", ImGuiTreeNodeFlags.DefaultOpen)) {
                ImGui.TextWrapped("Chronological observations. Snapshot HP/shield changes do not prove who caused them; action-effect amounts are not confirmed HP loss or effective healing.");
                foreach (var line in timeline) ImGui.TextWrapped(line);
                if (timeline.Length == 0) ImGui.TextDisabled("No events were observed in this review window.");
            }
            if (selected.PvP && ImGui.CollapsingHeader("PvP threat analysis", ImGuiTreeNodeFlags.DefaultOpen)) Threat(selected, false);
            ImGui.TextWrapped("Capture availability: " + selected.Availability);
            if (ImGui.Button("Delete selected recap")) request("delete", selected.Id);
        }
        if (ImGui.CollapsingHeader("History", ImGuiTreeNodeFlags.DefaultOpen)) {
            for (var i = 0; i < v.History.Length; i++) if (ImGui.Selectable(historyLabels[i], v.Selected?.Id == v.History[i].Id)) request("select", v.History[i].Id);
            if (v.History.Length == 0) ImGui.TextDisabled("No recorded deaths yet.");
            if (ImGui.Button("Clear history...")) confirmClear = true;
            if (confirmClear) {
                ImGui.TextWrapped("Delete all saved death recaps for this character?");
                if (ImGui.Button("Confirm clear")) { request("clear", default); confirmClear = false; }
                if (ImGui.Button("Cancel clear")) confirmClear = false;
            }
        }
        if (ImGui.CollapsingHeader("Recap settings")) Settings();
    }
    public void Summary(DeathView v, bool compact)
    {
        Refresh(v);
        if (v.Selected is not { } selected) { ImGui.TextWrapped("No death recap selected."); return; }
        var scale = ImGuiHelpers.GlobalScale;
        var width = Math.Max(80, ImGui.GetContentRegionAvail().X - 28 * scale);
        var height = ImGui.CalcTextSize(title, false, width).Y + ImGui.CalcTextSize(context, false, width).Y
            + ImGui.CalcTextSize(fatal, false, width).Y + ImGui.CalcTextSize(totals, false, width).Y + ImGui.GetStyle().ItemSpacing.Y * 5 + 28 * scale;
        using (var card = SentinelModernGlassCard.Begin("##DeathSummary", new() { Size = new(0, height / scale) }, scale)) {
            if (card.IsVisible) {
                ImGui.PushStyleColor(ImGuiCol.Text, SentinelModernPalette.Accent); ImGui.TextWrapped(title); ImGui.PopStyleColor();
                ImGui.TextWrapped(context); ImGui.TextWrapped(fatal); ImGui.TextWrapped(totals);
            }
        }
        if (selected.Synthetic) ImGui.TextColored(new Vector4(1, .75f, .3f, 1), "SYNTHETIC PREVIEW");
        if (compact && selected.PvP) Threat(selected, true);
    }
    private void Threat(DeathRecord selected, bool compact)
    {
        if (selected.Threat is null || threat is null) { ImGui.TextWrapped("Observed targeters: Unavailable. HUD must be loaded with its Targeting Me Counter active before death."); return; }
        ImGui.TextWrapped(threat.Fresh ? $"Enemies targeting you near death: {threat.NearDeath}" : "Targeters near death: Unavailable (last sample is stale).");
        ImGui.TextWrapped($"Maximum simultaneous observed targeters: {threat.Maximum}");
        ImGui.TextWrapped($"Observed coverage: {threat.ObservedSeconds:F1}s of requested {settings.ThreatSeconds}s; final sample {threat.SampleAgeSeconds:F2}s before HUD death detection.");
        foreach (var group in threat.Jobs) {
            if (icon is not null && group.Id is > 0 and < 100) { icon(62000 + group.Id, new(24 * ImGuiHelpers.GlobalScale)); ImGui.SameLine(); }
            ImGui.TextWrapped($"{group.Job} ×{group.Count}");
        }
        ImGui.TextWrapped("Observed targeters are not confirmed attackers or killers. Counts describe hard-target observations, not every enemy that may have damaged you.");
        if (compact) return;
        if (ImGui.TreeNode("Confirmed damage sources (captured action effects)")) {
            if (damage.Length == 0) ImGui.TextWrapped("Unavailable — no attributable damaging action effects were captured.");
            foreach (var line in damage) ImGui.TextWrapped(line);
            ImGui.TreePop();
        }
        if (ImGui.TreeNode("Observed targeters and approximate focus intervals")) {
            foreach (var line in intervals) ImGui.TextWrapped(line);
            ImGui.TreePop();
        }
    }
    private void Refresh(DeathView v)
    {
        if (!ReferenceEquals(history, v.History) || privacy != settings.HideNames) {
            history = v.History;
            historyLabels = v.History.Select(r => $"{r.At.ToLocalTime():g} — {(settings.HideNames ? r.Local ? "You" : "Party member" : r.Character)} — {r.Duty}##{r.Id}").ToArray();
        }
        if (ReferenceEquals(presented, v.Selected) && privacy == settings.HideNames && seconds == settings.ReviewSeconds && threatSeconds == settings.ThreatSeconds) return;
        presented = v.Selected; privacy = settings.HideNames; seconds = settings.ReviewSeconds; threatSeconds = settings.ThreatSeconds;
        if (v.Selected is not { } r) return;
        title = r.PvP ? r.Threat?.Mode.StartsWith("Frontline", StringComparison.OrdinalIgnoreCase) == true ? "FRONTLINE — DEATH RECAP" : "PVP — DEATH RECAP" : "PVE — DEATH RECAP";
        context = $"{r.At.ToLocalTime():G} — {r.Duty}\n{(privacy ? r.Local ? "You" : "Party member" : r.Character)} · Job {r.Job}";
        fatal = r.Fatal is { } fatalEvent ? $"Confirmed fatal event: {fatalEvent.Ability} — {fatalEvent.Source}\nDamage: {Value(fatalEvent.Amount)} · HP before: {Value(fatalEvent.HpBefore)} · Shield HP before: {Value(fatalEvent.ShieldBefore)} · Overkill: {Value(fatalEvent.Overkill)}"
            : "Confirmed fatal blow: Unknown\nExact pre-hit HP, shield HP and overkill: Unavailable";
        var events = r.Events.Where(e => e.At >= r.At.AddSeconds(-seconds)).OrderBy(e => e.At).ThenBy(e => e.Order).ToArray();
        var hits = events.Where(e => e.Kind == RecapEventKind.Damage && e.Amount.HasValue).ToArray();
        var heals = events.Where(e => e.Kind == RecapEventKind.Healing && e.Amount.HasValue).ToArray();
        totals = "Captured action-effect damage: " + (hits.Length == 0 ? "Unavailable" : hits.Sum(e => (long)e.Amount!.Value).ToString("N0"))
            + " · healing: " + (heals.Length == 0 ? "Unavailable" : heals.Sum(e => (long)e.Amount!.Value).ToString("N0"))
            + "\nCoverage may be incomplete; shield percentage is an observed percentage, not exact barrier HP.";
        timeline = events.Select(e => $"{(e.At - r.At).TotalSeconds:+0.000;-0.000;0.000}s · {e.Kind} · {e.Ability}"
            + (e.Amount is { } a ? $" · {a:N0} · {(privacy && e.SourceIsPlayer ? "Player" : e.Source)}" : $" · HP {Value(e.Hp)}/{Value(e.MaxHp)} · shield {e.ShieldPercent?.ToString() ?? "?"}%")
            + (e.Statuses.Length > 0 ? "\nStatuses observed: " + string.Join(", ", e.Statuses.Select(s => s.Name + (s.Stacks > 1 ? $" ×{s.Stacks}" : ""))) : "")).ToArray();
        damage = events.Where(e => e.Kind == RecapEventKind.Damage && e.Amount.HasValue).GroupBy(e => (e.Source, e.SourceJob, e.SourceIsPlayer))
            .Select(g => $"{(privacy && g.Key.SourceIsPlayer ? "Player" : g.Key.Source)} · Job {g.Key.SourceJob} · {g.Sum(e => (long)e.Amount!.Value):N0} observed action-effect damage").ToArray();
        threat = r.Threat is { } d ? DeathPolicy.Summarize(d, settings.ThreatSeconds) : null;
        intervals = [];
        if (r.Threat is { } td && threat is { } summary) {
            var rows = new List<string>();
            var end = td.LastSampleAt < td.At ? td.LastSampleAt : td.At;
            foreach (var a in summary.Changes.SelectMany(f => f.Targeters).DistinctBy(a => a.Token)) {
                double duration = 0;
                for (var i = 0; i < summary.Changes.Length; i++) {
                    var f = summary.Changes[i]; var start = f.At < td.At.AddSeconds(-settings.ThreatSeconds) ? td.At.AddSeconds(-settings.ThreatSeconds) : f.At;
                    var stop = i + 1 < summary.Changes.Length ? summary.Changes[i + 1].At : end;
                    if (f.Targeters.Any(t => t.Token == a.Token)) duration += Math.Max(0, (stop - start).TotalSeconds);
                }
                rows.Add($"{(privacy ? "Enemy " + a.Token : a.Name)} · {a.Job} · approximately {duration:F1}s observed targeting · {a.Classification}");
            }
            intervals = rows.ToArray();
        }
    }
    private static string Value(uint? value) => value?.ToString("N0") ?? "Unavailable";
    private void Settings()
    {
        Toggle("Record observable PvE party deaths", settings.PartyDeaths, v => settings.PartyDeaths = v);
        Toggle("Capture status snapshots", settings.CaptureStatuses, v => settings.CaptureStatuses = v);
        Toggle("Experimental incoming action details", settings.DetailedCapture, v => settings.DetailedCapture = v);
        ImGui.TextWrapped("Direct action capture is optional and requires in-game validation. It excludes periodic ticks, source-side effects and unsupported action types. It pauses if another death-recap plugin or an existing Receive detour is detected. Fatal blow attribution remains Unknown.");
        Toggle("Hide player names in recap UI", settings.HideNames, v => settings.HideNames = v);
        Toggle("Retain player names on disk (otherwise redacted)", settings.SaveNames, v => settings.SaveNames = v);
        var maximum = settings.MaximumHistory; ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt("##DeathRetention", ref maximum, 5, 100)) { settings.MaximumHistory = maximum; changed(); save(); }
        ImGui.TextWrapped("Maximum saved deaths per character");
        var window = settings.ThreatSeconds == 30;
        Toggle("Review 30 seconds of PvP targeting (default 15)", window, v => settings.ThreatSeconds = v ? 30 : 15);
        var popup = (int)settings.Popup; ImGui.SetNextItemWidth(-1);
        string[] modes = ["Popup disabled", "Popup in PvE and PvP", "Popup in PvE only", "Popup in PvP only"];
        if (ImGui.Combo("##DeathPopupMode", ref popup, modes, modes.Length)) { settings.Popup = (DeathPopupMode)popup; changed(); save(); }
        Toggle("Keep death popup open until dismissed", settings.KeepPopupOpen, v => settings.KeepPopupOpen = v);
        ImGui.TextWrapped("History stays local. No uploads, live target recommendations, automated targeting, movement or combat actions.");
    }
    private void Toggle(string label, bool value, Action<bool> set) { if (ImGui.Checkbox(label, ref value)) { set(value); changed(); save(); } }
}
