using FFXIVClientStructs.FFXIV.Client.UI.Misc;
namespace SentinelHub.Utilities.AntiAfk;
public static unsafe class NativeInactivityTimers
{
    // Preserves HUD's accepted direct timer behavior; no input, movement or chat synthesis.
    public static bool TryReset()
    {
        var timers = InputTimerModule.Instance();
        if (timers is null) return false;
        if (AntiAfkEngine.ShouldReset(timers->AfkTimer)) timers->AfkTimer = 0f;
        if (AntiAfkEngine.ShouldReset(timers->ContentInputTimer)) timers->ContentInputTimer = 0f;
        if (AntiAfkEngine.ShouldReset(timers->InputTimer)) timers->InputTimer = 0f;
        return true;
    }
}
