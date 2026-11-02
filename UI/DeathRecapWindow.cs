using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using SentinelCore.UI;
using SentinelHub.Configuration;
using SentinelHub.DeathRecaps;
namespace SentinelHub.UI;

public sealed class DeathRecapWindow : Window, IDisposable
{
    public const string WindowId = "Death Recap##SentinelHub-DeathRecap";
    private readonly HubSettings settings;
    private readonly Func<DeathView> view;
    private readonly DeathRecapsPanel panel;
    private readonly Action openFull, body, close;
    private readonly Func<bool> reducedMotion;
    private readonly SentinelModernStyleScope style = new();
    private readonly SentinelModernAppShellState shell = new();
    private readonly SentinelModernNavItem[] navigation;
    private readonly Action<SentinelModernIconDrawContext> drawIcon;
    private static readonly Action<string> Select = _ => { };
    private static readonly string Glyph = FontAwesomeIcon.Heartbeat.ToIconString();
    private long expires;
    private bool modern;
    public DeathRecapWindow(HubSettings settings, Func<DeathView> view, Action<string, Guid> request, Action changed,
        Action save, Action openFull, Func<bool> reducedMotion, Action<uint, Vector2>? icon = null, Func<ImFontPtr>? iconFont = null) : base(WindowId)
    {
        this.settings = settings; this.view = view; this.openFull = openFull; this.reducedMotion = reducedMotion;
        panel = new(settings.DeathRecaps, view, request, changed, save, icon);
        Size = new(440, 420); SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new() { MinimumSize = new(320, 240), MaximumSize = new(1600, 1600) };
        Flags = ImGuiWindowFlags.NoFocusOnAppearing;
        body = Body; close = () => IsOpen = false;
        var font = iconFont ?? (() => UiBuilder.IconFont);
        drawIcon = c => Paint(c.DrawList, c.Minimum, c.Maximum, SentinelModernPalette.Text);
        navigation = [new("Recap", null, "Death Recap") { DrawIcon = c => Paint(c.DrawList, c.Minimum, c.Maximum, c.Colour) }];
        void Paint(ImDrawListPtr draw, Vector2 min, Vector2 max, Vector4 colour) { ImGui.PushFont(font()); try { var size = ImGui.CalcTextSize(Glyph); draw.AddText(min + (max - min - size) / 2, ImGui.ColorConvertFloat4ToU32(colour), Glyph); } finally { ImGui.PopFont(); } }
    }
    public void Show(DeathRecord r)
    {
        var s = settings.DeathRecaps;
        if (!r.Synthetic && (!s.Enabled || !r.Local || s.Popup == DeathPopupMode.Off || s.Popup == DeathPopupMode.PvE && r.PvP || s.Popup == DeathPopupMode.PvP && !r.PvP)) return;
        expires = Environment.TickCount64 + 30_000; IsOpen = true;
    }
    public void RefreshSettings()
    {
        var s = settings.DeathRecaps;
        if (!s.Enabled || s.Popup == DeathPopupMode.Off) { IsOpen = false; return; }
        if (IsOpen && view().Selected is { } r && !r.Synthetic && (s.Popup == DeathPopupMode.PvE && r.PvP || s.Popup == DeathPopupMode.PvP && !r.PvP)) IsOpen = false;
    }
    public override void PreOpenCheck()
    {
        if (IsOpen && !settings.DeathRecaps.KeepPopupOpen && Environment.TickCount64 >= expires) IsOpen = false;
    }
    public override void PreDraw()
    {
        style.Pop(); modern = settings.Theme == 1;
        Flags = modern ? SentinelModernWindowChrome.UseCustomHeader(ImGuiWindowFlags.NoFocusOnAppearing) : ImGuiWindowFlags.NoFocusOnAppearing;
        if (modern) style.PushAppShell(ImGuiHelpers.GlobalScale);
    }
    public override void PostDraw() => style.Pop();
    public override void Draw()
    {
        if (modern) SentinelModernAppShell.Draw(new("SentinelHub.DeathRecap.Modern", "Death Recap", "Recap") {
            Scale = ImGuiHelpers.GlobalScale, DeltaTime = ImGui.GetIO().DeltaTime,
            Layout = SentinelModernAppLayoutOptions.Default with { ContentPadding = 12, MinimumContentWidth = 220, MinimumContentHeight = 160 },
            ReducedMotion = settings.ReducedMotion || reducedMotion(), EnableWindowDragging = true, RequestClose = close, DrawPluginIcon = drawIcon,
        }, shell, navigation, Select, body);
        else Body();
    }
    private void Body()
    {
        panel.Summary(view(), true);
        if (ImGui.Button("Open full recap")) openFull();
        if (ImGui.Button("Dismiss")) IsOpen = false;
    }
    public void Dispose() { style.Dispose(); shell.Dispose(); }
}
