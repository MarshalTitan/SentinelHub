using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using SentinelCore.UI;
using SentinelHub.TreasureMaps;
namespace SentinelHub.UI;

public sealed class TreasureMapsPanel(TreasureSettings settings, Func<TreasureView> view,
    Action<TreasureAction, Guid> request, Action changed, Action save)
{
    private bool confirmClear;
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
        ImGui.TextWrapped($"TREASURE MAP QUEUE — {v.Entries.Length} Remaining");
        if (!v.Available) ImGui.TextWrapped("Log in and enable Treasure Maps to use this character's queue.");
        ImGui.BeginDisabled(!v.Available);
        if (v.Entries.Length > 0 && Button("Next Map")) Send(TreasureAction.Next);
        if (v.CanUndo && Button("Undo Complete")) Send(TreasureAction.Undo);
        var count = compact ? Math.Min(v.Entries.Length, 4) : v.Entries.Length;
        for (var i = 0; i < count; i++)
        {
            var entry = v.Entries[i]; ImGui.PushID(entry.Id.ToString("N"));
            // Auto-height child rows allow wrapped owner/location text at narrow widths.
            ImGui.Separator();
            ImGui.TextWrapped($"{i + 1}. {entry.Owner.Name}"); ImGui.TextWrapped(entry.Location.Label);
            if (!compact) ImGui.TextWrapped("Received " + entry.ReceivedAt.ToLocalTime().ToString("g"));
            if (!compact || i == 0)
            {
                var openWidth = ImGui.CalcTextSize("Open Flag").X + 24;
                var shareWidth = ImGui.CalcTextSize("Share").X + 24;
                var completeWidth = ImGui.CalcTextSize("Complete").X + 24;
                var inline = ImGui.GetContentRegionAvail().X >= openWidth + shareWidth + completeWidth + 2 * ImGui.GetStyle().ItemSpacing.X;
                if (inline)
                {
                    if (ImGui.Button("Open Flag", new(openWidth, 0))) request(TreasureAction.Open, entry.Id);
                    ImGui.SameLine(); if (ImGui.Button("Share", new(shareWidth, 0))) request(TreasureAction.Share, entry.Id);
                    ImGui.SameLine(); if (ImGui.Button("Complete", new(completeWidth, 0))) request(TreasureAction.Complete, entry.Id);
                }
                else
                {
                    RowButton("Open Flag", TreasureAction.Open, entry.Id);
                    RowButton("Share", TreasureAction.Share, entry.Id);
                    RowButton("Complete", TreasureAction.Complete, entry.Id);
                }
                if (!compact)
                {
                    if (ImGui.Button("More...")) ImGui.OpenPopup("queue-actions");
                    if (ImGui.BeginPopup("queue-actions"))
                    {
                        RowButton("Delete", TreasureAction.Delete, entry.Id);
                        ImGui.BeginDisabled(i == 0); RowButton("Move Up", TreasureAction.Up, entry.Id); ImGui.EndDisabled();
                        ImGui.BeginDisabled(i == v.Entries.Length - 1); RowButton("Move Down", TreasureAction.Down, entry.Id); ImGui.EndDisabled();
                        RowButton("Add another", TreasureAction.AddCopy, entry.Id);
                        ImGui.EndPopup();
                    }
                }
            }
            ImGui.PopID(); ImGui.Spacing();
        }
        if (compact && v.Entries.Length > count) ImGui.TextWrapped($"+ {v.Entries.Length - count} more in Treasure Maps");
        if (v.Entries.Length == 0) ImGui.TextWrapped("No unfinished maps. Party flags appear here when capture is enabled.");
        if (v.Notice.Length > 0) ImGui.TextWrapped(v.Notice);
        ImGui.EndDisabled();
    }
    private void Toggle(string label, bool initial, Action<bool> set)
    {
        var value = initial;
        if (ImGui.Checkbox("##" + label, ref value)) { set(value); changed(); save(); }
        ImGui.SameLine(); ImGui.TextWrapped(label);
    }
    private static bool Button(string label) => ImGui.Button(label, new Vector2(-1, 28 * ImGuiHelpers.GlobalScale));
    private void RowButton(string label, TreasureAction action, Guid id) { if (Button(label)) request(action, id); }
    private void Send(TreasureAction action) => request(action, Guid.Empty);
}
