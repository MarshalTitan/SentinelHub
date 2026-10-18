using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using SentinelCore.UI;
using SentinelHub.Configuration;
using SentinelHub.Diagnostics;
using SentinelHub.Ecosystem;
using SentinelHub.Utilities;
using SentinelHub.Utilities.Quest;
using SentinelHub.Mounts;
using SentinelHub.Fashion;
using SentinelHub.Identification;
namespace SentinelHub.UI;
public sealed class MainWindow : Window, IDisposable
{
    public const string WindowId = "Sentinel Hub##SentinelHub-Main";
    public static readonly Vector2 Minimum = new(480, 500);
    private readonly HubSettings config;
    private readonly Func<HubSnapshot> snapshot;
    private readonly FaultJournal faults;
    private readonly Action save;
    private readonly Action refresh;
    private readonly Func<string, bool, bool> open;
    private readonly Action export;
    private readonly Func<bool> systemReducedMotion;
    private readonly Func<ImFontPtr> iconFont;
    private readonly Func<AntiAfkView> antiAfkView;
    private readonly Func<QuestConvenienceView> questView;
    private readonly Func<LatencyView> latencyView;
    private readonly MountCollectionPanel mounts;
    private readonly FashionAccessoriesPanel fashion;
    private readonly HealthPanel health;
    private readonly CompatibilityPanel compatibility;
    private readonly PlayerInspectorPanel inspector;
    private readonly PausePanel pause;
    private readonly RecentActivityPanel activity;
    private readonly BackupsPanel backups;
    private PlayerIdentification? identification;
    private readonly SentinelModernStyleScope style = new();
    private readonly SentinelModernAppShellState shell = new();
    private readonly SentinelModernNavItem[] navigation;
    private static readonly HubPage[] PageOrder = [HubPage.Dashboard, HubPage.Plugins, HubPage.Profiles, HubPage.Dependencies,
        HubPage.Utilities, HubPage.Mounts, HubPage.Fashion, HubPage.PlayerInspector, HubPage.RecentActivity, HubPage.Backups, HubPage.Diagnostics, HubPage.Appearance, HubPage.About];
    private Vector2 frameSize;
    private Vector2? pendingSize;
    private bool modern;
    public string Notice { get; set; } = "";
    public MainWindow(HubSettings config, Func<HubSnapshot> snapshot, FaultJournal faults,
        Action save, Action refresh, Func<string, bool, bool> open, Action export,
        Func<bool> systemReducedMotion, Func<ImFontPtr>? iconFont = null, Func<AntiAfkView>? antiAfkView = null, Func<QuestConvenienceView>? questView = null, Func<LatencyView>? latencyView = null, Func<MountCollectionView>? mountView = null,
        Action? refreshMounts = null, Action? retryMounts = null, Action<uint, Vector2>? mountIcon = null, Func<FashionView>? fashionView = null, Action? refreshFashion = null, Action? retryFashion = null,
        Func<SentinelHub.Health.HealthView>? healthView=null, Action? refreshHealth=null, Action? publicVersions=null,
        Func<SentinelHub.Compatibility.CompatibilityView>? compatibilityView=null,
        Func<SentinelHub.Inspection.InspectorView>? inspectorView=null,
        Func<SentinelHub.Coordination.PauseView>? pauseView=null, Action<bool>? pauseRequest=null,
        Func<SentinelHub.Activity.ActivityEntry[]>? activityEntries=null, Action? clearActivity=null, Func<SentinelHub.Backups.BackupView>? backupView=null,
        Action<string>? createBackup=null, Action? refreshBackups=null, Action<string,string>? restoreBackup=null, Action<string>? deleteBackup=null) : base(WindowId)
    {
        this.antiAfkView = antiAfkView ?? (() => AntiAfkView.Off);
        this.questView = questView ?? (() => QuestConvenienceView.Off);
        this.latencyView = latencyView ?? (() => LatencyView.Off);
        health=new(healthView??(()=>SentinelHub.Health.HealthView.Empty),refreshHealth??(()=>{}),publicVersions??(()=>{}));
        backups=new(backupView??(()=>SentinelHub.Backups.BackupView.Empty),createBackup??(_=>{}),refreshBackups??(()=>{}),restoreBackup??((_,_)=>{}),deleteBackup??(_=>{}));
        activity=new(activityEntries??(()=>[]),clearActivity??(()=>{}));
        pause=new(pauseView??(()=>SentinelHub.Coordination.PauseView.Empty),pauseRequest??(_=>{}));
        compatibility=new(compatibilityView??(()=>SentinelHub.Compatibility.CompatibilityView.Empty));
        inspector=new(config,save,inspectorView??(()=>SentinelHub.Inspection.InspectorView.Empty),OpenMount,OpenFashion,mountIcon);
        mounts = new(config, mountView ?? (() => MountCollectionView.Empty), save,
            refreshMounts ?? (() => {}), retryMounts ?? (() => {}), mountIcon);
        fashion = new(config, fashionView ?? (() => FashionView.Empty), save, refreshFashion ?? (() => {}), retryFashion ?? (() => {}), mountIcon);
        this.config = config; this.snapshot = snapshot; this.faults = faults;
        this.save = save; this.refresh = refresh; this.open = open; this.export = export;
        this.systemReducedMotion = systemReducedMotion;
        this.iconFont = iconFont ?? (() => UiBuilder.IconFont);
        navigation = PageOrder.Select(p => new SentinelModernNavItem(p.ToString(), null, PageTitle(p))
        {
            DrawIcon = context => DrawIcon(Icon(p), context.DrawList, context.Minimum, context.Maximum, context.Colour),
        }).ToArray();
        Size = new(config.ExpandedWidth, config.ExpandedHeight);
        SizeCondition = ImGuiCond.FirstUseEver;
    }
    private static FontAwesomeIcon Icon(HubPage p) => p switch
    {
        HubPage.Dashboard => FontAwesomeIcon.Home, HubPage.Plugins => FontAwesomeIcon.Cubes,
        HubPage.Profiles => FontAwesomeIcon.Users, HubPage.Dependencies => FontAwesomeIcon.Link, HubPage.PlayerInspector=>FontAwesomeIcon.Search, HubPage.RecentActivity=>FontAwesomeIcon.Bell, HubPage.Backups=>FontAwesomeIcon.Save,
        HubPage.Utilities => FontAwesomeIcon.Tools, HubPage.Diagnostics => FontAwesomeIcon.Heartbeat,
        HubPage.Appearance => FontAwesomeIcon.Palette, HubPage.Mounts => FontAwesomeIcon.Horse, HubPage.Fashion => FontAwesomeIcon.Umbrella, _ => FontAwesomeIcon.InfoCircle,
    };
    public static string PageTitle(HubPage p) => p == HubPage.Mounts ? "Mount Collection" : p == HubPage.Fashion ? "Fashion Accessories" : p == HubPage.Dependencies ? "Health" : p==HubPage.PlayerInspector?"Player Inspector":p==HubPage.RecentActivity?"Recent Activity":p.ToString();
    public override void PreDraw()
    {
        style.Pop();
        modern = config.Theme == 1;
        var scale = ImGuiHelpers.GlobalScale;
        Flags = modern ? SentinelModernWindowChrome.UseCustomHeader(ImGuiWindowFlags.None) : ImGuiWindowFlags.None;
        if (modern) style.PushAppShell(scale);
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(Minimum.X, modern && config.Collapsed ? 56 : Minimum.Y) * scale,
            MaximumSize = new(float.MaxValue, modern && config.Collapsed ? 56 * scale : float.MaxValue),
        };
        if (modern) ImGui.SetNextWindowCollapsed(false, ImGuiCond.Always);
        if (pendingSize is { } next) { Size = next; SizeCondition = ImGuiCond.Always; pendingSize = null; }
        else SizeCondition = ImGuiCond.FirstUseEver;
    }
    public override void PostDraw() => style.Pop();
    public override void Draw()
    {
        frameSize = ImGui.GetWindowSize();
        if (modern)
        {
            SentinelModernAppShell.Draw(new("SentinelHub.Modern", "Sentinel Hub", config.Page.ToString())
            {
                Scale = ImGuiHelpers.GlobalScale, DeltaTime = ImGui.GetIO().DeltaTime,
                Layout = SentinelModernAppLayoutOptions.Default with { RailButtonSize = Math.Clamp((frameSize.Y / ImGuiHelpers.GlobalScale - 108) / PageOrder.Length - 8,22,42) },
                ReducedMotion = config.ReducedMotion || systemReducedMotion(),
                EnableWindowDragging = true,
                RequestCollapse = ToggleCollapse, CollapseTooltip = config.Collapsed ? "Expand" : "Minimize",
                RequestClose = () => IsOpen = false,
                DrawPluginIcon = c => DrawIcon(FontAwesomeIcon.ShieldAlt, c.DrawList, c.Minimum, c.Maximum, SentinelModernPalette.Text),
            }, shell, navigation, id => { config.Page = Enum.Parse<HubPage>(id); save(); },
            () => { if (!config.Collapsed) DrawPage(); });
        }
        else
        {
            ImGui.SetNextItemWidth(-1);
            var page = Array.IndexOf(PageOrder, config.Page);
            var names = PageOrder.Select(PageTitle).ToArray();
            if (ImGui.Combo("##Page", ref page, names, names.Length)) { config.Page = PageOrder[page]; save(); }
            DrawPage();
        }
    }
    public void OpenMount(uint id)
    {
        config.Page = HubPage.Mounts; mounts.Select(id); OpenAndExpand(); save();
    }
    public void OpenFashion(uint? id)
    {
        config.Page = HubPage.Fashion; fashion.Select(id); OpenAndExpand(); save();
    }
    public void ClearIdentification() => identification = null;
    public void OpenPlayerInspector(){config.Page=HubPage.PlayerInspector;OpenAndExpand();save();}
    public void OpenIdentification(PlayerIdentification result)
    {
        identification = result;
        if (result.Fashion.State == IdentificationState.Found) OpenFashion(result.Fashion.Id);
        else if (result.Mount.State == IdentificationState.Found) OpenMount(result.Mount.Id);
        else if (config.FashionContextMenuEnabled) OpenFashion(null);
        else { config.Page=HubPage.Mounts; mounts.Select(null); OpenAndExpand(); save(); }
    }
    private void DrawIdentification()
    {
        if (identification is not {} result) return;
        ImGui.TextWrapped("Last player identification (snapshot at your click)");
        string Status(IdentificationPart part) => part.State switch {
            IdentificationState.None => "None active", IdentificationState.Off => "Lookup disabled",
            IdentificationState.Found => "Identified #" + part.Id, IdentificationState.UnknownId => "Unknown ornament #" + part.Id,
            _ => "Unavailable; identify a loaded player again",
        };
        ImGui.TextWrapped("Mount: " + Status(result.Mount));
        ImGui.TextWrapped("Fashion accessory: " + Status(result.Fashion));
        if (result.Mount.State == IdentificationState.Found && Button("View identified mount")) OpenMount(result.Mount.Id);
        if (result.Fashion.State == IdentificationState.Found && Button("View identified accessory")) OpenFashion(result.Fashion.Id);
        if (Button("Dismiss identification")) ClearIdentification();
        ImGui.Separator();
    }
    public void OpenAndExpand()
    {
        IsOpen = true;
        if (config.Collapsed) ToggleCollapse();
    }
    public void ToggleCollapse()
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (config.Collapsed)
        {
            config.Collapsed = false;
            var width = frameSize.X > 0 ? frameSize.X / scale : config.ExpandedWidth;
            pendingSize = new Vector2(Math.Max(Minimum.X, width), config.ExpandedHeight) * scale;
        }
        else
        {
            config.ExpandedWidth = Math.Max(Minimum.X, frameSize.X / scale);
            config.ExpandedHeight = Math.Max(Minimum.Y, frameSize.Y / scale);
            config.Collapsed = true;
            pendingSize = new(config.ExpandedWidth * scale, 56 * scale);
        }
        save();
    }
    private void DrawPage()
    {
        ImGui.TextWrapped(PageTitle(config.Page));
        ImGui.Separator(); ImGui.Spacing();
        if (config.Page is HubPage.Mounts or HubPage.Fashion) DrawIdentification();
        switch (config.Page)
        {
            case HubPage.Dashboard: DrawDashboard(); break;
            case HubPage.Plugins: DrawPlugins(); break;
            case HubPage.Profiles: DrawProfiles(); break;
            case HubPage.Dependencies: health.Draw(); break;
            case HubPage.Utilities: DrawUtilities(); break;
            case HubPage.Diagnostics: DrawDiagnostics(); break;
            case HubPage.Appearance: DrawAppearance(); break;
            case HubPage.About: DrawAbout(); break;
            case HubPage.Mounts: mounts.Draw(); break;
            case HubPage.Fashion: fashion.Draw(); break;
            case HubPage.PlayerInspector: inspector.Draw(); break;
            case HubPage.RecentActivity: activity.Draw(); break;
            case HubPage.Backups: backups.Draw(); break;
        }
        if (Notice.Length > 0) { ImGui.Spacing(); ImGui.TextWrapped(Notice); }
    }
    private void DrawDashboard()
    {
        pause.Draw();
        var s = snapshot();
        ImGui.TextWrapped("Optional ecosystem overview. Loaded means enabled in Dalamud; it does not prove gameplay readiness or an internal automation toggle.");
        ImGui.TextWrapped(s.DiscoveryAvailable ? "Inventory captured: " + s.CapturedAt.ToLocalTime().ToString("T") : "Inventory unavailable. Previous observations are not shown as current.");
        if (Button("Refresh inventory")) refresh();
        foreach (var p in s.Plugins) PluginCard(p, false);
    }
    private void DrawPlugins()
    {
        ImGui.TextWrapped("Detailed settings stay in their owning plugins. Settings commands may toggle an already-open window.");
        ImGui.TextWrapped("Update availability: not queried. Use Dalamud's plugin installer.");
        foreach (var p in snapshot().Plugins) PluginCard(p, true);
    }
    private void PluginCard(PluginStatus p, bool detail)
    {
        var lines = new List<string> { p.Component.Name, StatusPolicy.PresenceText(p.State), "Installed version: " + p.Version };
        if (p.State != Presence.Missing && p.State != Presence.Unknown)
            lines.Add(p.IsDev ? "Installation: development (no live installation found)" : "Installation: live");
        if (detail) lines.Add(p.Component.Purpose);
        if (detail)
        {
            var deps = Catalog.Dependencies.Where(d => d.Uses.Contains(p.Component.Name, StringComparison.Ordinal)).Select(d => d.Name);
            lines.Add("Dependencies: " + (deps.Any() ? string.Join(", ", deps) : "None required by Hub."));
        }
        var actions = new List<(string Label, bool Enabled, Action Click)>
        {
            ("Open settings", p.State == Presence.Loaded, () => Open(p.Component.Id, false)),
        };
        if (detail && p.Component.DiagnosticsCommand is not null)
            actions.Add(("Open diagnostics", p.State == Presence.Loaded, () => Open(p.Component.Id, true)));
        Card(p.Component.Id, p.State.ToString().ToUpperInvariant(), Tone(p.State), lines, actions);
    }
    private void DrawDependencies()
    {
        ImGui.TextWrapped("Dependencies are feature-specific. Missing optional companions do not make the whole ecosystem unhealthy.");
        foreach (var d in snapshot().Dependencies)
            Card(d.Dependency.Id, d.Response == Response.Unknown ? d.State.ToString().ToUpperInvariant() : d.Response.ToString().ToUpperInvariant(),
                d.Response switch { Response.Ready => SentinelModernPillTone.Ready, Response.NotReady or Response.Unavailable => SentinelModernPillTone.Warning, _ => Tone(d.State) },
                [d.Dependency.Name, StatusPolicy.DependencyText(d), "Installed version: " + d.Version, "Used by: " + d.Dependency.Uses, d.Dependency.Purpose,
                 d.Dependency.Probe is null ? "No safe readiness probe is exposed here." : "Read-only status; no movement or combat calls."]);
    }
    private void DrawProfiles()
    {
        var p = snapshot().Plugins.Single(x => x.Component.Id == "SentinelProfiles");
        PluginCard(p, false);
        Card("profiles-contract", "UNKNOWN", SentinelModernPillTone.Neutral,
            ["Selected profile: unavailable", "Last applied: unavailable", "Drift / health: unavailable",
             "The current Profiles build exposes no public read-only status contract. Open Profiles for authoritative state. Hub does not read private configuration or apply profiles."]);
    }
    private void DrawUtilities()
    {
        pause.Draw();
        compatibility.Draw();
        var view = antiAfkView();
        ImGui.TextWrapped("Prevent AFK Disconnect");
        var enabled = config.AntiAfkEnabled;
        var changed = modern
            ? SentinelModernSwitch.Draw("anti-afk-enable", "Enable Anti-AFK", ref enabled, shell.Motion, ImGuiHelpers.GlobalScale)
            : ImGui.Checkbox("Enable Anti-AFK", ref enabled);
        if (changed) { config.AntiAfkEnabled = enabled; save(); }
        ImGui.TextWrapped(view.Description);
        ImGui.TextWrapped("Checks every 10 seconds and resets inactivity timers above 30 seconds. No keys, movement or chat.");
        ImGui.TextWrapped("Successful timer checks this session: " + view.Cycles);
        ImGui.Separator(); ImGui.Spacing();
        DrawQuestConvenience();
        ImGui.Separator(); ImGui.Spacing();
        DrawLatency();
        foreach (var p in UtilityRegistry.Planned)
            Card(p.Name, "PLANNED", SentinelModernPillTone.Neutral, [p.Name, p.Status, p.NextGate]);
    }
    private void DrawLatency()
    {
        var view = latencyView();
        ImGui.TextWrapped("Latency / Weaving");
        var mode = (int)config.LatencyMode;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.Combo("##latency-mode", ref mode, new[] { "Off", "Measure only", "Compensate" }, 3))
        { config.LatencyMode = (LatencyMode)mode; save(); }
        ImGui.TextWrapped(view.Description);
        ImGui.TextWrapped("Reduces added response delay for your own instant spells, weaponskills and abilities. Fixed 40 ms response buffer and maximum 250 ms correction per action.");
        ImGui.TextWrapped("Measure only observes without adjusting. Cast-time actions and long/special locks are excluded. Keep other animation-lock tools disabled; detected NoClippy or XivAlexander blocks this module.");
        ImGui.TextWrapped("Samples / applied adjustments: " + view.Samples + " / " + view.Adjustments);
        ImGui.TextWrapped(view.Samples == 0 ? "No eligible action response measured yet." :
            $"Action response: {view.LastResponseMs:0} ms last / {view.AverageResponseMs:0} ms average");
        ImGui.TextWrapped($"Last calculated correction: {view.LastReductionMs:0} ms; applied total: {view.TotalReductionMs:0} ms");
        ImGui.TextWrapped("Measurements include server and frame delay; they are not network ping. Counts reset when an active mode restarts. Set Off before retrying a stopped module.");
    }
    private void DrawQuestConvenience()
    {
        var view = questView();
        ImGui.TextWrapped("Quest Convenience");
        ImGui.TextWrapped("Default Off. Hub can advance ordinary dialogue, skip cutscenes and select quest rewards. Your dialogue choices remain manual.");
        var enabled = config.QuestConvenienceEnabled;
        if (QuestToggle("quest-enable", "Enable Quest Convenience", ref enabled)) { config.QuestConvenienceEnabled = enabled; save(); }
        ImGui.TextWrapped(view.Description);
        var dialogue = config.QuestDialogue;
        if (QuestToggle("quest-dialogue", "Skip ordinary dialogue", ref dialogue)) { config.QuestDialogue = dialogue; save(); }
        var cutscenes = config.QuestCutscenes;
        if (QuestToggle("quest-cutscenes", "Skip cutscenes", ref cutscenes)) { config.QuestCutscenes = cutscenes; save(); }
        ImGui.TextWrapped("Reward selection");
        var reward = (int)config.QuestRewardMode;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.Combo("##quest-reward-mode", ref reward,
            new[] { "Manual", "First reward", "Current job (otherwise first)", "Allagan piece (otherwise first)" }, 4))
        { config.QuestRewardMode = (QuestRewardSelectionMode)reward; save(); }
        ImGui.TextWrapped("Automatic reward modes select a choose-one reward and press Complete when ready. Guaranteed rewards are left unchanged.");
        ImGui.TextWrapped("Turning Quest Convenience off stops its dialogue, cutscene and reward actions.");
        ImGui.TextWrapped("Dialogue: " + view.Dialogue);
        ImGui.TextWrapped("Cutscenes: " + view.Cutscene);
        ImGui.TextWrapped("Rewards: " + view.Reward);
    }
    private bool QuestToggle(string id, string label, ref bool value) => modern
        ? SentinelModernSwitch.Draw(id, label, ref value, shell.Motion, ImGuiHelpers.GlobalScale)
        : ImGui.Checkbox(label, ref value);
    private void DrawDiagnostics()
    {
        ImGui.TextWrapped("The ZIP contains structured JSON and a readable summary: known Sentinel versions, health, dependency responses, bounded Hub faults and compatible provider counters.");
        ImGui.TextWrapped("Credentials, configuration contents, names, private IDs, chat, paths and arbitrary provider text are excluded. Unsupported provider/profile state stays unavailable.");
        if (Button("Export Sentinel Diagnostics")) export();
        if (Button("Refresh observations")) refresh();
        var entries = faults.Snapshot();
        if (entries.Length == 0) ImGui.TextWrapped("No recent Hub faults.");
        foreach (var e in entries.Reverse()) ImGui.TextWrapped(e.At.ToLocalTime().ToString("T") + "  " + e.Code);
        if (Button("Clear recent Hub faults")) faults.Clear();
    }
    private void DrawAppearance()
    {
        ImGui.TextWrapped("Appearance preferences apply to Hub. Consumers retain their own settings.");
        var theme = config.Theme;
        ImGui.SetNextItemWidth(-1);
        if (ImGui.Combo("##Theme", ref theme, new[] { "Classic", "Sentinel Modern" }, 2))
        {
            if (config.Collapsed) ToggleCollapse();
            config.Theme = theme; save();
        }
        ImGui.TextWrapped("Theme");
        var reduced = config.ReducedMotion;
        var changed = modern
            ? SentinelModernSwitch.Draw("reduced", "Reduced motion", ref reduced, shell.Motion, ImGuiHelpers.GlobalScale)
            : ImGui.Checkbox("Reduced motion", ref reduced);
        if (changed) { config.ReducedMotion = reduced; save(); }
        ImGui.TextWrapped("Dalamud's reduced-motion preference also applies. Shared ecosystem preferences are planned; no consumer queries Hub to render.");
    }
    private void DrawAbout()
    {
        Card("about", "MVP", SentinelModernPillTone.Accent,
            ["Sentinel Hub " + (typeof(MainWindow).Assembly.GetName().Version?.ToString(4) ?? "Unknown"), "Optional Sentinel ecosystem control center.",
             "Core UI 0.4.1, bundled with Hub. No separately installed Core plugin.",
             "Other Sentinel plugins work normally when Hub is disabled or absent.",
             "Camera, HUD rendering, hunt policy, Frontline strategy, gear switching, Profiles and Relay remain owned by their existing plugins.",
             "Source: github.com/MarshalTitan/SentinelHub"]);
    }
    private void Open(string id, bool diagnostics)
    {
        Notice = open(id, diagnostics) ? "Settings command sent." : "Settings unavailable. Refresh inventory or use the owning plugin command.";
    }
    private static SentinelModernPillTone Tone(Presence p) => p switch
    {
        Presence.Loaded => SentinelModernPillTone.Enabled, Presence.Degraded => SentinelModernPillTone.Warning, _ => SentinelModernPillTone.Neutral,
    };
    private static bool Button(string text) => ImGui.Button(text, new Vector2(-1, 28 * ImGuiHelpers.GlobalScale));
    private void Card(string id, string status, SentinelModernPillTone tone, IEnumerable<string> lines,
        IReadOnlyList<(string Label, bool Enabled, Action Click)>? actions = null)
    {
        var text = lines.ToArray();
        var scale = ImGuiHelpers.GlobalScale;
        var width = Math.Max(80, ImGui.GetContentRegionAvail().X - 28 * scale - 16 * scale);
        var height = 34 * scale + text.Sum(s => ImGui.CalcTextSize(s, false, width).Y + 8 * scale)
            + (modern ? 26 * scale : 0) + (actions?.Count ?? 0) * (36 * scale) + 14 * scale;
        if (modern)
        {
            using var card = SentinelModernGlassCard.Begin(id, new() { Size = new(0, height / scale) }, scale);
            if (card.IsVisible) Body();
        }
        else
        {
            using var card = SentinelModernCard.Begin(id, new(0, height));
            if (card.IsVisible) Body();
        }
        ImGui.Spacing();
        void Body()
        {
            if (modern) SentinelModernStatusPill.Draw(new(status, tone), shell.Motion, scale);
            foreach (var line in text) { ImGui.TextWrapped(line); ImGui.Spacing(); }
            if (actions is not null) foreach (var a in actions)
            {
                ImGui.BeginDisabled(!a.Enabled);
                if (Button(a.Label)) a.Click();
                ImGui.EndDisabled();
            }
        }
    }
    private void DrawIcon(FontAwesomeIcon icon, ImDrawListPtr draw, Vector2 min, Vector2 max, Vector4 colour)
    {
        ImGui.PushFont(iconFont());
        try
        {
            var text = icon.ToIconString();
            var size = ImGui.CalcTextSize(text);
            draw.AddText(min + (max - min - size) / 2, ImGui.ColorConvertFloat4ToU32(colour), text);
        }
        finally { ImGui.PopFont(); }
    }
    public void Dispose() { style.Dispose(); shell.Dispose(); }
}

