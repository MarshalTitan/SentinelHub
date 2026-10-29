using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using SentinelCore.UI;
using SentinelHub.Configuration;
using SentinelHub.TreasureMaps;
using SentinelHub.UI;

internal static unsafe class TreasureQueueShellTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run(ImFontPtr font)
    {
        var io = ImGui.GetIO(); var oldSize = io.DisplaySize; var oldScale = io.FontGlobalScale; var cases = 0;
        try
        {
            foreach (var scale in new[] { 1f, 1.2f, 1.5f, 2f }) foreach (var reduced in new[] { false, true })
            {
                io.DisplaySize = new(2400, 1800); io.FontGlobalScale = scale; io.AddMousePosEvent(-100, -100);
                var settings = new HubSettings { Theme = 1 };
                settings.TreasureMaps.Enabled = settings.TreasureMaps.FloatingOpen = true;
                var entries = Enumerable.Range(0, 12).Select(i => new TreasureEntry(Guid.NewGuid(), new("Map Owner " + i, 42),
                    new(100, 401, 123456, 987654, "Elpis", 24.2f, 18.5f, []), DateTimeOffset.UtcNow)).ToArray();
                var state = new CharacterTreasureQueue { Entries = entries.ToList() }; settings.TreasureMaps.Characters["0000000000000001"] = state;
                var v = new TreasureView(true, entries, null, false, "", "");
                var reads = 0; var actions = 0; var saves = 0; var motionReads = 0;
                TreasureView View() { reads++; return v; }
                using var window = new TreasureQueueWindow(settings, View, _ => actions++, () => { }, () => saves++, () => font,
                    () => { motionReads++; return reduced; });
                Vector2 minPoint = default, closePoint = default, rootSize = default;
                void Frame(TreasureQueueWindow? target = null, HubSettings? config = null)
                {
                    var active = target ?? window; var cfg = config ?? settings;
                    ImGui.NewFrame(); var context = ImGui.GetCurrentContext();
                    var styles = context.StyleVarStack.Size; var colours = context.ColorStack.Size; var fonts = context.FontStack.Size;
                    active.PreOpenCheck();
                    if (active.IsOpen)
                    {
                        active.PreDraw();
                        if (cfg.Theme == 1)
                        {
                            Check(ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg] == SentinelModernPalette.Canvas, "Floating shell overrides Core Canvas");
                            Check(ImGui.GetStyle().WindowPadding == Vector2.Zero && SentinelModernWindowChrome.HasSingleCustomHeader(active.Flags), "Floating shell lost Core chrome policy");
                        }
                        else Check((active.Flags & ImGuiWindowFlags.NoTitleBar) == 0, "Classic lost its native title bar");
                        // Match WindowHost's single conversion from logical dimensions to native pixels.
                        var requested = active.SizeCondition == ImGuiCond.Always && active.Size is { } next ? next * scale : new Vector2(420, 520) * scale;
                        var constraints = active.SizeConstraints!.Value;
                        ImGui.SetNextWindowSizeConstraints(constraints.MinimumSize * scale, constraints.MaximumSize * scale);
                        ImGui.SetNextWindowPos(new(50, 50), ImGuiCond.Always); ImGui.SetNextWindowSize(requested, ImGuiCond.Always);
                        var open = active.IsOpen;
                        if (ImGui.Begin(TreasureQueueWindow.WindowId, ref open, active.Flags)) active.Draw();
                        rootSize = ImGui.GetWindowSize(); var pos = ImGui.GetWindowPos();
                        minPoint = pos + new Vector2(rootSize.X - 65 * scale, 28 * scale);
                        closePoint = pos + new Vector2(rootSize.X - 29 * scale, 28 * scale);
                        if (cfg.Theme == 1) Check(!context.CurrentWindow.ScrollbarY, "Core root acquired a scrollbar");
                        ImGui.End(); if (!open) active.IsOpen = false; active.PostDraw();
                    }
                    Check(context.StyleVarStack.Size == styles && context.ColorStack.Size == colours && context.FontStack.Size == fonts, "Core floating shell leaked style/font stacks");
                    ImGui.Render();
                }
                ImGuiWindowPtr Child(string token)
                {
                    var windows = ImGui.GetCurrentContext().Windows;
                    for (var i = 0; i < windows.Size; i++)
                    {
                        var w = windows[i]; var name = Marshal.PtrToStringUTF8((nint)w.Name)!;
                        if (w.Active && name.StartsWith(TreasureQueueWindow.WindowId + "/") && name.Contains(token)) return w;
                    }
                    throw new Exception("Core floating child missing: " + token);
                }
                void Click(Vector2 p, TreasureQueueWindow? target = null, HubSettings? config = null)
                {
                    io.AddMousePosEvent(p.X, p.Y); Frame(target, config); io.AddMouseButtonEvent(0, true); Frame(target, config);
                    io.AddMouseButtonEvent(0, false); Frame(target, config); io.AddMousePosEvent(-100, -100); Frame(target, config);
                }
                Frame(); Frame(); Frame();
                Check((Child("##Modern2Header").Flags & ImGuiWindowFlags.NoScrollbar) != 0, "Core header scrolls");
                Check(Child("##Modern2Page").ScrollMax.Y > 0, "Core queue page is not scrollable");
                Check(motionReads > 0, "Core shell omitted the system reduced-motion provider");
                var fullHeight = settings.TreasureMaps.FloatingHeight;
                Click(minPoint);
                Check(settings.TreasureMaps.FloatingCollapsed && Math.Abs(rootSize.Y - 56 * scale) < 1, "Core minimize did not produce a header-only window");
                var before = reads; Frame(); Frame();
                Check(reads == before && settings.TreasureMaps.FloatingHeight == fullHeight, "Minimized shell read queue state or overwrote expanded height");
                var recovered = JsonSerializer.Deserialize<HubSettings>(JsonSerializer.Serialize(settings))!; recovered.Normalize();
                Check(recovered.TreasureMaps.FloatingCollapsed && recovered.TreasureMaps.FloatingHeight == fullHeight
                    && recovered.TreasureMaps.Characters["0000000000000001"].Entries.Select(e => e.Id).SequenceEqual(entries.Select(e => e.Id)), "Collapse persistence damaged queue/geometry");
                Click(minPoint);
                Check(!settings.TreasureMaps.FloatingCollapsed && Math.Abs(rootSize.Y - fullHeight) < 1, "Core expand did not restore full height");
                var beforeClose = saves; Click(closePoint);
                Check(!window.IsOpen && !settings.TreasureMaps.FloatingOpen && saves == beforeClose + 1, "Core X close did not save exactly once");
                before = reads; Frame(); Frame(); Check(reads == before && actions == 0, "Closed Core shell rendered queue or sent map actions");
                window.OnClose(); Check(saves == beforeClose + 1, "Deferred close saved twice");
                settings.TreasureMaps.FloatingOpen = true; Frame();
                Check(window.IsOpen, "Core queue did not reopen from the shared preference");
                Click(minPoint); settings.Theme = 0; Frame(); Frame();
                Check(Math.Abs(rootSize.Y - fullHeight) < 1 && settings.TreasureMaps.FloatingHeight == fullHeight, "Switching to Classic damaged expanded geometry");
                settings.Theme = 1; Frame(); Frame();
                Check(settings.TreasureMaps.FloatingCollapsed && Math.Abs(rootSize.Y - 56 * scale) < 1, "Returning to Modern lost the minimized preference");
                recovered = JsonSerializer.Deserialize<HubSettings>(JsonSerializer.Serialize(settings))!; recovered.Normalize();
                using var reload = new TreasureQueueWindow(recovered, View, _ => actions++, () => { }, () => saves++, () => font);
                before = reads; Frame(reload, recovered); Frame(reload, recovered);
                Check(reads == before && Math.Abs(rootSize.Y - 56 * scale) < 1 && recovered.TreasureMaps.FloatingHeight == fullHeight,
                    "Reloading a minimized shell drew queue data, rescaled the header twice or lost expanded height");
                Click(closePoint, reload, recovered);
                Check(!recovered.TreasureMaps.FloatingOpen && recovered.TreasureMaps.FloatingHeight == fullHeight, "Minimized X close damaged saved geometry");
                cases++;
            }
        }
        finally { io.DisplaySize = oldSize; io.FontGlobalScale = oldScale; io.AddMousePosEvent(-100, -100); }
        Console.WriteLine($"PASS {cases} Core queue shell canvas/header/page, reduced-motion, native minimize/expand/X, persistence and dormant closed/minimized flows.");
    }
}
