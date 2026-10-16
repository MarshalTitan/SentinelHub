namespace SentinelHub.Utilities;
public sealed record UtilityPlan(string Name, string Status, string NextGate);
public static class UtilityRegistry
{
    // Later migrations remain inactive. Anti-AFK is an isolated module in Hub 0.2.
    public static readonly UtilityPlan[] Planned =
    [
        new("Quest Convenience", "Planned / inactive", "Preserve accepted submarine skipping and explicit choices; validate dialogue, cutscenes and rewards separately."),
        new("Latency / Weaving", "Planned / inactive", "Independent implementation after compatibility/ownership design and bounded compensation proving."),
    ];
}
