using SentinelHub.Utilities.Quest;
namespace SentinelHub.Utilities;
public enum QuestConvenienceState { Off, WaitingForLogin, LegacyHudInstalled, WaitingForHud, Active, Faulted }
public sealed record QuestConvenienceView(QuestConvenienceState State, int? HudPreferences, string Dialogue, string Cutscene, string Reward)
{
    public static QuestConvenienceView Off => new(QuestConvenienceState.Off, null, "Disabled", "Disabled", "Manual");
    public string Description => State switch
    {
        QuestConvenienceState.Off => "Off",
        QuestConvenienceState.WaitingForLogin => "Waiting for login",
        QuestConvenienceState.LegacyHudInstalled => "Update Sentinel HUD to 0.8.4.7 or later before enabling Hub Quest Convenience.",
        QuestConvenienceState.WaitingForHud => "An older HUD utility engine has not handed over Quest Convenience. Update HUD or disable its old copy.",
        QuestConvenienceState.Active => "Active; Hub owns Quest Convenience.",
        _ => "Quest Convenience paused after an error; pending actions cleared. Retrying shortly.",
    };
}
public static class QuestConvenienceOwnershipPolicy
{
    public static QuestConvenienceState Resolve(bool enabled, bool loggedIn, bool hudInstalled, bool hudLoaded, string? hudVersion, bool acknowledged)
    {
        if (!enabled) return QuestConvenienceState.Off;
        if (!loggedIn) return QuestConvenienceState.WaitingForLogin;
        if (hudInstalled && (!Version.TryParse(hudVersion, out var version) || version < new Version(0,8,4,7)))
            return QuestConvenienceState.LegacyHudInstalled;
        return hudLoaded && HudUtilityOwnership.RequiresLease(hudVersion) && !acknowledged ? QuestConvenienceState.WaitingForHud : QuestConvenienceState.Active;
    }
}
// All actions are synchronous. Clearing a session revokes its pending cutscene/reward work.
public sealed class QuestExecutionSession(Action<QuestConvenienceOptions> update, Action reset)
{
    private bool running;
    private int fingerprint = -1;
    private long retryAfter;
    private QuestConvenienceOptions? options;
    public QuestConvenienceState State { get; private set; }
    public void Tick(QuestConvenienceState ownership, int preferences, long now, bool process=true)
    {
        if (ownership != QuestConvenienceState.Active) { Stop(ownership); return; }
        if (now < retryAfter) { State = QuestConvenienceState.Faulted; return; }
        if (fingerprint!=preferences && !QuestPreferenceCodec.TryDecode(preferences, out options)) { Stop(QuestConvenienceState.Faulted); return; }
        try
        {
            if (!running || fingerprint != preferences) { reset(); running = true; fingerprint = preferences; }
            if(process)update(options!);
            State = QuestConvenienceState.Active;
        }
        catch
        {
            Stop(QuestConvenienceState.Faulted);
            retryAfter = now + 1000;
        }
    }
    public void Stop(QuestConvenienceState state = QuestConvenienceState.Off)
    {
        if (running) reset();
        running = false; fingerprint = -1; retryAfter = 0; State = state;
    }
}

