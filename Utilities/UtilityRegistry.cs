namespace SentinelHub.Utilities;
public sealed record UtilityPlan(string Name, string Status, string NextGate);
public static class UtilityRegistry
{
    // No runtime engines/enable flags in MVP. Existing HUD ownership is untouched.
    public static readonly UtilityPlan[] Planned =
    [
        new("Anti-AFK", "Planned / inactive", "Next migration: opt-in replacement, single-owner detection, then supervised testing before HUD cleanup."),
        new("Quest Convenience", "Planned / inactive", "Preserve accepted submarine skipping and explicit choices; validate dialogue, cutscenes and rewards separately."),
        new("Latency / Weaving", "Planned / inactive", "Independent implementation after compatibility/ownership design and bounded compensation proving."),
    ];
}
