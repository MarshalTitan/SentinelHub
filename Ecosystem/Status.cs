namespace SentinelHub.Ecosystem;
public enum Presence { Unknown, Missing, Disabled, Loaded, Degraded }
public enum Response { Unknown, Responding, Ready, NotReady, Unavailable }
public sealed record InstalledPlugin(string Id, bool Loaded, string? Version, bool Restricted = false, bool IsDev = false);
public sealed record PluginStatus(Component Component, Presence State, string Version, bool IsDev = false);
public sealed record DependencyStatus(Dependency Dependency, Presence State, string Version, Response Response);
public sealed record HubSnapshot(DateTimeOffset CapturedAt, bool DiscoveryAvailable, PluginStatus[] Plugins, DependencyStatus[] Dependencies)
{
    public static HubSnapshot Unknown => StatusPolicy.Build([], false, _ => Response.Unknown);
}
public static class StatusPolicy
{
    // The live installed copy owns its inventory row even when a development copy is loaded first.
    public static InstalledPlugin? SelectPlugin(IEnumerable<InstalledPlugin> installed, string id) =>
        installed.Where(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.IsDev).FirstOrDefault();
    public static Presence PresenceOf(InstalledPlugin? plugin, bool available) =>
        !available ? Presence.Unknown : plugin is null ? Presence.Missing :
        plugin.Restricted ? Presence.Degraded : plugin.Loaded ? Presence.Loaded : Presence.Disabled;
    public static HubSnapshot Build(IEnumerable<InstalledPlugin> installed, bool available, Func<Dependency, Response> probe)
    {
        var all = installed.ToArray();
        InstalledPlugin? Find(IEnumerable<string> ids) =>
            all.FirstOrDefault(p => ids.Contains(p.Id, StringComparer.OrdinalIgnoreCase));
        var plugins = Catalog.Components.Select(c =>
        {
            var p = SelectPlugin(all, c.Id);
            return new PluginStatus(c, PresenceOf(p, available), NumericVersion(p?.Version), p?.IsDev == true);
        }).ToArray();
        var deps = Catalog.Dependencies.Select(d =>
        {
            var p = Find(d.Aliases);
            var presence = PresenceOf(p, available);
            var response = presence == Presence.Loaded && d.Probe is not null ? probe(d) : Response.Unknown;
            return new DependencyStatus(d, presence, NumericVersion(p?.Version), response);
        }).ToArray();
        return new(DateTimeOffset.UtcNow, available, plugins, deps);
    }
    public static string NumericVersion(string? input) =>
        input is { Length: <= 40 } && System.Version.TryParse(input, out var version) ? version.ToString() : "Unknown";
    public static bool CanOpen(Component? component, IEnumerable<InstalledPlugin> current, bool available) =>
        available && component is not null && SelectPlugin(current, component.Id) is { Loaded: true, Restricted: false };
    public static string DependencyText(DependencyStatus status) => status.State != Presence.Loaded ? PresenceText(status.State) : status.Response switch
    {
        Response.Ready => "Ready (mesh available)",
        Response.NotReady => "Responding / mesh not ready",
        Response.Responding => "Responding",
        Response.Unavailable => "Loaded / status unavailable",
        _ => "Loaded / readiness unknown",
    };
    public static string PresenceText(Presence state) => state switch
    {
        Presence.Missing => "Not installed", Presence.Disabled => "Installed / not loaded",
        Presence.Loaded => "Loaded / readiness unknown", Presence.Degraded => "Degraded / installer restriction", _ => "Unknown",
    };
}
