using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using SentinelCore.UI;
using SentinelHub.TreasureMaps;
namespace SentinelHub.UI;

public sealed class TreasureMapsPanel(TreasureSettings settings, Func<TreasureView> view,
    Action<TreasureRequest> request, Action changed, Action save, Func<ImFontPtr>? iconFont = null)
{
    internal const string DragPayload = "SHUB_TREASURE_QUEUE";
    private readonly Func<ImFontPtr> icons = iconFont ?? (() => UiBuilder.IconFont);
    private bool confirmClear;
    private static readonly Dictionary<FontAwesomeIcon, string> Glyphs = new[] { FontAwesomeIcon.GripVertical, FontAwesomeIcon.MapMarkerAlt,
        FontAwesomeIcon.Flag, FontAwesomeIcon.ShareAlt, FontAwesomeIcon.Check, FontAwesomeIcon.EllipsisH, FontAwesomeIcon.Undo,
        FontAwesomeIcon.Trash, FontAwesomeIcon.ArrowUp, FontAwesomeIcon.ArrowDown, FontAwesomeIcon.Plus }.ToDictionary(i => i, i => i.ToIconString());
    private readonly Dictionary<string, (string Display, string Widget)> captions = new();
    private (nint Font, nint Icons, float Size, float Spacing) captionKey;
    private TreasureEntry[]? presentedEntries;
    private CardVisual[] presented = [];
    private (float Width, float Size, float Scale, nint Font, nint Icons, Vector2 Padding, Vector2 Spacing, float Inner, bool Compact) presentationKey;
    public void Draw()
    {
        Toggle("Enable Treasure Maps", settings.Enabled, value => settings.Enabled = value);
        ImGui.BeginDisabled(!settings.Enabled);
        var v = view();
        ImGui.TextWrapped("Your deciphered map");
        ImGui.TextWrapped(v.Current?.Location.Label ?? "View or decipher a treasure map to identify its location.");
        ImGui.TextWrapped(v.Detection);
        ImGui.BeginDisabled(!v.Available || v.Current is null);
        if (Button("Open My Map")) Send(TreasureAction.OpenMine);
        if (Button("Add My Map to Queue")) Send(TreasureAction.AddMine);
        ImGui.EndDisabled();
        if (Button(settings.FloatingOpen ? "Hide Floating Queue" : "Show Floating Queue"))
        { settings.FloatingOpen = !settings.FloatingOpen; changed(); save(); }
        ImGui.Separator(); Queue(v, false); ImGui.Separator();
        ImGui.TextWrapped("Queue settings");
        Toggle("Automatically detect deciphered maps", settings.DetectDeciphered, value => settings.DetectDeciphered = value);
        Toggle("Show location after deciphering", settings.OpenAfterDecipher, value => settings.OpenAfterDecipher = value);
        Toggle("Show location when viewing a deciphered map", settings.OpenWhenViewed, value => settings.OpenWhenViewed = value);
        Toggle("Add deciphered maps to the queue", settings.AddOwnMap, value => settings.AddOwnMap = value);
        Toggle("Capture Party map flags", settings.CaptureParty, value => settings.CaptureParty = value);
        Toggle("Capture Alliance map flags", settings.CaptureAlliance, value => settings.CaptureAlliance = value);
        var maximum = settings.MaximumEntries; ImGui.SetNextItemWidth(-1);
        if (ImGui.SliderInt("##TreasureMaximum", ref maximum, 5, 200)) { settings.MaximumEntries = maximum; changed(); save(); }
        ImGui.TextWrapped("Maximum queued entries (existing entries are never evicted)");
        ImGui.TextWrapped("Share restores the saved flag and copies /p <flag>. Paste it into chat and press Enter. Nothing is posted automatically.");
        ImGui.TextWrapped("Repeated flags from one owner are ignored. Different owners remain independent. Use Add another for a second map at the same location.");
        if (Button("Clear Entire Queue...")) confirmClear = true;
        if (confirmClear)
        {
            ImGui.TextWrapped("Remove all unfinished maps for this character?");
            if (Button("Confirm Clear Queue")) { Send(TreasureAction.Clear); confirmClear = false; }
            if (Button("Cancel Clear")) confirmClear = false;
        }
        ImGui.EndDisabled();
    }
    public void Queue(TreasureView v, bool compact)
    {
        PushQueueStyle();
        try { QueueBody(v, compact); }
        finally { PopQueueStyle(); }
    }
    private void QueueBody(TreasureView v, bool compact)
    {
        ImGui.PushTextWrapPos(0); ImGui.TextColored(TreasureQueuePalette.Muted, "TREASURE MAP QUEUE"); ImGui.PopTextWrapPos();
        var countLabel = $"{v.Entries.Length} remaining";
        var edge = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        var countWidth = ImGui.CalcTextSize(countLabel).X;
        if (ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X + countWidth <= edge)
        {
            ImGui.SameLine(); var cursor = ImGui.GetCursorScreenPos(); ImGui.SetCursorScreenPos(new(edge - countWidth, cursor.Y));
        }
        ImGui.TextColored(TreasureQueuePalette.Accent, countLabel);
        if (!v.Available) ImGui.TextWrapped("Log in and enable Treasure Maps to use this character's queue.");
        ImGui.BeginDisabled(!v.Available);
        var right = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
        var first = true;
        if (v.Entries.Length > 0 && ActionControl("Next Map", FontAwesomeIcon.Flag, right, ref first)) Send(TreasureAction.Next);
        if (v.CanUndo && ActionControl("Undo Complete", FontAwesomeIcon.Undo, right, ref first)) Send(TreasureAction.Undo);
        ImGui.Spacing();
        var rowWidth = ImGui.GetContentRegionAvail().X;
        var cards = Presentations(v.Entries, compact, rowWidth);
        for (var i = 0; i < v.Entries.Length; i++)
        {
            var entry = v.Entries[i]; var card = cards[i]; ImGui.PushID(card.Id);
            var start = ImGui.GetCursorScreenPos(); var width = rowWidth;
            var layout = card.Layout;
            var end = start + new Vector2(width, layout.Height);
            var draw = ImGui.GetWindowDrawList();
            var hovered = ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(start, end);
            draw.AddRectFilled(start, end, ImGui.ColorConvertFloat4ToU32(hovered ? TreasureQueuePalette.CardHover : TreasureQueuePalette.Card), 7 * ImGuiHelpers.GlobalScale);
            draw.AddRect(start, end, ImGui.ColorConvertFloat4ToU32(i == 0 ? TreasureQueuePalette.Accent with { W = .55f } : TreasureQueuePalette.Border), 7 * ImGuiHelpers.GlobalScale);
            var pad = 12 * ImGuiHelpers.GlobalScale;
            ImGui.SetCursorScreenPos(start + new Vector2(pad, pad));
            EntryLink(entry, i, v.Available, layout.LinkWidth, card.Owner, card.Location);
            if (!compact)
            {
                ImGui.SetCursorScreenPos(new(start.X + pad, ImGui.GetCursorScreenPos().Y));
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + layout.LinkWidth);
                ImGui.TextColored(TreasureQueuePalette.Muted, card.Received);
                ImGui.PopTextWrapPos();
            }
            var actions = layout.Inline
                ? start + new Vector2(width - pad - layout.ActionsWidth, pad + (layout.ContentHeight - ImGui.GetFrameHeight()) / 2)
                : start + new Vector2(pad, pad + layout.LinkHeight + 10 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorScreenPos(actions);
            RowActions(entry, i, v.Entries.Length, end.X - pad);
            ImGui.SetCursorScreenPos(new(start.X, end.Y + Math.Max(0, 10 * ImGuiHelpers.GlobalScale - ImGui.GetStyle().ItemSpacing.Y)));
            ImGui.Dummy(Vector2.Zero); ImGui.PopID();
        }
        if (v.Entries.Length == 0)
        {
            var start = ImGui.GetCursorScreenPos(); var width = ImGui.GetContentRegionAvail().X;
            var pad = 12 * ImGuiHelpers.GlobalScale;
            const string empty = "No unfinished maps. Party flags appear here when capture is enabled.";
            var height = ImGui.CalcTextSize(empty, false, Math.Max(1, width - 2 * pad)).Y + 2 * pad;
            ImGui.GetWindowDrawList().AddRectFilled(start, start + new Vector2(width, height), ImGui.ColorConvertFloat4ToU32(TreasureQueuePalette.Card), 7 * ImGuiHelpers.GlobalScale);
            ImGui.SetCursorScreenPos(start + new Vector2(pad));
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + width - 2 * pad); ImGui.TextUnformatted(empty); ImGui.PopTextWrapPos();
            ImGui.SetCursorScreenPos(new(start.X, start.Y + height)); ImGui.Dummy(Vector2.Zero);
        }
        if (v.Notice.Length > 0) { ImGui.Spacing(); ImGui.TextWrapped(v.Notice); }
        ImGui.EndDisabled();
    }
    internal void RowActions(TreasureEntry entry, int index, int count, float right)
    {
            var first = true;
            var left = ImGui.GetCursorScreenPos().X;
            if (ActionControl("Open Flag", FontAwesomeIcon.Flag, right, ref first, left: left)) request(new(TreasureAction.Open, entry.Id));
            if (ActionControl("Share", FontAwesomeIcon.ShareAlt, right, ref first, left: left)) request(new(TreasureAction.Share, entry.Id));
            if (ActionControl("Complete", FontAwesomeIcon.Check, right, ref first, left: left)) request(new(TreasureAction.Complete, entry.Id));
            Arrow("Move Up", ImGuiDir.Up, TreasureAction.Up, entry.Id, index == 0, right, ref first, left);
            Arrow("Move Down", ImGuiDir.Down, TreasureAction.Down, entry.Id, index == count - 1, right, ref first, left);
            if (ActionControl("More...", FontAwesomeIcon.EllipsisH, right, ref first, false, left)) ImGui.OpenPopup("queue-actions");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("More actions");
            // MenuItem supplies intrinsic text widths. BeginPopup creates an independent
            // auto-sizing popup window and lets ImGui place it inside the viewport.
            if (ImGui.BeginPopup("queue-actions", ImGuiWindowFlags.AlwaysAutoResize))
            {
                if (IconMenuItem("Delete", FontAwesomeIcon.Trash)) request(new(TreasureAction.Delete, entry.Id));
                ImGui.BeginDisabled(index == 0);
                if (IconMenuItem("Move Up", FontAwesomeIcon.ArrowUp) && index > 0) request(new(TreasureAction.Up, entry.Id));
                ImGui.EndDisabled();
                ImGui.BeginDisabled(index == count - 1);
                if (IconMenuItem("Move Down", FontAwesomeIcon.ArrowDown) && index < count - 1) request(new(TreasureAction.Down, entry.Id));
                ImGui.EndDisabled();
                if (IconMenuItem("Add another", FontAwesomeIcon.Plus)) request(new(TreasureAction.AddCopy, entry.Id));
                ImGui.EndPopup();
            }
    }
    private void Toggle(string label, bool initial, Action<bool> set)
    {
        var value = initial;
        if (ImGui.Checkbox("##" + label, ref value)) { set(value); changed(); save(); }
        ImGui.SameLine(); ImGui.TextWrapped(label);
    }
    private static bool Button(string label) => ImGui.Button(label, new Vector2(-1, 28 * ImGuiHelpers.GlobalScale));
    private bool ActionControl(string label, FontAwesomeIcon icon, float right, ref bool first, bool text = true, float left = float.NaN)
    {
        var caption = Caption(label, icon, text);
        var width = MathF.Ceiling(text ? ImGui.CalcTextSize(caption).X + 2 * ImGui.GetStyle().FramePadding.X : ImGui.GetFrameHeight());
        PlaceControl(width, right, ref first, left);
        var clicked = ImGui.Button(captions[label].Widget, new(width, ImGui.GetFrameHeight()));
        var min = ImGui.GetItemRectMin(); var size = ImGui.GetItemRectSize();
        ImGui.PushFont(icons()); var glyph = Glyphs[icon]; var measured = ImGui.CalcTextSize(glyph);
        ImGui.GetWindowDrawList().AddText(new(min.X + (text ? ImGui.GetStyle().FramePadding.X : (size.X - measured.X) / 2),
            min.Y + (size.Y - measured.Y) / 2), ImGui.GetColorU32(ImGuiCol.Text), glyph);
        ImGui.PopFont(); return clicked;
    }
    private void Arrow(string label, ImGuiDir direction, TreasureAction action, Guid id, bool disabled, float right, ref bool first, float left = float.NaN)
    {
        PlaceControl(MathF.Ceiling(ImGui.GetFrameHeight()), right, ref first, left);
        ImGui.BeginDisabled(disabled);
        if (ImGui.ArrowButton(label, direction) && !disabled) request(new(action, id));
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(label);
    }
    private static void PlaceControl(float width, float right, ref bool first, float left = float.NaN)
    {
        if (!first && ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X + width <= right)
            ImGui.SameLine();
        else if (!first && float.IsFinite(left)) ImGui.SetCursorScreenPos(new(left, ImGui.GetCursorScreenPos().Y));
        first = false;
    }
    private void Send(TreasureAction action) => request(new(action));

    internal static void PushQueueStyle()
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8, 6) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(7, 6) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 6 * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupBorderSize, 1);
        ImGui.PushStyleColor(ImGuiCol.Text, TreasureQueuePalette.Text);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, TreasureQueuePalette.Muted);
        ImGui.PushStyleColor(ImGuiCol.Button, TreasureQueuePalette.Button);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, TreasureQueuePalette.ButtonHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, TreasureQueuePalette.ButtonActive);
        ImGui.PushStyleColor(ImGuiCol.Border, TreasureQueuePalette.Border);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, TreasureQueuePalette.Card);
        ImGui.PushStyleColor(ImGuiCol.Header, TreasureQueuePalette.ButtonActive);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, TreasureQueuePalette.ButtonHover);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, TreasureQueuePalette.ButtonActive);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, TreasureQueuePalette.Accent);
    }
    internal static void PopQueueStyle() { ImGui.PopStyleVar(6); ImGui.PopStyleColor(11); }
    private unsafe string Caption(string label, FontAwesomeIcon icon, bool text = true)
    {
        var key = ((nint)ImGui.GetFont().Handle, (nint)icons().Handle, ImGui.GetFontSize(), ImGui.GetStyle().ItemInnerSpacing.X);
        if (captionKey != key) { captions.Clear(); captionKey = key; }
        if (captions.TryGetValue(label, out var existing)) return existing.Display;
        if (!text) { captions[label] = ("", "###" + label); return ""; }
        ImGui.PushFont(icons()); var width = ImGui.CalcTextSize(Glyphs[icon]).X; ImGui.PopFont();
        var spaces = (int)Math.Ceiling((width + ImGui.GetStyle().ItemInnerSpacing.X) / Math.Max(1, ImGui.CalcTextSize(" ").X));
        var display = new string(' ', spaces) + label; captions[label] = (display, display + "###" + label); return display;
    }
    internal readonly record struct CardLayout(float Height, float LinkWidth, float LinkHeight, float ContentHeight, float ActionsWidth, bool Inline);
    internal sealed record CardVisual(string Id, string Owner, string Location, string Received, CardLayout Layout);
    internal unsafe CardVisual[] Presentations(TreasureEntry[] entries, bool compact, float width)
    {
        var style = ImGui.GetStyle();
        var key = (width, ImGui.GetFontSize(), ImGuiHelpers.GlobalScale, (nint)ImGui.GetFont().Handle, (nint)icons().Handle,
            style.FramePadding, style.ItemSpacing, style.ItemInnerSpacing.X, compact);
        if (ReferenceEquals(entries, presentedEntries) && presentationKey == key) return presented;
        presentationKey = key; presentedEntries = entries;
        var next = new CardVisual[entries.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i]; var owner = $"{i + 1}. {entry.Owner.Name}"; var location = entry.Location.Label;
            var received = "Received " + entry.ReceivedAt.ToLocalTime().ToString("g");
            next[i] = new(entry.Id.ToString("N"), owner, location, received, MeasureCard(owner, location, received, compact, width));
        }
        return presented = next;
    }
    internal CardLayout MeasureCard(TreasureEntry entry, int index, bool compact, float width)
        => MeasureCard($"{index + 1}. {entry.Owner.Name}", entry.Location.Label, "Received " + entry.ReceivedAt.ToLocalTime().ToString("g"), compact, width);
    private CardLayout MeasureCard(string owner, string location, string received, bool compact, float width)
    {
        var style = ImGui.GetStyle(); var scale = ImGuiHelpers.GlobalScale; var pad = 12 * scale; var gap = 10 * scale;
        var available = Math.Max(1, width - 2 * pad);
        Span<float> controls = stackalloc float[6];
        controls[0] = MathF.Ceiling(ImGui.CalcTextSize(Caption("Open Flag", FontAwesomeIcon.Flag)).X + 2 * style.FramePadding.X);
        controls[1] = MathF.Ceiling(ImGui.CalcTextSize(Caption("Share", FontAwesomeIcon.ShareAlt)).X + 2 * style.FramePadding.X);
        controls[2] = MathF.Ceiling(ImGui.CalcTextSize(Caption("Complete", FontAwesomeIcon.Check)).X + 2 * style.FramePadding.X);
        controls[3] = controls[4] = controls[5] = MathF.Ceiling(ImGui.GetFrameHeight());
        var actions = 5 * style.ItemSpacing.X + 2; foreach (var w in controls) actions += w;
        var inline = available >= 220 * scale + gap + actions;
        var linkWidth = inline ? available - actions - gap : available;
        ImGui.PushFont(icons());
        var iconWidth = Math.Max(ImGui.CalcTextSize(Glyphs[FontAwesomeIcon.GripVertical]).X, ImGui.CalcTextSize(Glyphs[FontAwesomeIcon.MapMarkerAlt]).X);
        var iconHeight = ImGui.GetFontSize(); ImGui.PopFont();
        var wrap = Math.Max(1, linkWidth - iconWidth - style.ItemInnerSpacing.X);
        var linkHeight = Math.Max(iconHeight, ImGui.CalcTextSize(owner, false, wrap).Y)
            + style.ItemSpacing.Y + Math.Max(iconHeight, ImGui.CalcTextSize(location, false, wrap).Y);
        if (!compact) linkHeight += style.ItemSpacing.Y + ImGui.CalcTextSize(received, false, linkWidth).Y;
        var rows = 1; float used = 0;
        foreach (var w in controls)
        {
            if (used > 0 && used + style.ItemSpacing.X + w > available) { rows++; used = 0; }
            used += (used > 0 ? style.ItemSpacing.X : 0) + w;
        }
        var actionHeight = rows * ImGui.GetFrameHeight() + (rows - 1) * style.ItemSpacing.Y;
        var contentHeight = inline ? Math.Max(linkHeight, ImGui.GetFrameHeight()) : linkHeight + gap + actionHeight;
        return new(contentHeight + 2 * pad, linkWidth, linkHeight, contentHeight, actions, inline);
    }

    private bool IconMenuItem(string label, FontAwesomeIcon icon)
    {
        // Keep MenuItem's intrinsic size and native hit box, reserving an icon column.
        var indent = (int)Math.Ceiling((ImGui.GetFontSize() + ImGui.GetStyle().ItemInnerSpacing.X)
            / Math.Max(1, ImGui.CalcTextSize(" ").X));
        var clicked = ImGui.MenuItem(new string(' ', indent) + label + "##" + label);
        var min = ImGui.GetItemRectMin(); var height = ImGui.GetItemRectSize().Y;
        ImGui.PushFont(icons());
        var glyph = Glyphs[icon]; var size = ImGui.CalcTextSize(glyph);
        ImGui.GetWindowDrawList().AddText(new(min.X + ImGui.GetStyle().ItemSpacing.X / 2,
            min.Y + (height - size.Y) / 2), ImGui.GetColorU32(ImGuiCol.Text), glyph);
        ImGui.PopFont();
        return clicked;
    }

    // Shared by both windows. Only explicit drops publish work; hover and previews are read-only.
    internal unsafe void EntryLink(TreasureEntry entry, int index, bool available = true, float width = 0, string? owner = null, string? location = null)
    {
        if (width <= 0) width = ImGui.GetContentRegionAvail().X;
        ImGui.BeginGroup();
        LinkLine(FontAwesomeIcon.GripVertical, owner ?? $"{index + 1}. {entry.Owner.Name}", width, TreasureQueuePalette.Text);
        LinkLine(FontAwesomeIcon.MapMarkerAlt, location ?? entry.Location.Label, width, TreasureQueuePalette.Link, true);
        ImGui.EndGroup();
        if (!available) return;
        var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Drag this map to reorder. Click to open its flag.");
        if (ImGui.IsItemHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Left)
            && ImGui.GetIO().MouseDragMaxDistanceSqr[0] < MathF.Pow(ImGui.GetIO().MouseDragThreshold, 2))
            request(new(TreasureAction.Open, entry.Id));
        if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.SourceAllowNullId))
        {
            Span<byte> id = stackalloc byte[16]; entry.Id.TryWriteBytes(id);
            ImGui.SetDragDropPayload(DragPayload, id, ImGuiCond.Once);
            ImGui.TextUnformatted(entry.Owner.Name); ImGui.TextWrapped(entry.Location.Label);
            ImGui.EndDragDropSource();
        }
        if (ImGui.BeginDragDropTarget())
        {
            var payload = ImGui.AcceptDragDropPayload(DragPayload, ImGuiDragDropFlags.AcceptBeforeDelivery);
            if (payload.Handle != null && payload.DataSize == 16)
            {
                var source = new Guid(new ReadOnlySpan<byte>(payload.Data, 16));
                if (source != entry.Id)
                {
                    var after = ImGui.GetMousePos().Y >= (min.Y + max.Y) / 2;
                    var y = after ? max.Y : min.Y;
                    ImGui.GetWindowDrawList().AddLine(new(min.X, y), new(max.X, y),
                        ImGui.GetColorU32(ImGuiCol.CheckMark), 2 * ImGuiHelpers.GlobalScale);
                    if (payload.IsDelivery()) request(new(after ? TreasureAction.MoveAfter : TreasureAction.MoveBefore, source, entry.Id));
                }
            }
            ImGui.EndDragDropTarget();
        }
    }
    private void LinkLine(FontAwesomeIcon icon, string label, float width, Vector4 colour, bool underline = false)
    {
        var start = ImGui.GetCursorScreenPos();
        ImGui.PushFont(icons());
        var column = Math.Max(ImGui.CalcTextSize(Glyphs[FontAwesomeIcon.GripVertical]).X, ImGui.CalcTextSize(Glyphs[FontAwesomeIcon.MapMarkerAlt]).X);
        ImGui.TextColored(underline ? TreasureQueuePalette.Link : TreasureQueuePalette.Muted, Glyphs[icon]); ImGui.PopFont();
        ImGui.SameLine(); var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new(start.X + column + ImGui.GetStyle().ItemInnerSpacing.X, cursor.Y));
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + Math.Max(1, width - column - ImGui.GetStyle().ItemInnerSpacing.X));
        ImGui.TextColored(colour, label); ImGui.PopTextWrapPos();
        if (underline && ImGui.GetItemRectSize().Y <= ImGui.GetTextLineHeight() + 1)
        {
            var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddLine(new(min.X, max.Y - 1), new(max.X, max.Y - 1), ImGui.ColorConvertFloat4ToU32(colour));
        }
    }
}
