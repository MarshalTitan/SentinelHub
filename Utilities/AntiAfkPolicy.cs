namespace SentinelHub.Utilities;
public enum AntiAfkState { Off, WaitingForLogin, LegacyHudInstalled, WaitingForHud, Active, TimerUnavailable, Faulted }
public sealed record AntiAfkView(AntiAfkState State, long Cycles, bool? HudPreference)
{
    public static AntiAfkView Off => new(AntiAfkState.Off, 0, null);
    public string ControlLabel(bool enabled) => !enabled ? "Anti-AFK: Off" : State switch
    {
        AntiAfkState.Active => "Anti-AFK: Active",
        AntiAfkState.WaitingForLogin => "Anti-AFK: Waiting for login",
        AntiAfkState.LegacyHudInstalled or AntiAfkState.WaitingForHud => "Anti-AFK: Blocked by older HUD",
        AntiAfkState.TimerUnavailable or AntiAfkState.Faulted => "Anti-AFK: Unavailable",
        _ => "Anti-AFK: Starting",
    };
    public string Description => State switch
    {
        AntiAfkState.Off => "Off",
        AntiAfkState.WaitingForLogin => "Waiting for login",
        AntiAfkState.LegacyHudInstalled => "Blocked: update Sentinel HUD to 0.8.4.6 or later before enabling Hub Anti-AFK.",
        AntiAfkState.WaitingForHud => "An older HUD utility engine has not handed over Anti-AFK. Update HUD or disable its old copy.",
        AntiAfkState.Active => "Active; Hub owns Anti-AFK.",
        AntiAfkState.TimerUnavailable => "Client inactivity timers unavailable; bounded retry pending.",
        _ => "Faulted; no timer resets. Turn off to return ownership.",
    };
}
public static class AntiAfkOwnershipPolicy
{
    public static AntiAfkState Resolve(bool enabled, bool loggedIn, bool hudInstalled, bool hudLoaded, string? hudVersion, bool acknowledged)
    {
        if (!enabled) return AntiAfkState.Off;
        if (!loggedIn) return AntiAfkState.WaitingForLogin;
        // Even a disabled legacy installation can be hot-loaded. Require its compatibility update.
        if (hudInstalled && (!Version.TryParse(hudVersion, out var version) || version < new Version(0, 8, 4, 6)))
            return AntiAfkState.LegacyHudInstalled;
        return hudLoaded && HudUtilityOwnership.RequiresLease(hudVersion) && !acknowledged ? AntiAfkState.WaitingForHud : AntiAfkState.Active;
    }
}
public sealed class AntiAfkEngine(Func<bool> resetTimers)
{
    public const long IntervalMilliseconds = 10000;
    public const float ThresholdSeconds = 30f;
    private long next;
    public AntiAfkState State { get; private set; }
    public long Cycles { get; private set; }
    public static bool ShouldReset(float value) => float.IsFinite(value) && value > ThresholdSeconds;
    public void Tick(AntiAfkState ownership, long now)
    {
        if (ownership != AntiAfkState.Active) { Suspend(ownership); return; }
        if (now < next) return;
        next = now + IntervalMilliseconds;
        try
        {
            if (!resetTimers()) { State = AntiAfkState.TimerUnavailable; return; }
            Cycles++; State = AntiAfkState.Active;
        }
        catch { State = AntiAfkState.Faulted; }
    }
    public void Suspend(AntiAfkState state = AntiAfkState.Off) { State = state; next = 0; }
}

