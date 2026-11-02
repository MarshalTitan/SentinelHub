using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using SentinelHub.Ecosystem;
namespace SentinelHub.DeathRecaps;

public sealed class DeathController : IDisposable
{
    private readonly DeathSettings settings;
    private readonly IClientState client;
    private readonly ICondition condition;
    private readonly IObjectTable objects;
    private readonly IPartyList party;
    private readonly IPlayerState player;
    private readonly IDataManager data;
    private readonly Func<InstalledPlugin[]> inventory;
    private readonly Func<long> clock;
    private readonly Func<DateTimeOffset> utc;
    private readonly HudDeathBridge hud;
    private readonly IncomingActionCapture incoming;
    private readonly DeathLedger ledger = new();
    private readonly DeathHistory history = new();
    private readonly Dictionary<uint, string> actions = new(), statuses = new();
    private readonly Dictionary<uint, RecapStatus[]> statusSnapshots = new();
    private readonly Dictionary<uint, long> statusDue = new();
    private readonly List<PendingDeath> pending = new(8);
    private readonly string directory;
    private string? path;
    private string duty = "Unknown duty";
    private bool dirty = true, sessionDirty = true, eligible, pvp, disposed, savePending, configChanged;
    private bool nativeEnabled, nativeConflict;
    private long nextSample, saveDue;
    private uint territory;
    private Guid selected;
    private DeathRecord? preview;
    private Task? saveTask;
    private volatile bool persistenceFailed;
    private InstalledPlugin[]? lastInventory;
    private string capture = "Waiting for a duty.";
    private readonly System.Collections.Concurrent.ConcurrentQueue<(string Action, Guid Id)> requests = new();
    public DeathView View { get; private set; } = DeathView.Empty;
    public long Revision { get; private set; }
    public event Action<DeathRecord>? Recorded;
    private sealed record PendingDeath(uint Entity, DeathRecord Record, long Due);
    public DeathController(DeathSettings settings, IDalamudPluginInterface pi, IClientState client, ICondition condition,
        IObjectTable objects, IPartyList party, IPlayerState player, IDataManager data, IGameInteropProvider interop,
        ISigScanner scanner, Func<InstalledPlugin[]> inventory, Func<long>? clock = null, Func<DateTimeOffset>? utc = null)
    {
        this.settings = settings; this.client = client; this.condition = condition; this.objects = objects; this.party = party;
        this.clock = clock ?? (() => Environment.TickCount64); this.utc = utc ?? (() => DateTimeOffset.UtcNow);
        this.player = player; this.data = data; this.inventory = inventory; directory = Path.Combine(pi.GetPluginConfigDirectory(), "death-recaps");
        hud = new(pi); incoming = new(interop, scanner);
        client.Login += Session; client.Logout += Logout; client.TerritoryChanged += Territory;
        condition.ConditionChange += Condition;
    }
    public void Configure() { dirty = true; configChanged = true; }
    public void Request(string action, Guid id = default)
    {
        if (requests.Count < 32) requests.Enqueue((action, id));
    }
    private void Session() { sessionDirty = true; dirty = true; }
    private void Logout(int _, int __) => Session();
    private void Territory(uint _) { dirty = true; ResetCombat(); }
    private void Condition(ConditionFlag flag, bool _)
    {
        if (flag is ConditionFlag.BoundByDuty or ConditionFlag.BoundByDuty56 or ConditionFlag.BoundByDuty95
            or ConditionFlag.BetweenAreas or ConditionFlag.BetweenAreas51 or ConditionFlag.Unconscious) dirty = true;
    }
    public void Tick()
    {
        if (disposed) return;
        if (persistenceFailed) { persistenceFailed = false; capture = "History could not be saved; recaps remain in memory. Check configuration-folder access."; Publish(); }
        if (sessionDirty) {
            sessionDirty = false;
            if (saveTask is { IsCompleted: false }) saveTask.GetAwaiter().GetResult();
            ResetCombat(); Flush(); hud.Reset(); history.Clear(); preview = null; selected = default;
            path = player.IsLoaded && player.ContentId != 0 ? Path.Combine(directory, player.ContentId.ToString("X16") + ".json") : null;
            if (path is not null && File.Exists(path)) {
                try { if (new FileInfo(path).Length <= 8_000_000) history.Load(File.ReadAllText(path), settings.MaximumHistory); }
                catch { capture = "Saved history unavailable; new recaps remain in memory."; }
            }
            Publish();
        }
        if (dirty) {
            dirty = false; settings.Normalize(); history.Trim(settings.MaximumHistory);
            if (configChanged) { configChanged = false; QueueSave(); }
            territory = client.TerritoryType; pvp = client.IsPvPExcludingDen;
            eligible = settings.Enabled && client.IsLoggedIn && path is not null
                && !condition[ConditionFlag.BetweenAreas] && !condition[ConditionFlag.BetweenAreas51]
                && (pvp || condition[ConditionFlag.BoundByDuty] || condition[ConditionFlag.BoundByDuty56] || condition[ConditionFlag.BoundByDuty95]);
            if (!eligible) ResetCombat();
            ResolveDuty(); Publish();
        }
        while (requests.TryDequeue(out var r)) {
            if (r.Action == "select") { selected = r.Id; preview = null; }
            else if (r.Action == "delete") { history.Delete(r.Id); QueueSave(); }
            else if (r.Action == "clear") { history.Clear(); preview = null; QueueSave(); }
            else if (r.Action is "preview-pvp" or "preview-pve") { preview = DeathPreview.Create(r.Action == "preview-pvp"); Publish(); Recorded?.Invoke(preview); }
            else if (r.Action == "end-preview") preview = null;
            Publish();
        }
        if (savePending && clock() >= saveDue && !condition[ConditionFlag.InCombat]) Flush();
        if (!eligible) return;
        var all = inventory();
        if (!ReferenceEquals(lastInventory, all)) {
            lastInventory = all;
            nativeConflict = all.Any(p => p.Loaded && !p.Restricted && (p.Id.Equals("BetterDeaths", StringComparison.OrdinalIgnoreCase) || p.Id.Equals("DeathRecap", StringComparison.OrdinalIgnoreCase)));
        }
        var enable = settings.DetailedCapture && eligible;
        if (enable != nativeEnabled || nativeConflict != lastConflict) { nativeEnabled = enable; lastConflict = nativeConflict; incoming.SetEnabled(enable, nativeConflict); Publish(); }
        var nowTicks = clock();
        // No clock, services, or allocations below this point between the bounded duty samples.
        if (nowTicks < nextSample) return;
        nextSample = nowTicks + 125;
        var now = utc();
        DrainActions();
        Span<uint> watched = stackalloc uint[8]; var count = 0;
        var local = objects.LocalPlayer;
        if (local is not null) { watched[count++] = local.EntityId; Sample(local, true, now); }
        if (!pvp && settings.PartyDeaths) foreach (var member in party) {
            if (count >= 8) break;
            if (member.GameObject is IBattleChara actor && !watched[..count].Contains(actor.EntityId)) { watched[count++] = actor.EntityId; Sample(actor, false, now); }
        }
        ledger.RemoveAbsent(watched[..count]); incoming.Watch(watched[..count]);
        for (var i = pending.Count - 1; i >= 0; i--) {
            var d = pending[i];
            var threat = d.Record.Local && pvp ? hud.Find(d.Record.Territory, d.Entity, d.Record.At, retry: nowTicks >= d.Due) : null;
            if (threat is null && nowTicks < d.Due) continue;
            var events = d.Record.Events.Concat(ledger.Snapshot(d.Entity, d.Record.At.AddMilliseconds(250)))
                .DistinctBy(e => e.Order).OrderBy(e => e.At).ThenBy(e => e.Order).TakeLast(DeathPolicy.MaximumEvents).ToArray();
            // The observation timestamp is explicit; event packets are not proof of a fatal blow.
            var record = d.Record with { Events = events, Threat = threat,
                Availability = "Death observed from alive-to-zero HP transition (125 ms). " + incoming.Availability + (ledger.Truncated || incoming.Dropped > 0 ? " Event buffer reached its bound; history may be incomplete." : "") };
            history.Add(record, settings.MaximumHistory); selected = record.Id; preview = null;
            pending.RemoveAt(i); QueueSave(); Publish(); Recorded?.Invoke(record);
        }
    }
    private bool lastConflict;
    private void Sample(IBattleChara actor, bool local, DateTimeOffset now)
    {
        var status = ReadStatuses(actor);
        if (!ledger.Vitals(actor.EntityId, now, actor.CurrentHp, actor.MaxHp, actor.ShieldPercentage, status)) return;
        if (pending.Count >= 8) return;
        pending.Add(new(actor.EntityId, new(Guid.NewGuid(), now, territory, duty, actor.Name.TextValue, actor.ClassJob.RowId,
            local, pvp, ledger.Snapshot(actor.EntityId, now), null, null, "Death observed from alive-to-zero HP transition (125 ms sampling)."), clock() + (pvp && local ? 1000 : 250)));
    }
    private RecapStatus[] ReadStatuses(IBattleChara actor)
    {
        if (!settings.CaptureStatuses) return [];
        if (!statusSnapshots.ContainsKey(actor.EntityId) && statusSnapshots.Count >= 8) { statusSnapshots.Clear(); statusDue.Clear(); }
        statusSnapshots.TryGetValue(actor.EntityId, out var old);
        var now = clock();
        if (actor.CurrentHp != 0 && statusDue.TryGetValue(actor.EntityId, out var due) && now < due) return old ?? [];
        statusDue[actor.EntityId] = now + 1000;
        // Compare native status IDs/stacks through the supported wrapper. Retain unchanged arrays.
        var list = actor.StatusList; var count = 0; var same = old is not null;
        foreach (var s in list) if (s.StatusId != 0) {
            if (old is null || count >= old.Length || old[count].Id != s.StatusId || old[count].Stacks != s.Param) same = false;
            count++;
        }
        if (same && old!.Length == count) return old;
        var result = new RecapStatus[Math.Min(count, 60)]; var index = 0;
        foreach (var s in list) if (s.StatusId != 0 && index < result.Length) {
            if (!statuses.TryGetValue(s.StatusId, out var name)) {
                name = data.GetExcelSheet<Status>().TryGetRow(s.StatusId, out var row) ? row.Name.ToString() : "Status #" + s.StatusId;
                if (statuses.Count < 512) statuses[s.StatusId] = name;
            }
            result[index++] = new(s.StatusId, name, (byte)s.Param);
        }
        statusSnapshots[actor.EntityId] = result; return result;
    }
    private readonly Queue<(uint Seq, uint Source, uint Target, byte Slot)> seenOrder = new();
    private readonly HashSet<(uint Seq, uint Source, uint Target, byte Slot)> seen = new();
    private void DrainActions()
    {
        for (var n = 0; n < 1024 && incoming.TryRead(out var a); n++) {
            var key = (a.Sequence, a.Source, a.Target, a.Slot);
            if (a.Sequence != 0 && !seen.Add(key)) continue;
            if (a.Sequence != 0) { seenOrder.Enqueue(key); if (seenOrder.Count > 1024) seen.Remove(seenOrder.Dequeue()); }
            var source = objects.SearchByEntityId(a.Source) as ICharacter;
            var name = source?.Name.TextValue ?? "Unknown / no longer observable";
            var job = source?.ClassJob.RowId ?? 0;
            if (!actions.TryGetValue(a.Action, out var ability)) {
                ability = data.GetExcelSheet<Lumina.Excel.Sheets.Action>().TryGetRow(a.Action, out var row) ? row.Name.ToString() : "Action #" + a.Action;
                if (actions.Count < 1024) actions[a.Action] = ability;
            }
            ledger.Action(a.Target, new(a.At, 0, a.Kind, name, job, a.Action, ability, a.Amount, null, null, null, [],
                "Observed action-effect amount; not confirmed HP loss, effective healing, or a verified fatal blow.", source is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter));
        }
    }
    private void ResolveDuty()
    {
        try { var t = data.GetExcelSheet<TerritoryType>().GetRow(territory); var c = t.ContentFinderCondition.Value;
            duty = c.RowId != 0 ? c.Name.ToString() : t.PlaceName.Value.Name.ToString(); if (string.IsNullOrWhiteSpace(duty)) duty = "Unknown duty"; }
        catch { duty = "Unknown duty"; }
        capture = eligible ? "Recording observable HP / shield / status changes in this duty (125 ms)." : settings.Enabled ? "Dormant outside duties / PvP." : "Death Recaps disabled.";
    }
    private void ResetCombat()
    {
        // Preserve deaths already detected even if a territory/logout arrives during the short IPC grace period.
        foreach (var p in pending) { history.Add(p.Record, settings.MaximumHistory); QueueSave(); }
        ledger.Clear(); pending.Clear(); statusSnapshots.Clear(); statusDue.Clear(); seen.Clear(); seenOrder.Clear();
        incoming.SetEnabled(false, false); nativeEnabled = false; nextSample = 0;
    }
    private void Publish() { Revision++; View = new(history.Snapshot, preview ?? history.Snapshot.FirstOrDefault(r => r.Id == selected) ?? history.Snapshot.FirstOrDefault(), capture + " " + incoming.Availability, hud.Availability, preview is not null); }
    private void QueueSave() { savePending = true; saveDue = clock() + 1500; }
    private void Flush()
    {
        if (!savePending || path is null || saveTask is { IsCompleted: false }) return;
        var records = history.Snapshot; var saveNames = settings.SaveNames; var destination = path;
        savePending = false;
        // The snapshot and its records are immutable after publication; worker code never touches game services.
        saveTask = Task.Run(() => { try {
            var json = System.Text.Json.JsonSerializer.Serialize(records.Select(r => DeathPolicy.Redact(r, saveNames)).ToArray());
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            if (bytes.Length > 8_000_000) throw new IOException("Death history bound exceeded");
            SentinelHub.Storage.AtomicFile.Write(destination, bytes);
        } catch { persistenceFailed = true; } });
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        client.Login -= Session; client.Logout -= Logout; client.TerritoryChanged -= Territory; condition.ConditionChange -= Condition;
        incoming.Dispose(); hud.Dispose();
        ResetCombat();
        if (saveTask is { IsCompleted: false }) saveTask.GetAwaiter().GetResult(); Flush(); saveTask?.GetAwaiter().GetResult();
    }
}
