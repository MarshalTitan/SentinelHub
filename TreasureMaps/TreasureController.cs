using System.Collections.Concurrent;
using Dalamud.Game.Chat;
using Dalamud.Game.Inventory;
using Dalamud.Game.Inventory.InventoryEventArgTypes;
using Dalamud.Plugin.Services;

namespace SentinelHub.TreasureMaps;

public sealed class TreasureController : IDisposable
{
    private enum WorkKind { Chat, Displayed, Added, Removed, Action }
    private sealed record Work(int Epoch, WorkKind Kind, byte[]? Sender = null, byte[]? Message = null, int Channel = 0,
        ushort Rank = 0, ushort Spot = 0, uint Item = 0, TreasureRequest Request = default, DateTimeOffset Received = default);
    private readonly TreasureSettings settings;
    private readonly IChatGui chat;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly IObjectTable objects;
    private readonly IFramework framework;
    private readonly IGameGui gui;
    private readonly IGameInventory inventory;
    private readonly Action save;
    private readonly Action<string> clipboard;
    private readonly TreasureGameData data;
    private readonly TreasureDetection detection;
    private readonly Func<uint, ushort, OwnTreasureMap?> resolveTreasure;
    private readonly Func<TreasureLocation, bool> validLocation;
    private readonly Func<long> clock;
    private readonly TreasureQueue queue = new();
    private readonly ConcurrentQueue<Work> incoming = new();
    private volatile bool sessionDirty = true, configureDirty = true, disposed;
    private int epoch, pending, dropped;
    private volatile int activeEpoch;
    private string? characterKey;
    private TreasureOwner? self;
    private long saveAt, undoUntil, addedUntil, shownAt, retryAt, retryUntil, viewDue;
    private OwnTreasureMap? pendingView;
    private uint addedItem;
    private bool decipherHandled;
    private string notice = "";
    public TreasureView View { get; private set; } = TreasureView.Empty;
    public TreasureController(TreasureSettings settings, IChatGui chat, IClientState client, IPlayerState player,
        IObjectTable objects, IFramework framework, IGameGui gui, IGameInventory inventory, IDataManager data,
        ISigScanner scanner, IGameInteropProvider interop, Action save, Action<string> clipboard,
        Func<uint, ushort, OwnTreasureMap?>? resolveTreasure = null,
        Func<TreasureLocation, bool>? validLocation = null, Func<long>? clock = null)
    {
        this.settings = settings; this.chat = chat; this.client = client; this.player = player;
        this.objects = objects; this.framework = framework; this.gui = gui; this.inventory = inventory;
        this.save = save; this.clipboard = clipboard; this.data = new(data);
        this.resolveTreasure = resolveTreasure ?? this.data.ResolveTreasure;
        this.validLocation = validLocation ?? (location => this.data.Resolve(location.TerritoryId, location.MapId, location.RawX, location.RawY, location.Payload) is not null);
        this.clock = clock ?? (() => Environment.TickCount64);
        detection = new(scanner, interop, ObserveMapView);
        chat.ChatMessage += OnChat;
        inventory.ItemAddedExplicit += OnAdded;
        inventory.ItemRemovedExplicit += OnRemoved;
        client.Login += OnSession;
        client.Logout += OnLogout;
    }
    public void Configure() { configureDirty = true; }
    public void Request(TreasureAction action, Guid id = default) => Publish(new(activeEpoch, WorkKind.Action, Request: new(action, id)));
    private void OnSession() { Interlocked.Increment(ref epoch); detection.SuspendObservation(); retryUntil = 0; sessionDirty = true; }
    private void OnLogout(int _, int __) => OnSession();
    private void OnAdded(InventoryItemAddedArgs args)
    {
        if (!disposed && settings.Enabled && settings.DetectDeciphered && args.Item.ContainerType == GameInventoryType.KeyItems)
            ObserveKeyAdded(args.Item.ItemId);
    }
    // Primitive ingress shared by native/inventory adapters. No game API is used here.
    internal void ObserveMapView(ushort rank, ushort spot) => Publish(new(activeEpoch, WorkKind.Displayed, Rank: rank, Spot: spot));
    internal void ObserveKeyAdded(uint item) => Publish(new(activeEpoch, WorkKind.Added, Item: item));
    private void OnRemoved(InventoryItemRemovedArgs args)
    {
        if (!disposed && settings.Enabled && args.Item.ContainerType == GameInventoryType.KeyItems)
            Publish(new(activeEpoch, WorkKind.Removed, Item: args.Item.ItemId));
    }
    private void OnChat(IHandleableChatMessage message)
    {
        if (disposed || characterKey is null || !TreasurePolicy.Capture((int)message.LogKind,
            settings.Enabled, settings.CaptureParty, settings.CaptureAlliance)) return;
        try
        {
            var session = activeEpoch;
            // Only copy primitive data during the callback; never retain the borrowed chat object.
            if (!message.Message.Payloads.Any(p => p is Dalamud.Game.Text.SeStringHandling.Payloads.MapLinkPayload)) return;
            var sender = message.Sender.Encode(); var text = message.Message.Encode();
            if (sender.Length <= 1024 && text.Length <= 8192)
                Publish(new(session, WorkKind.Chat, sender, text, (int)message.LogKind, Received: DateTimeOffset.UtcNow));
        }
        catch { /* Malformed chat must not affect ordinary chat delivery. */ }
    }
    private void Publish(Work work)
    {
        if (disposed) return;
        if (Interlocked.Increment(ref pending) > 128) { Interlocked.Decrement(ref pending); Interlocked.Increment(ref dropped); return; }
        incoming.Enqueue(work);
    }
    public void Tick()
    {
        // Disabled and unchanged enabled sessions read no game state, clocks, metadata or inventory.
        if (disposed || !sessionDirty && !configureDirty && Volatile.Read(ref pending) == 0 && saveAt == 0 && undoUntil == 0 && retryUntil == 0 && viewDue == 0) return;
        if (!framework.IsInFrameworkUpdateThread) return;
        var now = clock();
        var changed = false;
        if (Interlocked.Exchange(ref dropped, 0) != 0) { notice = "Too many map events arrived at once. Relink any missing flags; saved maps were not removed."; changed = true; }
        if (retryUntil != 0 && now >= retryUntil) { retryUntil = 0; retryAt = 0; }
        else if (retryUntil != 0 && now >= retryAt) { sessionDirty = true; retryAt = now + 500; }
        if (sessionDirty)
        {
            sessionDirty = false; Flush(); queue.Detach(); characterKey = null; self = null; notice = "";
            addedItem = 0; addedUntil = 0; shownAt = 0; undoUntil = 0; decipherHandled = false; pendingView = null; viewDue = 0;
            if (client.IsLoggedIn && player.IsLoaded && player.ContentId != 0 && objects.LocalPlayer is { } local)
            {
                characterKey = player.ContentId.ToString("X16"); self = new(local.Name.TextValue, local.HomeWorld.RowId);
                if (!settings.Characters.TryGetValue(characterKey, out var state))
                    settings.Characters[characterKey] = state = new();
                queue.Bind(state);
                retryUntil = retryAt = 0;
                // A one-time login check validates only the saved current key item, not all inventories.
                if (queue.Current is { } current && CheckKey(current.EventItemId) == false) { queue.Current = null; Dirty(now); }
            }
            else if (client.IsLoggedIn && retryUntil == 0) { retryAt = now + 500; retryUntil = now + 10_000; }
            activeEpoch = epoch; configureDirty = true; changed = true;
        }
        if (configureDirty)
        {
            configureDirty = false;
            detection.SetEnabled(settings.Enabled && settings.DetectDeciphered && characterKey is not null);
            if (!settings.Enabled) { Interlocked.Increment(ref epoch); activeEpoch = epoch; undoUntil = 0; queue.ClearTransient(); pendingView = null; viewDue = 0; }
            changed = true;
        }
        var processed = 0;
        while (processed++ < 32 && incoming.TryDequeue(out var work))
        {
            Interlocked.Decrement(ref pending);
            if (work.Epoch != epoch || characterKey is null || !settings.Enabled) continue;
            try { changed |= Process(work, now); }
            catch { notice = "Treasure-map action unavailable. Your saved queue has been retained."; changed = true; }
        }
        if (undoUntil != 0 && now >= undoUntil) { undoUntil = 0; changed |= queue.Expire(now); }
        if (viewDue != 0 && now >= viewDue)
        {
            var viewed = pendingView; pendingView = null; viewDue = 0;
            if (settings.Enabled && settings.DetectDeciphered && settings.OpenWhenViewed && viewed is not null)
            { try { Open(viewed.Location); } catch { notice = "The world map could not be opened right now."; } changed = true; }
        }
        if (saveAt != 0 && now >= saveAt) Flush();
        if (changed) Rebuild(now);
    }
    private bool Process(Work work, long now)
    {
        switch (work.Kind)
        {
            case WorkKind.Chat:
                var changed = false;
                foreach (var f in TreasureLinks.Capture(work.Channel,
                    Dalamud.Game.Text.SeStringHandling.SeString.Parse(work.Sender!),
                    Dalamud.Game.Text.SeStringHandling.SeString.Parse(work.Message!),
                    settings.Enabled, settings.CaptureParty, settings.CaptureAlliance))
                {
                    var location = data.Resolve(f.TerritoryId, f.MapId, f.RawX, f.RawY, f.Payload);
                    if (location is null || queue.IsRepost(f.Owner, location, now)) continue;
                    if (queue.Add(f.Owner, location, work.Received, settings.MaximumEntries)) { Dirty(now); changed = true; }
                    else if (queue.Entries.Length >= settings.MaximumEntries) { notice = "Queue limit reached; no unfinished maps were removed."; changed = true; }
                }
                return changed;
            case WorkKind.Added:
                addedItem = work.Item; addedUntil = now + 5000;
                if (queue.Current is { } recent && recent.EventItemId == work.Item && now - shownAt < 5000)
                    return OnDeciphered(recent, now);
                return false;
            case WorkKind.Removed:
                if (queue.Current?.EventItemId != work.Item) return false;
                queue.Current = null; decipherHandled = false; pendingView = null; viewDue = 0; Dirty(now); return true;
            case WorkKind.Displayed:
                if (!settings.DetectDeciphered) return false;
                var own = resolveTreasure(work.Rank, work.Spot);
                if (own is null) { notice = "This treasure location could not be resolved from current game data."; return true; }
                var different = queue.Current is null || queue.Current.RankId != own.RankId || queue.Current.SpotId != own.SpotId;
                if (different) decipherHandled = false;
                queue.Current = own; shownAt = now; Dirty(now);
                if (addedItem == own.EventItemId && now < addedUntil) return OnDeciphered(own, now);
                // Briefly defer classification so an inventory notification arriving after the
                // native display call can still obey the independent decipher/open-view settings.
                pendingView = own; viewDue = now + 250;
                if (settings.AddOwnMap && self is not null) queue.Add(self, own.Location, DateTimeOffset.UtcNow, settings.MaximumEntries);
                return true;
            case WorkKind.Action: return Act(work.Request, now);
            default: return false;
        }
    }
    private bool OnDeciphered(OwnTreasureMap map, long now)
    {
        addedItem = 0; addedUntil = 0; pendingView = null; viewDue = 0;
        if (decipherHandled) return true;
        if (settings.OpenAfterDecipher) Open(map.Location);
        if (settings.AddOwnMap && self is not null)
            queue.Add(self, map.Location, DateTimeOffset.UtcNow, settings.MaximumEntries);
        decipherHandled = true; Dirty(now); return true;
    }
    private bool Act(TreasureRequest request, long now)
    {
        var entry = queue.Entries.FirstOrDefault(e => e.Id == request.EntryId);
        switch (request.Action)
        {
            case TreasureAction.Next:
                if (queue.Entries.FirstOrDefault() is { } first) Open(first.Location); break;
            case TreasureAction.Open: if (entry is not null) Open(entry.Location); break;
            case TreasureAction.Share:
                if (entry is not null && self is not null && Open(entry.Location))
                {
                    queue.Shared(entry.Location, self, now); clipboard("/p <flag>");
                    notice = "Flag prepared for " + entry.Owner.Name + ". Paste /p <flag> into chat and press Enter to share it.";
                }
                break;
            case TreasureAction.Complete:
                if (queue.Remove(request.EntryId, true, now)) { undoUntil = now + 15_000; notice = "Map completed. Undo is available for 15 seconds."; Dirty(now); } break;
            case TreasureAction.Delete: if (queue.Remove(request.EntryId, false, now)) Dirty(now); break;
            case TreasureAction.Up: if (queue.Move(request.EntryId, -1)) Dirty(now); break;
            case TreasureAction.Down: if (queue.Move(request.EntryId, 1)) Dirty(now); break;
            case TreasureAction.Undo:
                if (queue.Undo(now, settings.MaximumEntries)) { undoUntil = 0; Dirty(now); }
                else notice = "Undo expired or the queue is full."; break;
            case TreasureAction.Clear: queue.Clear(); undoUntil = 0; Dirty(now); break;
            case TreasureAction.OpenMine:
                if (queue.Current is { } current)
                {
                    var key = CheckKey(current.EventItemId);
                    if (key == true) Open(current.Location);
                    else if (key == false) { queue.Current = null; Dirty(now); notice = "The deciphered key item is no longer available. View a treasure map to detect it."; }
                    else notice = "Key items are not ready yet. View the treasure map again after loading.";
                }
                break;
            case TreasureAction.AddMine:
                if (queue.Current is { } mine && self is not null && queue.Add(self, mine.Location, DateTimeOffset.UtcNow, settings.MaximumEntries)) Dirty(now); break;
            case TreasureAction.AddCopy:
                if (entry is not null && queue.Add(entry.Owner, entry.Location, DateTimeOffset.UtcNow, settings.MaximumEntries, additional: true)) Dirty(now); break;
        }
        return true;
    }
    private bool Open(TreasureLocation location)
    {
        if (!client.IsLoggedIn || !validLocation(location))
        { notice = "The saved map is unavailable in current game data."; return false; }
        var success = gui.OpenMapWithMapLink(TreasureLinks.Restore(location));
        notice = success ? "Map flag opened." : "The world map could not be opened right now."; return success;
    }
    private bool? CheckKey(uint item)
    {
        try { var items = inventory.GetInventoryItems(GameInventoryType.KeyItems); if (items.Length == 0) return null;
            foreach (ref readonly var slot in items) if (slot.ItemId == item && slot.Quantity > 0) return true; }
        catch { return null; }
        return false;
    }
    private void Dirty(long now) { if (saveAt == 0) saveAt = now + 300; }
    private void Flush() { if (saveAt == 0) return; saveAt = 0; save(); }
    private void Rebuild(long now) => View = new(characterKey is not null && settings.Enabled, queue.Entries,
        queue.Current, queue.CanUndo(now), detection.Status, notice);
    public void Dispose()
    {
        if (disposed) return; disposed = true; Interlocked.Increment(ref epoch);
        chat.ChatMessage -= OnChat; inventory.ItemAddedExplicit -= OnAdded; inventory.ItemRemovedExplicit -= OnRemoved;
        client.Login -= OnSession; client.Logout -= OnLogout;
        detection.Dispose(); Flush(); while (incoming.TryDequeue(out _)) { } pending = 0;
        queue.Detach(); View = TreasureView.Empty;
    }
}
