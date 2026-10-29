using System.Numerics;
using Dalamud.Bindings.ImGui;
using SentinelHub.Configuration;
using SentinelHub.TreasureMaps;
using SentinelHub.UI;

internal static unsafe class TreasureQueueUiTests
{
    private static string scenario = "";
    private static void Check(bool value, string message) { if (!value) throw new Exception(message + " (" + scenario + ")"); }
    public static void Run(ImFontPtr font)
    {
        foreach (var icon in new[] { Dalamud.Interface.FontAwesomeIcon.Trash, Dalamud.Interface.FontAwesomeIcon.ArrowUp,
            Dalamud.Interface.FontAwesomeIcon.ArrowDown, Dalamud.Interface.FontAwesomeIcon.Plus, Dalamud.Interface.FontAwesomeIcon.GripVertical,
            Dalamud.Interface.FontAwesomeIcon.Flag, Dalamud.Interface.FontAwesomeIcon.ShareAlt, Dalamud.Interface.FontAwesomeIcon.Check,
            Dalamud.Interface.FontAwesomeIcon.MapMarkerAlt, Dalamud.Interface.FontAwesomeIcon.EllipsisH, Dalamud.Interface.FontAwesomeIcon.Undo })
            Check(font.FindGlyphNoFallback((ushort)icon) != null, "Queue icon missing from actual icon font: " + icon);
        var io = ImGui.GetIO(); var oldDisplay = io.DisplaySize; var oldScale = io.FontGlobalScale;
        var actionCases = 0; var placementCases = 0;
        try
        {
            foreach (var floating in new[] { false, true }) foreach (var theme in new[] { 0, 1 })
            foreach (var scale in new[] { 1f, 1.5f, 2f }) foreach (var width in new[] { 320f, 620f, 900f })
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
                void Request(TreasureRequest request) => actions.Add(request);
                var panel = new TreasureMapsPanel(settings.TreasureMaps, () => view, Request, () => { }, () => { }, () => font);
                using var style = new SentinelCore.UI.SentinelModernStyleScope();
                using var window = new TreasureQueueWindow(settings, () => view, Request, () => { }, () => { }, () => font);
                float? scroll = null;
                Vector2 lastMorePoint = default;
                float menuLineHeight = 0, menuFontHeight = 0;
                void Body(int index, string? activate)
                {
                    if (scroll is { } y) ImGui.SetScrollY(y);
                    if (activate is not null)
                    {
                        ImGui.PushID(entries[index].Id.ToString("N"));
                        var context = ImGui.GetCurrentContext(); context.NavNextActivateId = ImGui.GetID(activate is "Move Up" or "Move Down" ? activate : "###" + activate);
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
                    var fonts = context.FontStack.Size;
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
                    Check(context.StyleVarStack.Size == styles && context.ColorStack.Size == colours && context.FontStack.Size == fonts, "Queue actions unbalanced styles/fonts");
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
                    Check(popup.Size.X >= ImGui.CalcTextSize("Add another").X + ImGui.GetFontSize() + 2 * popup.WindowPadding.X,
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
                        menuFontHeight / 2 + row * ((popup.ContentSize.Y - menuFontHeight) / 3));
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
                Frame(last, "More..."); Frame(last); Frame(last); _ = Popup(); ClosePopup();
                scroll = 0; Frame(); Frame(); scroll = null;
                var beforeFirstDown = actions.Count;
                // A tall card at large text scales may need scrolling before its controls are visible.
                for (var y = 0; y <= 600 && actions.Count == beforeFirstDown; y += 50)
                {
                    scroll = y; Frame(); Frame(); scroll = null; Frame(0, "Move Down"); Frame();
                }
                Check(actions.Count == beforeFirstDown + 1 && actions[^1] == new TreasureRequest(TreasureAction.Down, entries[0].Id), "First entry Down was inaccessible after scrolling");
                beforeDisabled = actions.Count; Frame(0, "Move Up"); Frame(0);
                Check(actions.Count == beforeDisabled, "First entry Up arrow emitted actions: " + string.Join(",", actions.Skip(beforeDisabled)));
                actionCases++;
            }
        }
        finally { io.DisplaySize = oldDisplay; io.FontGlobalScale = oldScale; io.AddMousePosEvent(-100, -100); }
        Console.WriteLine($"PASS {placementCases} queue popup viewport/width/theme/scale placements and {actionCases} full/floating native menu, arrow and all-row action flows.");
        DragLinks(font);
        CardLayouts(font);
    }
    private static void CardLayouts(ImFontPtr font)
    {
        var io = ImGui.GetIO(); var oldDisplay = io.DisplaySize; var oldScale = io.FontGlobalScale;
        var cases = 0; var inlineCases = 0;
        try
        {
            io.DisplaySize = new(1500, 3000);
            foreach (var scale in new[] { 1f, 1.2f, 1.5f, 2f }) foreach (var width in new[] { 300f, 420f, 900f })
            foreach (var compact in new[] { false, true }) foreach (var longText in new[] { false, true })
            {
                scenario = $"card scale={scale}, width={width}, compact={compact}, long={longText}"; io.FontGlobalScale = scale;
                var entry = new TreasureEntry(Guid.NewGuid(), new(longText ? string.Join(" ", Enumerable.Repeat("Map Owner", 12)) : "Map Owner", 42),
                    new(100, 401, 123456, 987654, longText ? string.Join(" ", Enumerable.Repeat("District", 14)) : "Elpis", 24.2f, 18.5f, []), DateTimeOffset.UtcNow);
                var actions = new List<TreasureRequest>(); var v = new TreasureView(true, [entry], null, false, "", "");
                var panel = new TreasureMapsPanel(new(), () => v, r => actions.Add(r), () => { }, () => { }, () => font);
                Vector2 more = default;
                void Frame(bool full = false)
                {
                    ImGui.NewFrame(); var context = ImGui.GetCurrentContext();
                    var styles = context.StyleVarStack.Size; var colours = context.ColorStack.Size; var fonts = context.FontStack.Size;
                    ImGui.SetNextWindowPos(new(20, 20), ImGuiCond.Always); ImGui.SetNextWindowSize(new(width + 32, 2800), ImGuiCond.Always);
                    ImGui.Begin("Card layout checks");
                    if (full)
                    {
                        panel.Queue(v, compact);
                        var vertices = ImGui.GetWindowDrawList().VtxBuffer; var cards = 0; var links = 0;
                        for (var j = 0; j < vertices.Size; j++)
                        {
                            if (vertices[j].Col == ImGui.ColorConvertFloat4ToU32(TreasureQueuePalette.Card)) cards++;
                            if (vertices[j].Col == ImGui.ColorConvertFloat4ToU32(TreasureQueuePalette.Link)) links++;
                        }
                        Check(cards >= 4 && links > 0, "Native queue draw data lost the dark card or blue map link");
                    }
                    else
                    {
                        TreasureMapsPanel.PushQueueStyle();
                        var cardStart = ImGui.GetCursorScreenPos(); var cardWidth = ImGui.GetContentRegionAvail().X;
                        var layout = panel.MeasureCard(entry, 0, compact, cardWidth);
                        var cached = panel.Presentations(v.Entries, compact, cardWidth);
                        Check(ReferenceEquals(cached, panel.Presentations(v.Entries, compact, cardWidth)), "Unchanged card layout was rebuilt");
                        if (cases == 0)
                        {
                            Check(!ReferenceEquals(cached, panel.Presentations(v.Entries, compact, cardWidth + 50)), "Resize reused stale card layout");
                            Check(!ReferenceEquals(cached, panel.Presentations(v.Entries.ToArray(), compact, cardWidth)), "Changed queue snapshot reused card layout");
                        }
                        var pad = 12 * Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale;
                        var start = cardStart + new Vector2(pad); var contentRight = cardStart.X + cardWidth - pad;
                        ImGui.SetCursorScreenPos(start);
                        panel.EntryLink(entry, 0, true, layout.LinkWidth);
                        if (!compact)
                        {
                            ImGui.SetCursorScreenPos(new(start.X, ImGui.GetCursorScreenPos().Y));
                            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + layout.LinkWidth);
                            ImGui.TextColored(TreasureQueuePalette.Muted, "Received " + entry.ReceivedAt.ToLocalTime().ToString("g")); ImGui.PopTextWrapPos();
                        }
                        var metadataEnd = ImGui.GetItemRectMax();
                        Check(metadataEnd.Y <= start.Y + layout.LinkHeight + 1, "Wrapped map metadata exceeded the measured card area");
                        var actionPos = layout.Inline ? new Vector2(contentRight - layout.ActionsWidth, start.Y + (layout.ContentHeight - ImGui.GetFrameHeight()) / 2)
                            : start + new Vector2(0, layout.LinkHeight + 10 * Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale);
                        if (layout.Inline) Check(metadataEnd.X < actionPos.X, "Inline metadata overlaps buttons");
                        else Check(metadataEnd.Y < actionPos.Y, "Stacked buttons overlap wrapped metadata");
                        ImGui.SetCursorScreenPos(actionPos);
                        panel.RowActions(entry, 0, 2, contentRight);
                        more = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
                        Check(ImGui.GetItemRectMin().X >= start.X - 1 && ImGui.GetItemRectMax().X <= contentRight + 1, "Wrapped buttons escaped the card padding");
                        Check(ImGui.GetItemRectMax().Y <= start.Y + layout.ContentHeight + 2, "Wrapped controls exceeded card bounds");
                        if (layout.Inline) Check(Math.Abs(ImGui.GetItemRectMin().Y - actionPos.Y) <= 1, "Inline actions unexpectedly wrapped");
                        TreasureMapsPanel.PopQueueStyle();
                    }
                    ImGui.End();
                    Check(context.StyleVarStack.Size == styles && context.ColorStack.Size == colours && context.FontStack.Size == fonts, "Card renderer leaked styles/fonts");
                    ImGui.Render();
                }
                io.AddMousePosEvent(-100, -100); Frame(); Frame(); Frame();
                io.AddMousePosEvent(more.X, more.Y); io.AddMouseButtonEvent(0, true); Frame();
                io.AddMouseButtonEvent(0, false); Frame(); Frame(); _ = Popup();
                io.AddKeyEvent(ImGuiKey.Escape, true); Frame(); io.AddKeyEvent(ImGuiKey.Escape, false); Frame();
                io.AddMousePosEvent(-100, -100); Frame(true); Frame(true);
                if (width == 900 && scale == 1) inlineCases++; cases++;
                Check(actions.Count == 0, "Measuring, rendering or opening More changed queue state");
            }
        }
        finally { io.DisplaySize = oldDisplay; io.FontGlobalScale = oldScale; io.AddMousePosEvent(-100, -100); }
        Check(inlineCases > 0, "No wide inline card layout exercised");
        Console.WriteLine($"PASS {cases} dark card layout, long wrapped text, inline/stacked geometry, native More mouse clicks, glyph draw data and style isolation cases.");
    }
    private static void DragLinks(ImFontPtr font)
    {
        var io = ImGui.GetIO(); var oldDisplay = io.DisplaySize; var oldScale = io.FontGlobalScale;
        var cases = 0;
        try
        {
            foreach (var scale in new[] { 1f, 1.5f, 2f }) foreach (var theme in new[] { 0, 1 })
            foreach (var crossWindow in new[] { false, true })
            {
                scenario = $"drag scale={scale}, theme={theme}, crossWindow={crossWindow}";
                io.DisplaySize = new(1000, 760); io.FontGlobalScale = scale;
                var entries = Enumerable.Range(0, 3).Select(i => new TreasureEntry(Guid.NewGuid(), new("Owner " + i, 42),
                    new(100, 401, i * 1000, 123456, "District", 24.2f, 18.5f, []), DateTimeOffset.UtcNow)).ToArray();
                var queue = new TreasureQueue(); queue.Bind(new() { Entries = entries.ToList() });
                var requests = new List<TreasureRequest>();
                var v = new TreasureView(true, entries, null, false, "", "");
                var panel = new TreasureMapsPanel(new(), () => v, r => requests.Add(r), () => { }, () => { }, () => font);
                using var style = new SentinelCore.UI.SentinelModernStyleScope();
                var rectangles = new (Vector2 Min, Vector2 Max)[2, 3];
                void Frame(bool available = true)
                {
                    ImGui.NewFrame();
                    if (theme == 1) style.PushAppShell(Dalamud.Interface.Utility.ImGuiHelpers.GlobalScale);
                    for (var side = 0; side < 2; side++)
                    {
                        ImGui.SetNextWindowPos(new(side * 470 + 10, 20), ImGuiCond.Always);
                        ImGui.SetNextWindowSize(new(440, 700), ImGuiCond.Always);
                        ImGui.Begin("Drag queue " + side);
                        for (var i = 0; i < entries.Length; i++)
                        {
                            ImGui.PushID(entries[i].Id.ToString("N"));
                            panel.EntryLink(entries[i], i, available);
                            rectangles[side, i] = (ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
                            ImGui.PopID(); ImGui.Separator();
                        }
                        ImGui.End();
                    }
                    if (theme == 1) style.Pop();
                    ImGui.Render();
                }
                Vector2 Point(int side, int row, bool after = false)
                {
                    var (min, max) = rectangles[side, row];
                    return new(min.X + 20, min.Y + (max.Y - min.Y) * (after ? .75f : .25f));
                }
                void Drag(int from, int target, bool after, bool cancel = false, bool available = true)
                {
                    var p = Point(0, from); io.AddMousePosEvent(p.X, p.Y); Frame(available);
                    io.AddMouseButtonEvent(0, true); Frame(available);
                    io.AddMousePosEvent(p.X + 24, p.Y); Frame(available); Frame(available);
                    p = cancel ? new(980, 750) : Point(crossWindow ? 1 : 0, target, after);
                    io.AddMousePosEvent(p.X, p.Y); Frame(available); Frame(available);
                    io.AddMouseButtonEvent(0, false); Frame(available); Frame(available);
                }
                io.AddMousePosEvent(-100, -100); Frame(); Frame(); Frame();
                var click = Point(0, 1); io.AddMousePosEvent(click.X, click.Y); Frame();
                io.AddMouseButtonEvent(0, true); Frame(); io.AddMouseButtonEvent(0, false); Frame();
                Check(requests.SequenceEqual(new[] { new TreasureRequest(TreasureAction.Open, entries[1].Id) }), "Map link click did not open the selected flag");
                requests.Clear();
                Drag(2, 0, false);
                Check(requests.SequenceEqual(new[] { new TreasureRequest(TreasureAction.MoveBefore, entries[2].Id, entries[0].Id) }),
                    "Dragging a map link did not deliver exactly one atomic MoveBefore: " + string.Join(",", requests));
                Check(queue.MoveRelative(requests[0].EntryId, requests[0].TargetId, false)
                    && queue.Entries.Select(e => e.Id).SequenceEqual(new[] { entries[2].Id, entries[0].Id, entries[1].Id }), "Drop changed the wrong identities");
                requests.Clear(); Drag(0, 2, true);
                Check(requests.SequenceEqual(new[] { new TreasureRequest(TreasureAction.MoveAfter, entries[0].Id, entries[2].Id) }),
                    "Dragging below a link did not select MoveAfter");
                requests.Clear(); Drag(1, 1, false); Check(requests.Count == 0, "Self-drop emitted work");
                Drag(2, 0, false, cancel: true); Check(requests.Count == 0, "Canceled drag emitted work");
                Drag(2, 0, false, available: false); Check(requests.Count == 0, "Unavailable queue accepted a drop");
                io.AddMousePosEvent(-100, -100); Frame(); cases++;
            }
        }
        finally { io.DisplaySize = oldDisplay; io.FontGlobalScale = oldScale; io.AddMousePosEvent(-100, -100); }
        Console.WriteLine($"PASS {cases} native mouse drag/drop flows across themes/scales and between queue windows; before/after, self-drop, cancel and disabled guards.");
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
