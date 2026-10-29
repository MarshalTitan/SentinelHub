using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using SentinelCore.UI;
using SentinelHub.Configuration;
using SentinelHub.TreasureMaps;
namespace SentinelHub.UI;

public sealed class TreasureQueueWindow : Window, IDisposable
{
    public const string WindowId = "Treasure Map Queue##SentinelHub-TreasureQueue";
    private readonly HubSettings settings;
    private readonly TreasureMapsPanel panel;
    private readonly Func<TreasureView> view;
    private readonly Action save, drawQueue, collapse, close;
    private readonly Func<bool> systemReducedMotion;
    private readonly Func<ImFontPtr> icons;
    private readonly Action<SentinelModernIconDrawContext> headerIcon;
    private readonly SentinelModernNavItem[] navigation;
    private static readonly Action<string> KeepQueue = _ => { };
    private static readonly string MapGlyph = FontAwesomeIcon.Map.ToIconString();
    private readonly SentinelModernStyleScope style = new();
    private readonly SentinelModernAppShellState shell = new();
    private bool paddingPushed, modern;
    private Vector2 frameSize;
    private Vector2? pendingSize;
    public TreasureQueueWindow(HubSettings settings, Func<TreasureView> view, Action<TreasureRequest> request,
        Action changed, Action save, Func<ImFontPtr>? iconFont = null, Func<bool>? systemReducedMotion = null) : base(WindowId)
    {
        this.settings = settings; this.view = view; this.save = save;
        icons = iconFont ?? (() => UiBuilder.IconFont);
        this.systemReducedMotion = systemReducedMotion ?? (() => false);
        panel = new(settings.TreasureMaps, view, request, changed, save, icons);
        drawQueue = DrawQueue; collapse = ToggleCollapse; close = Close;
        headerIcon = c => DrawIcon(c.DrawList, c.Minimum, c.Maximum, SentinelModernPalette.Text);
        navigation = [new("Queue", null, "Treasure Maps") { DrawIcon = c => DrawIcon(c.DrawList, c.Minimum, c.Maximum, c.Colour) }];
        var s = settings.TreasureMaps;
        Position = new(s.FloatingX, s.FloatingY); PositionCondition = ImGuiCond.FirstUseEver;
        Size = new(s.FloatingWidth, s.FloatingHeight); SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new() { MinimumSize = new(300, 220), MaximumSize = new(2000, 2000) };
    }
    public override void PreOpenCheck() => IsOpen = settings.TreasureMaps.Enabled && settings.TreasureMaps.FloatingOpen;
    public override void PreDraw()
    {
        PopPadding(); style.Pop();
        if (modern && settings.Theme != 1 && settings.TreasureMaps.FloatingCollapsed)
            pendingSize = new(settings.TreasureMaps.FloatingWidth, settings.TreasureMaps.FloatingHeight);
        modern = settings.Theme == 1;
        var scale = ImGuiHelpers.GlobalScale; var collapsed = modern && settings.TreasureMaps.FloatingCollapsed;
        Flags = modern ? SentinelModernWindowChrome.UseCustomHeader(ImGuiWindowFlags.None) : ImGuiWindowFlags.None;
        if (modern)
        {
            style.PushAppShell(scale);
            ImGui.SetNextWindowCollapsed(false, ImGuiCond.Always);
        }
        else
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12) * scale); paddingPushed = true;
        }
        // WindowHost scales Size and constraints itself; stored geometry remains native pixels.
        SizeConstraints = new() { MinimumSize = new(300, collapsed ? 56 : 220),
            MaximumSize = new(2000, collapsed ? 56 : 2000) };
        if (pendingSize is { } next) { Size = next / scale; SizeCondition = ImGuiCond.Always; pendingSize = null; }
        else
        {
            Size = new(settings.TreasureMaps.FloatingWidth / scale, collapsed ? 56 : settings.TreasureMaps.FloatingHeight / scale);
            SizeCondition = ImGuiCond.FirstUseEver;
        }
    }
    public override void PostDraw()
    {
        PopPadding(); style.Pop();
        // Persist either native Classic X or the Core close callback before the next open check.
        if (!IsOpen) OnClose();
    }
    private void PopPadding() { if (!paddingPushed) return; ImGui.PopStyleVar(); paddingPushed = false; }
    private void Close() { IsOpen = false; OnClose(); }
    public override void OnClose()
    {
        if (!settings.TreasureMaps.FloatingOpen) return;
        settings.TreasureMaps.FloatingOpen = false; save();
    }
    public override void Draw()
    {
        frameSize = ImGui.GetWindowSize();
        if (modern)
        {
            SentinelModernAppShell.Draw(new("SentinelHub.TreasureQueue.Modern", "Treasure Maps", "Queue")
            {
                Scale = ImGuiHelpers.GlobalScale, DeltaTime = ImGui.GetIO().DeltaTime,
                Layout = SentinelModernAppLayoutOptions.Default with { ContentPadding = 12, MinimumContentWidth = 220, MinimumContentHeight = 160 },
                ReducedMotion = settings.ReducedMotion || systemReducedMotion(), EnableWindowDragging = true,
                RequestCollapse = collapse, CollapseTooltip = settings.TreasureMaps.FloatingCollapsed ? "Expand" : "Minimize",
                RequestClose = close, DrawPluginIcon = headerIcon,
            }, shell, navigation, KeepQueue, drawQueue);
        }
        else panel.Queue(view(), true);
        SaveGeometry();
    }
    private void DrawQueue() { if (!settings.TreasureMaps.FloatingCollapsed) panel.Queue(view(), true); }
    private void ToggleCollapse()
    {
        var s = settings.TreasureMaps; var scale = ImGuiHelpers.GlobalScale;
        if (!s.FloatingCollapsed)
        {
            s.FloatingWidth = frameSize.X; s.FloatingHeight = frameSize.Y;
            s.FloatingCollapsed = true; pendingSize = new(frameSize.X, 56 * scale);
        }
        else
        {
            s.FloatingCollapsed = false;
            pendingSize = new(Math.Max(300 * scale, frameSize.X), Math.Max(220 * scale, s.FloatingHeight));
        }
        save();
    }
    private void SaveGeometry()
    {
        if (pendingSize is not null || ImGui.IsMouseDown(ImGuiMouseButton.Left)) return;
        var p = ImGui.GetWindowPos(); var size = ImGui.GetWindowSize(); var s = settings.TreasureMaps;
        var expanded = !modern || !s.FloatingCollapsed;
        if (Math.Abs(p.X - s.FloatingX) <= 1 && Math.Abs(p.Y - s.FloatingY) <= 1
            && Math.Abs(size.X - s.FloatingWidth) <= 1 && (!expanded || Math.Abs(size.Y - s.FloatingHeight) <= 1)) return;
        s.FloatingX = p.X; s.FloatingY = p.Y; s.FloatingWidth = size.X;
        if (expanded) s.FloatingHeight = size.Y;
        save();
    }
    private void DrawIcon(ImDrawListPtr draw, Vector2 min, Vector2 max, Vector4 colour)
    {
        ImGui.PushFont(icons());
        try { var size = ImGui.CalcTextSize(MapGlyph); draw.AddText(min + (max - min - size) / 2, ImGui.ColorConvertFloat4ToU32(colour), MapGlyph); }
        finally { ImGui.PopFont(); }
    }
    public void Dispose() { style.Dispose(); shell.Dispose(); }
}
