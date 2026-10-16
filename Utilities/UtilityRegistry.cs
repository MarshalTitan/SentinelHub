namespace SentinelHub.Utilities;
public sealed record UtilityPlan(string Name, string Status, string NextGate);
public static class UtilityRegistry
{
    // Later migrations remain inactive. Anti-AFK and Quest Convenience are isolated opt-in modules.
    public static readonly UtilityPlan[] Planned =
    [
        new("Latency / Weaving", "Planned / inactive", "Independent implementation after compatibility/ownership design and bounded compensation proving."),
    ];
}
