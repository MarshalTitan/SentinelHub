using System.Numerics;
using Dalamud.Bindings.ImGui;
using SentinelHub.Configuration;
using SentinelHub.TreasureMaps;
using SentinelHub.UI;

internal static unsafe class TreasureQueueUiTests
{
    private static string scenario = "";
    private static void Check(bool value, string message) { if (!value) throw new Exception(message + " (" + scenario + ")"); }
    public static void Run()
    {
        var io = ImGui.GetIO(); var oldDisplay = io.DisplaySize; var oldScale = io.FontGlobalScale;
        var actionCases = 0; var placementCases = 0;
        try
        {
            foreach (var floating in new[] { false, true }) foreach (var theme in new[] { 0, 1 })
            foreach (var scale in new[] { 1f, 1.5f, 2f }) foreach (var width in new[] { 320f, 620f })
            {
                scenario = $"floating={floating}, theme={theme}, scale={scale}, width={width}";
                io.DisplaySize = new(1000, 760); io.FontGlobalScale = scale;
                io.AddMousePosEvent(-100, -100);
                var settings = new HubSettings { Theme = theme };
                settings.TreasureMaps.Enabled = true; settings.TreasureMaps.FloatingOpen = true;
                var entries = Enumerable.Range(0, 12).Select(i => new TreasureEntry(Guid.NewGuid(), new("Queue Owner " + i, 42),
                    new(100, 401, 123456 + i * 1000, -789012, "Fixture District", 24.2f, 18.5f, []), DateTimeOffset.UtcNow)).ToArray();
                var view = new TreasureView(true, entries, null, true, "Listening", "");
                var actions = new List<TreasureRequest>();
                void Request(TreasureAction action, Guid id) => actions.Add(new(action, id));
                var panel = new TreasureMapsPanel(settings.TreasureMaps, () => view, Request, () => { }, () => { });
                using var style = new SentinelCore.UI.SentinelModernStyleScope();
                using var window = new TreasureQueueWindow(settings, () => view, Request, () => { }, () => { });
                float? scroll = null;
                Vector2 lastMorePoint = default;
                float menuLineHeight = 0, menuFontHeight = 0;
                void Body(int index, string? activate)
                {
                    if (scroll is { } y) ImGui.SetScrollY(y);
                    if (activate is not null)
                    {
                        ImGui.PushID(entries[index].Id.ToString("N"));
                        var context = ImGui.GetCurrentContext(); context.NavNextActivateId = ImGui.GetID(activate);
                        ImGui.PopID();
                    }
                    if (floating) window.Draw(); else panel.Queue(view, false);
                    lastMorePoint = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
                    menuLineHeight = ImGui.GetTextLineHeightWithSpacing(); menuFontHeight = ImGui.GetFontSize();
                }
                void Frame(int index = 1, string? activate = null)
                {
                    ImGui.NewFrame();
                    var context = ImGui.GetCurrentContext(); var styles = context.StyleVarStack.Size; var colours = context.ColorStack.Size;
                    if (floating) { window.PreOpenCheck(); window.PreDraw(); }
                    else if (theme == 1) style.PushAppShell(Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale);
                    ImGui.SetNextWindowPos(new(15, 15), ImGuiCond.Always); ImGui.SetNextWindowSize(new(width, 700), ImGuiCond.Always);
                    var open = true;
                    ImGui.Begin(floating ? TreasureQueueWindow.WindowId : "Queue actions Hub", ref open);
                    if (!floating) ImGui.BeginChild("queue-scroll", Vector2.Zero);
                    Body(index, activate);
                    if (!floating) ImGui.EndChild();
                    ImGui.End();
                    if (floating) window.PostDraw();
                    else if (theme == 1) style.Pop();
                    Check(context.StyleVarStack.Size == styles && context.ColorStack.Size == colours, "Queue actions unbalanced styles");
                    ImGui.Render();
                }
                void Activate(string label, TreasureAction action, int index)
                {
                    var before = actions.Count; Frame(index, label); Frame(index);
                    Check(actions.Count == before + 1 && actions[^1] == new TreasureRequest(action, entries[index].Id),
                        $"Queue control {label} failed in {(floating ? "floating" : "Hub")}, scale={scale}, index={index}");
                }
                void ClosePopup()
                {
                    io.AddKeyEvent(ImGuiKey.Escape, true); Frame();
                    io.AddKeyEvent(ImGuiKey.Escape, false); Frame();
                    io.AddMousePosEvent(-100, -100);
                }
                for (var i = 0; i < 3; i++) Frame();
                var body = BodyWindow(floating);
                Check(body.ScrollMax.Y > 0, "Full queue is not scrollable");
                Check(body.ContentSize.X <= body.Size.X + 1 && !body.ScrollbarX, "Queue controls overflow narrow content");
                var menuIndex = entries.Length - 2;
                scroll = 1_000_000; Frame(); Frame(); scroll = null;
                foreach (var anchor in new[] { new Vector2(2, 2), new(998, 2), new(2, 758), new(998, 758) })
                {
                    io.AddMousePosEvent(anchor.X, anchor.Y);
                    Frame(menuIndex, "More..."); Frame(); Frame();
                    var popup = Popup(); var viewport = ImGui.GetMainViewport();
                    Check(popup.Pos.X >= viewport.WorkPos.X && popup.Pos.Y >= viewport.WorkPos.Y
                        && popup.Pos.X + popup.Size.X <= viewport.WorkPos.X + viewport.WorkSize.X + 1
                        && popup.Pos.Y + popup.Size.Y <= viewport.WorkPos.Y + viewport.WorkSize.Y + 1,
                        "Queue popup escaped the game viewport");
                    Check(popup.Size.X >= ImGui.CalcTextSize("Add another").X + 2 * popup.WindowPadding.X,
                        "Queue menu text clipped by intrinsic width");
                    Check(popup.ContentSize.X <= popup.Size.X + 1 && !popup.ScrollbarX, "Queue popup has horizontal clipping");
                    Check(popup.ClipRect.Min.X <= popup.Pos.X + popup.WindowPadding.X + 1
                        && popup.ClipRect.Max.X >= popup.Pos.X + popup.Size.X - popup.WindowPadding.X - 1
                        && popup.ClipRect.Max.Y >= popup.Pos.Y + popup.Size.Y - popup.WindowPadding.Y - 1,
                        "Queue popup inherited its parent's clipping boundary");
                    Check((popup.Flags & ImGuiWindowFlags.Popup) != 0 && (popup.Flags & ImGuiWindowFlags.ChildWindow) == 0,
                        "Queue menu was rendered in a clipped child");
                    ClosePopup(); placementCases++;
                }
                foreach (var (action, row) in new[] { (TreasureAction.Delete, 0), (TreasureAction.Up, 1), (TreasureAction.Down, 2), (TreasureAction.AddCopy, 3) })
                {
                    io.AddMousePosEvent(700, 450); Frame(menuIndex, "More..."); Frame(); Frame();
                    var popup = Popup(); var before = actions.Count;
                    var center = popup.Pos + popup.WindowPadding + new Vector2(popup.Size.X / 3,
                        menuFontHeight / 2 + row * menuLineHeight);
                    io.AddMousePosEvent(center.X, center.Y); io.AddMouseButtonEvent(0, true); Frame();
                    io.AddMouseButtonEvent(0, false); Frame();
                    Check(actions.Count == before + 1 && actions[^1] == new TreasureRequest(action, entries[menuIndex].Id),
                        $"Native menu action {action} not clickable in {(floating ? "floating" : "Hub")}, scale={scale}, width={width}");
                    io.AddMousePosEvent(-100, -100); Frame();
                }
                scroll = 1_000_000; Frame(); Frame(); scroll = null;
                var last = entries.Length - 1;
                Activate("Open Flag", TreasureAction.Open, last); Activate("Share", TreasureAction.Share, last);
                Activate("Complete", TreasureAction.Complete, last); Activate("Move Up", TreasureAction.Up, last);
                var beforeDisabled = actions.Count; Frame(last, "Move Down"); Frame(last);
                Check(actions.Count == beforeDisabled, $"Last entry Down arrow emitted actions: floating={floating}, theme={theme}, scale={scale}, width={width}; " + string.Join(",", actions.Skip(beforeDisabled)));
                // Later rows must expose More too, rather than truncating the floating queue.
                scroll = 1_000_000; Frame(); Frame(); scroll = null;
                io.AddMousePosEvent(lastMorePoint.X, lastMorePoint.Y); io.AddMouseButtonEvent(0, true); Frame(last);
                io.AddMouseButtonEvent(0, false); Frame(last); Frame(last); _ = Popup(); ClosePopup();
                scroll = 0; Frame(); Frame(); scroll = null;
                Activate("Move Down", TreasureAction.Down, 0);
                beforeDisabled = actions.Count; Frame(0, "Move Up"); Frame(0);
                Check(actions.Count == beforeDisabled, "First entry Up arrow emitted actions: " + string.Join(",", actions.Skip(beforeDisabled)));
                actionCases++;
            }
        }
        finally { io.DisplaySize = oldDisplay; io.FontGlobalScale = oldScale; io.AddMousePosEvent(-100, -100); }
        Console.WriteLine($"PASS {placementCases} queue popup viewport/width/theme/scale placements and {actionCases} full/floating native menu, arrow and all-row action flows.");
    }
    private static ImGuiWindowPtr BodyWindow(bool floating)
    {
        var windows = ImGui.GetCurrentContext().Windows;
        for (var i = 0; i < windows.Size; i++)
        {
            var w = windows[i]; var name = System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)w.Name)!;
            if (w.Active && (floating ? name == TreasureQueueWindow.WindowId : name.Contains("queue-scroll"))) return w;
        }
        throw new Exception("Queue content window not found");
    }
    private static ImGuiWindowPtr Popup()
    {
        var windows = ImGui.GetCurrentContext().Windows;
        for (var i = 0; i < windows.Size; i++)
            if (windows[i].Active && (windows[i].Flags & ImGuiWindowFlags.Popup) != 0) return windows[i];
        throw new Exception("Queue More popup did not open (" + scenario + ")");
    }
}
