using SentinelHub.Ecosystem;
namespace SentinelHub.Health;

public enum HealthState { Unknown, Missing, Disabled, Loaded, Ready, Degraded, DependencyMissing, DependencyDisabled, IpcUnavailable, DevelopmentBuild, VersionMismatch }
public sealed record HealthCheck(string Id, HealthState State, string RuntimeVersion, string PublicVersion, bool DevelopmentOverride, string Explanation);
public sealed record HealthView(DateTimeOffset At, bool Available, HealthCheck[] Checks, DateTimeOffset? CatalogAt, string CatalogStatus)
{
    public static HealthView Empty => new(DateTimeOffset.MinValue, false, [], null, "Public versions not fetched");
}
public static class HealthPolicy
{
    public static readonly string[] KnownIds = [..Catalog.Components.Select(c=>c.Id), ..Catalog.Dependencies.Select(d=>d.Id), "SentinelCore"];
    public static HealthCheck Plugin(Component component, IEnumerable<InstalledPlugin> installed, bool available,
        IReadOnlyDictionary<string,string> versions, int? response = null)
    {
        var all = installed.Where(p=>p.Id.Equals(component.Id,StringComparison.OrdinalIgnoreCase)).ToArray();
        var loaded = all.Where(p=>p.Loaded).OrderByDescending(p=>p.IsDev).FirstOrDefault();
        var publicCopy = all.FirstOrDefault(p=>!p.IsDev);
        var runtime = loaded ?? publicCopy ?? all.FirstOrDefault();
        var version = StatusPolicy.NumericVersion(runtime?.Version);
        var published = versions.TryGetValue(component.Id,out var v)?StatusPolicy.NumericVersion(v):"Unknown";
        var dev = loaded?.IsDev==true;
        var state = !available?HealthState.Unknown:runtime is null?HealthState.Missing:
            runtime.Restricted?HealthState.Degraded:loaded is null?HealthState.Disabled:
            dev?HealthState.DevelopmentBuild:
            published!="Unknown"&&version!="Unknown"&&version!=published?HealthState.VersionMismatch:
            response is null?HealthState.Loaded:response==0?HealthState.Ready:response==1?HealthState.Disabled:
            response==2?HealthState.Degraded:HealthState.IpcUnavailable;
        var text = state switch {
            HealthState.Unknown=>"Installed-plugin discovery unavailable; no cached readiness claim.",
            HealthState.Missing=>"Not installed.",
            HealthState.Disabled=>loaded is null?"Installed, not loaded; disabled or failed loading. Internal enable state is unknown.":"Responding; automation is paused/disabled.",
            HealthState.DevelopmentBuild=>"A loaded development copy owns runtime behavior. Public installed copy: "+StatusPolicy.NumericVersion(publicCopy?.Version)+".",
            HealthState.VersionMismatch=>"Loaded version differs from the dated public catalog. A difference is not proof of a broken installation.",
            HealthState.Ready=>"The supported health endpoint reports ready; feature-specific dependencies still apply.",
            HealthState.Degraded=>"Restricted installation or provider reports degraded; inspect the owning plugin.",
            HealthState.IpcUnavailable=>"Loaded, but the optional Sentinel health endpoint is unavailable or unsupported.",
            _=>"Loaded; internal enable/readiness is not exposed by this build. DLL presence is not proof of readiness.",
        };
        return new(component.Id,state,version,published,dev,text);
    }
    public static HealthCheck Dependency(DependencyStatus d) => new(d.Dependency.Id,
        d.State switch { Presence.Missing=>HealthState.DependencyMissing,Presence.Disabled=>HealthState.DependencyDisabled,
            Presence.Degraded=>HealthState.Degraded,Presence.Unknown=>HealthState.Unknown,
            _=>d.Response switch { Response.Ready=>HealthState.Ready,Response.Unavailable=>HealthState.IpcUnavailable,
                Response.NotReady=>HealthState.Degraded,_=>HealthState.Loaded } },
        StatusPolicy.NumericVersion(d.Version),"Unknown",false,
        StatusPolicy.DependencyText(d)+". "+d.Dependency.Purpose+
        (d.Dependency.Probe is null?" No supported readiness probe; internal enabled state unknown.":" Read-only probe; feature configuration is owned by its plugin."));
}
