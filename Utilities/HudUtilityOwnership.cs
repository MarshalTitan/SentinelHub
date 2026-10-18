using SentinelHub.Ecosystem;
namespace SentinelHub.Utilities;
public static class HudUtilityOwnership
{
    // HUD 0.8.5.0 removed both native utility engines. Its retained preference bridges
    // are optional and must not block Hub (including an isolated loaded development copy).
    // Older/unknown builds retain the existing version and exclusive-handoff safeguards.
    public static bool RequiresLease(string? version)=>!Version.TryParse(version,out var parsed)||parsed<new Version(0,8,5,0);
    public static InstalledPlugin? Select(IEnumerable<InstalledPlugin> installed,Version minimum)
    {
        var hud=installed.Where(p=>p.Id=="SentinelHUD").ToArray();
        // A disabled modern install must not hide an older loaded development engine,
        // nor an unsafe legacy/unknown copy that could be hot-loaded.
        return hud.FirstOrDefault(p=>!Version.TryParse(p.Version,out var v)||v<minimum)
            ??hud.FirstOrDefault(p=>p.Loaded&&RequiresLease(p.Version))
            ??hud.FirstOrDefault(p=>p.Loaded)??hud.FirstOrDefault();
    }
}
