namespace SentinelHub.DeathRecaps;
public static class DeathPreview
{
    public static DeathRecord Create(bool pvp = true)
    {
        var now = DateTimeOffset.UtcNow;
        ThreatActor[] actors = [new(1, "Example enemy A", 32, "DRK", "Synthetic"), new(2, "Example enemy B", 22, "DRG", "Synthetic"),
            new(3, "Example enemy C", 22, "DRG", "Synthetic"), new(4, "Example enemy D", 31, "MCH", "Synthetic"), new(5, "Example enemy E", 24, "WHM", "Synthetic")];
        var threat = pvp ? new ThreatDeath(Guid.NewGuid(), 0, 0, now, now.AddMilliseconds(-90), now.AddSeconds(-15),
            [new(now.AddSeconds(-15), actors[..2]), new(now.AddSeconds(-5), actors)], "Frontline — synthetic") : null;
        return new(Guid.NewGuid(), now, 0, pvp ? "Frontline preview" : "PvE duty preview", "Example player", 19, true, pvp,
            [new(now.AddSeconds(-5), 1, RecapEventKind.Vitals, "", 0, 0, "HP / shield snapshot", null, 62000, 90000, 10, [], "Synthetic"),
             new(now.AddSeconds(-3), 2, RecapEventKind.Damage, "Example source", pvp ? 32u : 0u, 0, "Example attack", 42000, null, null, null, [], "Synthetic", pvp),
             new(now.AddSeconds(-1), 3, RecapEventKind.Healing, "Example healer", 24, 0, "Example heal", 8000, null, null, null, [], "Synthetic", true),
             new(now, 4, RecapEventKind.Vitals, "", 0, 0, "Death observed", null, 0, 90000, 0, [], "Synthetic")],
            null, threat, "SYNTHETIC PREVIEW — not captured gameplay; never saved to history.", true);
    }
}
