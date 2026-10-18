namespace SentinelHub.Utilities;
public enum LatencyMode { Off, Measure, Compensate }
public enum LatencyState { Off, WaitingForLogin, Paused, NoClippyConflict, XivAlexanderConflict, ConflictCheckUnavailable, Measuring, Compensating, UnsupportedClient, Contested, Faulted }
public sealed record LatencyView(LatencyState State, long Samples = 0, long Adjustments = 0,
    double LastResponseMs = 0, double AverageResponseMs = 0, double LastReductionMs = 0, double TotalReductionMs = 0)
{
    public static LatencyView Off => new(LatencyState.Off);
    public string Description => State switch
    {
        LatencyState.Off => "Off; no latency hooks are active.",
        LatencyState.WaitingForLogin => "Waiting for a logged-in character.",
        LatencyState.Paused => "Paused by readiness or Sentinel safety controls.",
        LatencyState.NoClippyConflict => "Blocked: NoClippy is loaded. Keep only one compensation engine active.",
        LatencyState.XivAlexanderConflict => "Blocked: XivAlexander runtime or loader detected.",
        LatencyState.ConflictCheckUnavailable => "Paused: conflict detection is unavailable.",
        LatencyState.Measuring => "Measure only; observing action responses without changing animation locks.",
        LatencyState.Compensating => "Active; bounded instant-action response compensation.",
        LatencyState.UnsupportedClient => "Native bindings unavailable. Set Off before retrying after an update.",
        LatencyState.Contested => "Unexpected animation lock detected; compensation stopped. Set Off before retrying.",
        _ => "Latency module stopped after an error. Set Off before retrying.",
    };
}
public static class LatencyConflictPolicy
{
    public static bool IsNoClippy(string id, bool loaded) =>
        loaded && id.StartsWith("NoClippy", StringComparison.OrdinalIgnoreCase);
    public static bool IsAlexander(string name, bool initializeExport, bool injectExport, bool enableExport) =>
        name.Equals("XivAlexander64.dll", StringComparison.OrdinalIgnoreCase)
        || name.Equals("XivAlexander32.dll", StringComparison.OrdinalIgnoreCase)
        || name.Equals("XivAlexanderLoader64.dll", StringComparison.OrdinalIgnoreCase)
        || name.Equals("XivAlexanderLoader32.dll", StringComparison.OrdinalIgnoreCase)
        || initializeExport || injectExport && enableExport;
}
public static class LatencyActionPolicy
{
    public static bool IsEligible(uint category, int cast100ms) => cast100ms == 0 && category is 2 or 3 or 4;
}
public readonly record struct LockDecision(bool Write, float RemainingLock);
public sealed class LatencySession
{
    public const float ResponseBufferSeconds = 0.040f;
    public const float MaximumReductionSeconds = 0.250f;
    public const long MaximumResponseMilliseconds = 500;
    private ushort sequence;
    private uint action;
    private long sentAt;
    private float initialLock;
    private bool awaitingWrite;
    public LatencyView View { get; private set; }
    public LatencyMode Mode { get; }
    public LatencySession(LatencyMode mode)
    {
        Mode = mode;
        View = new(mode == LatencyMode.Measure ? LatencyState.Measuring : mode == LatencyMode.Compensate ? LatencyState.Compensating : LatencyState.Off);
    }
    public void ClearPending() { sequence = 0; action = 0; sentAt = 0; initialLock = 0; awaitingWrite = false; }
    public void Stop(LatencyState state) { ClearPending(); View = View with { State = state }; }
    public bool Matches(ushort nextSequence, uint actionId) => sequence != 0 && sequence == nextSequence && action == actionId;
    public bool Track(ushort nextSequence, uint actionId, float predictedLock, long now)
    {
        ClearPending();
        if (Mode == LatencyMode.Off || View.State is not (LatencyState.Measuring or LatencyState.Compensating)
            || nextSequence == 0 || actionId == 0 || !float.IsFinite(predictedLock) || Math.Abs(predictedLock - 0.5f) > 0.002f)
            return false;
        sequence = nextSequence; action = actionId; initialLock = predictedLock; sentAt = now;
        return true;
    }
    // Called only for the local actor after the game's unchanged effect handler returns.
    public LockDecision Receive(ushort responseSequence, uint actionId, float wireLock, float beforeLock, float afterLock, long now)
    {
        if (sequence == 0 || responseSequence != sequence || actionId != action
            || View.State is not (LatencyState.Measuring or LatencyState.Compensating)) return default;
        var elapsedMs = now - sentAt;
        var predicted = initialLock;
        ClearPending(); // One response can never be applied twice.
        if (elapsedMs <= 0 || elapsedMs > MaximumResponseMilliseconds
            || !float.IsFinite(wireLock) || wireLock < 0.5f || wireLock > 0.8f
            || !float.IsFinite(beforeLock) || beforeLock <= 0 || beforeLock > 0.502f
            || !float.IsFinite(afterLock)) return default;
        // Another native writer, changed packet layout, or already-adjusted response: fail closed.
        var milliseconds = wireLock * 1000f;
        if (Math.Abs(afterLock - wireLock) > 0.001f
            || Math.Abs(milliseconds - MathF.Round(milliseconds / 10f) * 10f) > 0.5f)
        { Stop(LatencyState.Contested); return default; }
        // Reject samples inconsistent with the countdown observed for this exact request.
        if (Math.Abs(beforeLock - Math.Max(0, predicted - elapsedMs / 1000f)) > 0.075f) return default;
        var reduction = Math.Min(MaximumReductionSeconds, Math.Max(0, elapsedMs / 1000f - ResponseBufferSeconds));
        var remaining = Math.Max(ResponseBufferSeconds, wireLock - reduction);
        var samples = View.Samples < long.MaxValue ? View.Samples + 1 : View.Samples;
        var write = Mode == LatencyMode.Compensate && reduction > 0.0005f;
        View = View with {
            Samples = samples,
            LastResponseMs = elapsedMs,
            AverageResponseMs = View.Samples == 0 ? elapsedMs : View.AverageResponseMs * 0.8 + elapsedMs * 0.2,
            LastReductionMs = reduction * 1000,
        };
        awaitingWrite = write;
        return new(write, remaining);
    }
    public void RecordApplied()
    {
        if (!awaitingWrite || Mode != LatencyMode.Compensate || View.State != LatencyState.Compensating) return;
        awaitingWrite = false;
        View = View with {
            Adjustments = View.Adjustments < long.MaxValue ? View.Adjustments + 1 : View.Adjustments,
            TotalReductionMs = View.TotalReductionMs + View.LastReductionMs,
        };
    }
}
// The native adapter uses this transaction as well: observer faults cannot skip or replay Original.
public interface ILatencyInterceptedCall
{
    void Before();
    bool Original();
    void After(bool result);
    void Fault();
}
public static class LatencyInterception
{
    public static bool Invoke<T>(ref T call) where T : struct, ILatencyInterceptedCall
    {
        var observed = false;
        try { call.Before(); observed = true; } catch { TryFault(ref call); }
        var result = call.Original();
        if (observed) { try { call.After(result); } catch { TryFault(ref call); } }
        return result;
    }
    private static void TryFault<T>(ref T call) where T : struct, ILatencyInterceptedCall
    { try { call.Fault(); } catch { /* Never obstruct the original game call. */ } }
}
public interface ILatencyRuntime : IDisposable { LatencyView View { get; } }
public sealed class LatencyLifecycle(Func<LatencyMode, ILatencyRuntime> create) : IDisposable
{
    private ILatencyRuntime? runtime;
    private LatencyMode mode;
    private LatencyState? latched;
    private bool disposed;
    public LatencyView View { get; private set; } = LatencyView.Off;
    private void Stop()
    {
        var old = runtime; runtime = null;
        if (old is null) return;
        try { View = old.View; } finally { old.Dispose(); }
    }
    public void Tick(LatencyMode requested, LatencyState readiness)
    {
        if (disposed) return;
        if (!Enum.IsDefined(requested)) requested = LatencyMode.Off;
        try
        {
            if (requested == LatencyMode.Off)
            { Stop(); mode = requested; latched = null; View = View with { State = LatencyState.Off }; return; }
            if (latched is { } state) { View = View with { State = state }; return; }
            if (readiness != LatencyState.Compensating)
            { Stop(); View = View with { State = readiness }; return; }
            if (runtime is not null && mode != requested) Stop();
            if (runtime is null) { mode = requested; runtime = create(requested); }
            View = runtime.View;
            if (View.State is LatencyState.Contested or LatencyState.Faulted)
            { latched = View.State; Stop(); View = View with { State = latched.Value }; }
            else if (View.State is not (LatencyState.Measuring or LatencyState.Compensating)) Stop();
        }
        catch
        {
            try { Stop(); } catch { }
            latched = LatencyState.UnsupportedClient;
            View = View with { State = latched.Value };
        }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { Stop(); } finally { View = View with { State = LatencyState.Off }; }
    }
}

