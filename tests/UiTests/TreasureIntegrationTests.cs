using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using SentinelHub.TreasureMaps;

internal static class TreasureIntegrationTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        var owner = new TreasureOwner("Queue Owner", 42);
        var link = new MapLinkPayload(100, 401, -123456, 987654);
        var message = new SeString(new List<Payload> { new TextPayload("Map "), link, new TextPayload(" here") });
        var decoded = SeString.Parse(message.Encode());
        var flags = TreasureLinks.Extract(owner, decoded);
        Check(flags.Count == 1 && flags[0].MapId == 401 && flags[0].TerritoryId == 100
            && flags[0].RawX == -123456 && flags[0].RawY == 987654, "Structured map-link raw identity lost");
        var location = new TreasureLocation(flags[0].TerritoryId, flags[0].MapId, flags[0].RawX, flags[0].RawY,
            "Fixture", 24.2f, 18.5f, flags[0].Payload);
        var restored = TreasureLinks.Restore(location);
        Check(restored.DataString == link.DataString && restored.Encode().SequenceEqual(flags[0].Payload), "Saved link did not round-trip exactly");
        Check(TreasureLinks.Extract(owner, new SeString(new List<Payload> { new TextPayload("Zone (24.2, 18.5)") })).Count == 0,
            "Coordinate-looking text was treated as a map payload");
        Check(TreasureLinks.Capture(10, new SeString(), decoded, true, true, true).Count == 0,
            "Unrelated chat channel captured");
        Check(TreasureLinks.Capture(14, new SeString(), decoded, true, true, false).Count == 0,
            "A non-player label was invented as an owner");
        Lifecycle();
        DisplaySettings();
        Console.WriteLine("PASS Treasure structured payload round-trip, text/channel rejection, shared controller queue, lifecycle cleanup and zero-allocation dormant ticks.");
    }
    private static void DisplaySettings()
    {
        foreach (var after in new[] { false, true }) foreach (var viewed in new[] { false, true })
        foreach (var order in new[] { 0, 1, 2 }) // added first, displayed first, existing-map view only
        {
            long now = 100; var opened = 0; var copied = new List<string>(); var posted = 0;
            var local = IdlePerformanceTests.Fake<IPlayerCharacter>((m, _) => m.Name == "get_Name" ? new SeString(new List<Payload> { new TextPayload("Own Map") }) : IdlePerformanceTests.Default(m.ReturnType));
            var client = IdlePerformanceTests.Fake<IClientState>((m, _) => m.Name == "get_IsLoggedIn" ? true : IdlePerformanceTests.Default(m.ReturnType));
            var player = IdlePerformanceTests.Fake<IPlayerState>((m, _) => m.Name == "get_IsLoaded" ? true : m.Name == "get_ContentId" ? 789UL : IdlePerformanceTests.Default(m.ReturnType));
            var objects = IdlePerformanceTests.Fake<IObjectTable>((m, _) => m.Name == "get_LocalPlayer" ? local : IdlePerformanceTests.Default(m.ReturnType));
            var framework = IdlePerformanceTests.Fake<IFramework>((m, _) => m.Name == "get_IsInFrameworkUpdateThread" ? true : IdlePerformanceTests.Default(m.ReturnType));
            var gui = IdlePerformanceTests.Fake<IGameGui>((m, args) => {
                if (m.Name == "OpenMapWithMapLink") { opened++; Check(((MapLinkPayload)args![0]!).DataString == "m:100,401,-123456,987654", "Open changed the map variant or raw flag"); return true; }
                return IdlePerformanceTests.Default(m.ReturnType);
            });
            var chat = IdlePerformanceTests.Fake<IChatGui>((m, _) => { if (m.Name.StartsWith("Print")) posted++; return IdlePerformanceTests.Default(m.ReturnType); });
            var location = new TreasureLocation(100, 401, -123456, 987654, "Fixture", 24.2f, 18.5f, []);
            var settings = new TreasureSettings { Enabled = true, DetectDeciphered = true, OpenAfterDecipher = after, OpenWhenViewed = viewed };
            using var controller = new TreasureController(settings, chat, client, player, objects, framework, gui,
                IdlePerformanceTests.Fake<IGameInventory>(), IdlePerformanceTests.Fake<IDataManager>(),
                IdlePerformanceTests.Fake<ISigScanner>(), IdlePerformanceTests.Fake<IGameInteropProvider>(), () => { }, copied.Add,
                (rank, spot) => rank == 3 && spot == 0 ? new(3, 0, 2001000, location) : null,
                value => value.Valid, () => now);
            controller.Tick();
            if (order == 0) controller.ObserveKeyAdded(2001000);
            controller.ObserveMapView(3, 0);
            if (order == 1) controller.ObserveKeyAdded(2001000);
            controller.Tick(); now = 500; controller.Tick();
            Check(opened == (order == 2 ? viewed ? 1 : 0 : after ? 1 : 0), "Decipher/view settings changed with notification order");
            Check(controller.View.Entries.Length == 1 && controller.View.Current?.Location.MapId == 401, "Own map not retained/queued");
            var before = opened; controller.Request(TreasureAction.Share, controller.View.Entries[0].Id); controller.Tick();
            Check(opened == before + 1 && copied.SequenceEqual(new[] { "/p <flag>" }) && posted == 0,
                "Share failed to prepare the real flag or posted without manual sending");
        }
        Console.WriteLine("PASS 12 decipher/view setting and inventory-event-order cases, exact map opening and explicit manual Share preparation.");
    }
    private static void Lifecycle()
    {
        var subscriptions = 0; var removals = 0; var reads = 0; var saved = 0; var loggedIn = true;
        var cid = 123UL; var frameworkThread = true; Delegate? logout = null; Delegate? login = null;
        var local = IdlePerformanceTests.Fake<IPlayerCharacter>((m, _) => m.Name == "get_Name" ? new SeString(new List<Payload> { new TextPayload("Queue Owner") }) : IdlePerformanceTests.Default(m.ReturnType));
        var client = IdlePerformanceTests.Fake<IClientState>((m, args) => {
            if (m.Name.StartsWith("add_")) { subscriptions++; if (m.Name == "add_Logout") logout = (Delegate)args![0]!; if (m.Name == "add_Login") login = (Delegate)args![0]!; }
            if (m.Name.StartsWith("remove_")) removals++;
            if (m.Name == "get_IsLoggedIn") { reads++; return loggedIn; }
            return IdlePerformanceTests.Default(m.ReturnType);
        });
        var player = IdlePerformanceTests.Fake<IPlayerState>((m, _) => { reads++; return m.Name == "get_IsLoaded" ? true : m.Name == "get_ContentId" ? cid : IdlePerformanceTests.Default(m.ReturnType); });
        var objects = IdlePerformanceTests.Fake<IObjectTable>((m, _) => { reads++; return m.Name == "get_LocalPlayer" ? local : IdlePerformanceTests.Default(m.ReturnType); });
        var framework = IdlePerformanceTests.Fake<IFramework>((m, _) => { reads++; return m.Name == "get_IsInFrameworkUpdateThread" ? frameworkThread : IdlePerformanceTests.Default(m.ReturnType); });
        object? Events(System.Reflection.MethodInfo m, object?[]? _) {
            if (m.Name.StartsWith("add_")) subscriptions++;
            if (m.Name.StartsWith("remove_")) removals++;
            return IdlePerformanceTests.Default(m.ReturnType);
        }
        var chat = IdlePerformanceTests.Fake<IChatGui>(Events);
        var inventory = IdlePerformanceTests.Fake<IGameInventory>(Events);
        var settings = new TreasureSettings { Enabled = true, DetectDeciphered = false };
        var first = new CharacterTreasureQueue(); var q = new TreasureQueue(); q.Bind(first);
        q.Add(new("Queue Owner", 42), new(100, 401, 123, 456, "Fixture", 24.2f, 18.5f, []), DateTimeOffset.UtcNow, 50);
        settings.Characters[cid.ToString("X16")] = first;
        using var controller = new TreasureController(settings, chat, client, player, objects, framework,
            IdlePerformanceTests.Fake<IGameGui>(), inventory, IdlePerformanceTests.Fake<IDataManager>(),
            IdlePerformanceTests.Fake<ISigScanner>(), IdlePerformanceTests.Fake<IGameInteropProvider>(), () => saved++, _ => throw new Exception("Unexpected clipboard action"));
        controller.Tick(); Check(controller.View.Available && controller.View.Entries.Length == 1, "Saved queue not restored");
        reads = 0; var bytes = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++) controller.Tick();
        Check(reads == 0 && GC.GetAllocatedBytesForCurrentThread() == bytes, "Enabled idle Treasure Maps performed reads or allocations");
        IdlePerformanceTests.Measure("treasure-enabled-idle", controller.Tick);
        var original = controller.View.Entries[0].Id;
        frameworkThread = false; controller.Request(TreasureAction.Complete, original); controller.Tick();
        Check(controller.View.Entries.Length == 1, "Queue actions ran off the framework thread");
        frameworkThread = true; controller.Tick();
        Check(controller.View.Entries.Length == 0 && controller.View.CanUndo, "Complete did not remove from shared view");
        controller.Request(TreasureAction.Undo); controller.Tick();
        Check(controller.View.Entries.Single().Id == original, "Undo did not restore identity");
        controller.Request(TreasureAction.Delete, original); // queued against the previous session
        loggedIn = false; logout!.DynamicInvoke(0, 0); controller.Tick();
        Check(!controller.View.Available && controller.View.Entries.Length == 0 && first.Entries.Count == 1,
            "Logout lost saved maps or accepted a stale queued action");
        cid = 456; loggedIn = true; login!.DynamicInvoke(); controller.Tick();
        Check(controller.View.Available && controller.View.Entries.Length == 0, "Character queues mixed");
        settings.Enabled = false; controller.Configure(); controller.Tick();
        reads = 0; bytes = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10000; i++) controller.Tick();
        Check(reads == 0 && GC.GetAllocatedBytesForCurrentThread() == bytes, "Disabled idle Treasure Maps did work");
        controller.Dispose(); controller.Tick();
        Check(subscriptions == removals && saved > 0, "Subscriptions leaked or pending persistence was not flushed");
    }
}
