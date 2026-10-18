using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SentinelHub.Configuration;
using SentinelHub.Diagnostics;
namespace SentinelHub.Utilities.Latency;
public sealed class LatencyController : IDisposable
{
    private readonly IClientState client;
    private readonly ICondition condition;
    private readonly IObjectTable objects;
    private readonly HubSettings settings;
    private readonly FaultJournal faults;
    private readonly LatencyConflictProbe conflicts;
    private readonly LatencyLifecycle lifecycle;
    private uint? territory;
    private HashSet<uint>? instantActions;
    private bool disposed;
    private readonly Func<bool> suspended;
    public LatencyView View => lifecycle.View;
    public LatencyController(IDalamudPluginInterface pi, IGameInteropProvider interop, IDataManager data, IClientState client,
        ICondition condition, IObjectTable objects, HubSettings settings, FaultJournal faults, Func<bool>? suspended=null)
    {
        this.client = client; this.condition = condition; this.objects = objects; this.settings = settings; this.faults = faults;this.suspended=suspended??(()=>false);
        conflicts = new(pi);
        lifecycle = new(mode =>
        {
            // Managed sheet lookup runs only on first enable, never from a native effect callback.
            instantActions ??= data.GetExcelSheet<Lumina.Excel.Sheets.Action>()
                .Where(row => LatencyActionPolicy.IsEligible(row.ActionCategory.RowId, row.Cast100ms))
                .Select(row => row.RowId).ToHashSet();
            if (instantActions.Count == 0) throw new InvalidOperationException("Action metadata unavailable.");
            var scope = client.TerritoryType;
            return new NativeLatencyRuntime(interop, objects, instantActions, mode, () => settings.LatencyMode,
                force => scope == client.TerritoryType ? Readiness(force) : LatencyState.Paused);
        });
    }
    private LatencyState Readiness(bool force)
    {
        if (disposed || settings.LatencyMode == LatencyMode.Off) return LatencyState.Off;
        if (suspended()) return LatencyState.Paused;
        if (!client.IsLoggedIn || objects.LocalPlayer is null) return LatencyState.WaitingForLogin;
        if (objects.LocalPlayer.IsDead || condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51]
            || condition[ConditionFlag.OccupiedInCutSceneEvent] || condition[ConditionFlag.WatchingCutscene] || condition[ConditionFlag.WatchingCutscene78])
            return LatencyState.Paused;
        return conflicts.Read(force);
    }
    public void Tick()
    {
        if (disposed) return;
        var mode = Enum.IsDefined(settings.LatencyMode) ? settings.LatencyMode : LatencyMode.Off;
        LatencyState state;
        try
        {
            if (mode == LatencyMode.Off) state = LatencyState.Off; // No native access or conflict scanning while Off.
            else if (territory is { } old && old != client.TerritoryType) state = LatencyState.Paused;
            else state = Readiness(false);
            territory = client.TerritoryType;
        }
        catch { state = LatencyState.ConflictCheckUnavailable; }
        lifecycle.Tick(mode, state);
        if (View.State is LatencyState.Faulted or LatencyState.UnsupportedClient or LatencyState.Contested or LatencyState.ConflictCheckUnavailable)
            faults.Record(FaultCode.LatencyUnavailable);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { lifecycle.Dispose(); } catch { faults.Record(FaultCode.LatencyUnavailable); }
    }
}

