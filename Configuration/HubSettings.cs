namespace SentinelHub.Configuration;
public enum HubPage { Dashboard, Plugins, Profiles, Dependencies, Utilities, Diagnostics, Appearance, About }
public class HubSettings
{
    public int Version { get; set; } = 1;
    public int Theme { get; set; } = 1;
    public HubPage Page { get; set; }
    public bool AntiAfkEnabled { get; set; } // New installs and upgrades default OFF.
    public bool AntiAfkPreferenceImported { get; set; }
    public bool ReducedMotion { get; set; }
    public bool Collapsed { get; set; }
    public float ExpandedWidth { get; set; } = 860;
    public float ExpandedHeight { get; set; } = 680;
    public void Normalize()
    {
        Version = 1;
        if (Theme is not (0 or 1)) Theme = 1;
        if (!Enum.IsDefined(Page)) Page = HubPage.Dashboard;
        if (!float.IsFinite(ExpandedWidth)) ExpandedWidth = 860;
        if (!float.IsFinite(ExpandedHeight)) ExpandedHeight = 680;
        ExpandedWidth = Math.Clamp(ExpandedWidth, 480, 4000);
        ExpandedHeight = Math.Clamp(ExpandedHeight, 500, 3000);
    }
}
