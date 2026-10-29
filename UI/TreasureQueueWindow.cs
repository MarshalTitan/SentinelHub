using System.Numerics;
using Dalamud.Bindings.ImGui;
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
    private readonly Action save;
    private readonly SentinelModernStyleScope style = new();
    private bool paddingPushed;
    public TreasureQueueWindow(HubSettings settings, Func<TreasureView> view, Action<TreasureRequest> request,
        Action changed, Action save, Func<ImFontPtr>? iconFont = null) : base(WindowId)
    {
        this.settings = settings; this.view = view; this.save = save;
        panel = new(settings.TreasureMaps, view, request, changed, save, iconFont);
        var s = settings.TreasureMaps;
        Position = new(s.FloatingX, s.FloatingY); PositionCondition = ImGuiCond.FirstUseEver;
        Size = new(s.FloatingWidth, s.FloatingHeight); SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new() { MinimumSize = new(300, 220), MaximumSize = new(2000, 2000) };
    }
    public override void PreOpenCheck() => IsOpen = settings.TreasureMaps.Enabled && settings.TreasureMaps.FloatingOpen;
    public override void PreDraw()
    {
        PopPadding(); style.Pop(); if (settings.Theme == 1) style.PushAppShell(ImGuiHelpers.GlobalScale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12) * ImGuiHelpers.GlobalScale); paddingPushed = true;
    }
    public override void PostDraw()
    {
        PopPadding(); style.Pop();
        // The host applies the title-bar X before PostDraw, but invokes OnClose
        // next frame. Save now so PreOpenCheck cannot reopen the closed window.
        if (!IsOpen) OnClose();
    }
    private void PopPadding() { if (!paddingPushed) return; ImGui.PopStyleVar(); paddingPushed = false; }
    public override void OnClose()
    {
        if (!settings.TreasureMaps.FloatingOpen) return;
        settings.TreasureMaps.FloatingOpen = false; save();
    }
    public override void Draw()
    {
        panel.Queue(view(), true);
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var p = ImGui.GetWindowPos(); var size = ImGui.GetWindowSize(); var s = settings.TreasureMaps;
            if (Math.Abs(p.X - s.FloatingX) > 1 || Math.Abs(p.Y - s.FloatingY) > 1
                || Math.Abs(size.X - s.FloatingWidth) > 1 || Math.Abs(size.Y - s.FloatingHeight) > 1)
            { s.FloatingX = p.X; s.FloatingY = p.Y; s.FloatingWidth = size.X; s.FloatingHeight = size.Y; save(); }
        }
    }
    public void Dispose() => style.Dispose();
}
