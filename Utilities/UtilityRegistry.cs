namespace SentinelHub.Utilities;
public sealed record UtilityPlan(string Name, string Status, string NextGate);
public static class UtilityRegistry
{
    // Anti-AFK, Quest Convenience and Latency/Weaving are implemented, isolated opt-in modules.
    public static readonly UtilityPlan[] Planned = [];
}
