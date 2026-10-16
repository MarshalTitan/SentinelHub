namespace SentinelHub.Utilities.Quest;

public enum CutsceneSkipConfirmationDecision
{
    Wait,
    Confirm,
    Dismissed,
    TimedOut,
}

public enum CutsceneSkipPromptKind
{
    None,
    SelectString,
    CutSceneSelectString,
}

/// <summary>
/// Matches only the two game addon identities currently used for cutscene-skip selection and
/// requires the visible addon's live ID to equal the ID published by <c>AgentCutscene</c>.
/// Keeping this policy platform-neutral makes the fail-closed identity gate regression-testable.
/// </summary>
public static class CutsceneSkipPromptPolicy
{
    public const string SelectStringAddonName = "SelectString";
    public const string CutSceneSelectStringAddonName = "CutSceneSelectString";

    public static bool TryMatch(
        string? addonName,
        uint observedAddonId,
        uint expectedAddonId,
        out CutsceneSkipPromptKind kind)
    {
        kind = CutsceneSkipPromptKind.None;
        if (expectedAddonId is 0 or > ushort.MaxValue || observedAddonId != expectedAddonId)
            return false;

        kind = addonName switch
        {
            SelectStringAddonName => CutsceneSkipPromptKind.SelectString,
            CutSceneSelectStringAddonName => CutsceneSkipPromptKind.CutSceneSelectString,
            _ => CutsceneSkipPromptKind.None,
        };
        return kind != CutsceneSkipPromptKind.None;
    }
}

public readonly record struct CutsceneSkipConfirmationState(
    bool Pending,
    bool PromptObserved,
    long DeadlineTick)
{
    public static CutsceneSkipConfirmationState Begin(long now, long timeoutMilliseconds)
    {
        if (timeoutMilliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));

        return new CutsceneSkipConfirmationState(true, false, now + timeoutMilliseconds);
    }
}

public static class CutsceneSkipConfirmationPolicy
{
    public static CutsceneSkipConfirmationDecision Evaluate(
        ref CutsceneSkipConfirmationState state,
        long now,
        bool promptVisible,
        bool confirmationAvailable)
    {
        if (!state.Pending)
            return CutsceneSkipConfirmationDecision.Wait;

        if (now >= state.DeadlineTick)
            return CutsceneSkipConfirmationDecision.TimedOut;

        if (promptVisible)
        {
            state = state with { PromptObserved = true };
            return confirmationAvailable
                ? CutsceneSkipConfirmationDecision.Confirm
                : CutsceneSkipConfirmationDecision.Wait;
        }

        if (state.PromptObserved)
            return CutsceneSkipConfirmationDecision.Dismissed;

        return CutsceneSkipConfirmationDecision.Wait;
    }
}
