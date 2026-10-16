namespace SentinelHub.Ecosystem;
public sealed record Component(string Id, string Name, string SettingsCommand, string Purpose, string? DiagnosticsCommand = null);
public sealed record Dependency(string Id, string Name, string[] Aliases, string Uses, string Purpose, string? Probe = null);
public static class Catalog
{
    // Commands verified against consumer source; no free-form command input.
    public static readonly Component[] Components =
    [
        new("SRankSentinel", "S Rank Sentinel", "/sranksentinel", "S/SS hunts; detailed hunt policy stays here."),
        new("PvPSentinel", "PvP Sentinel", "/pvpsentinel config", "Frontline strategy and navigation.", "/pvpsentinel"),
        new("SentinelHUD", "Sentinel HUD", "/shud", "Visual HUD, awareness, camera and editor."),
        new("ClassySentinel", "Classy Sentinel", "/classysentinel config", "Gear-set launcher."),
        new("SentinelProfiles", "Sentinel Profiles", "/sprofiles", "Manual three-state plugin profiles."),
        new("SentinelRelay", "Sentinel Relay", "/srelay", "Discord/FFXIV chat bridge."),
        new("SentinelHunts", "Sentinel Hunts", "/sentinelhunts", "Separate hunt companion beta."),
        new("SentinelHub", "Sentinel Hub", "/shub", "Optional ecosystem control center."),
    ];
    public static readonly Dependency[] Dependencies =
    [
        new("vnavmesh", "vnavmesh", ["vnavmesh"], "S Rank Sentinel, PvP Sentinel, Sentinel Hunts", "Movement for enabled automation; not globally required.", "vnavmesh.Nav.IsReady"),
        new("Lifestream", "Lifestream", ["Lifestream"], "S Rank Sentinel, Sentinel Hunts", "Travel for Sentinel Hunts; optional SRank companion alongside native world travel.", "Lifestream.IsBusy"),
        new("RotationSolver", "RotationSolverReborn", ["RotationSolver", "RotationSolverReborn"], "PvP Sentinel", "External combat provider when selected in PvP settings.", "RotationSolverReborn.AutorotationActive"),
        new("Splatoon", "Splatoon", ["Splatoon"], "Sentinel HUD", "Optional visual danger integration; response does not prove geometry availability.", "Splatoon.IsLoaded"),
        new("HuntAlerts", "HuntAlerts", ["HuntAlerts"], "S Rank Sentinel, Sentinel Hunts", "Optional report provider; SRank also has direct Faloop."),
        new("SonarPlugin", "Sonar", ["SonarPlugin", "Sonar"], "S Rank Sentinel, Sentinel Hunts", "Optional hunt report provider."),
    ];
    public static Component? Find(string id) => Components.FirstOrDefault(c => c.Id == id);
}
