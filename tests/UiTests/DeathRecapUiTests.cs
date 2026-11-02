using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SentinelHub.Configuration;
using SentinelHub.DeathRecaps;
using SentinelHub.UI;

internal static unsafe class DeathRecapUiTests
{
    private static void Check(bool value, string why) { if (!value) throw new Exception(why); }
    public static void Integration()
    {
        PacketFixture();
        DeathLifecycle();
        var available = false; var version = 1; var reads = 0; var removes = 0;
        var r = DeathPreview.Create(); var t = r.Threat! with { Territory = 123, LocalEntityId = 42 };
        var json = JsonSerializer.Serialize(new ThreatEnvelope(1, Guid.NewGuid(), [t]));
        Action<long>? notify = null;
        var pi = IdlePerformanceTests.Fake<IDalamudPluginInterface>((m, args) => {
            if (m.Name == "GetPluginConfigDirectory") return Path.Combine(Path.GetTempPath(), "SentinelDeathFixture-" + Guid.NewGuid().ToString("N"));
            if (m.Name == "GetIpcSubscriber") {
                var endpoint = (string)args![0]!;
                return IdlePerformanceTests.Proxy(m.ReturnType, (method, values) => {
                    if (method.Name == "Subscribe") { notify = (Action<long>)values![0]!; return null; }
                    if (method.Name == "Unsubscribe") { removes++; return null; }
                    if (method.Name == "InvokeFunc") { reads++; if (!available) throw new InvalidOperationException(); return endpoint.EndsWith("Version") ? version : json; }
                    return IdlePerformanceTests.Default(method.ReturnType);
                });
            }
            return IdlePerformanceTests.Default(m.ReturnType);
        });
        using (var bridge = new HudDeathBridge(pi)) {
            Check(bridge.Find(123, 42, t.At) is null, "Unavailable HUD fabricated data");
            available = true; notify!(1); Check(bridge.Find(123, 42, t.At)?.Id == t.Id, "HUD reload did not recover");
            Check(bridge.Find(124, 42, t.At) is null && bridge.Find(123, 43, t.At) is null && bridge.Find(123, 42, t.At.AddSeconds(5)) is null, "Cross-session/territory/stale data accepted");
            version = 2; notify!(2); Check(bridge.Find(123, 42, t.At) is null, "Incompatible IPC accepted");
            version = 1; notify!(3); Check(bridge.Find(123, 42, t.At) is not null, "Compatible IPC failed after reload");
            bridge.Reset(); available = false; Check(bridge.Find(123, 42, t.At) is null, "Provider unload retained old death");
        }
        Check(removes == 1, "IPC subscription leaked");
        var gameReads = 0; var subscriptions = 0;
        object? Services(MethodInfo m, object?[]? _) {
            if (m.Name.StartsWith("add_")) subscriptions++;
            if (m.Name.StartsWith("remove_")) subscriptions--;
            if (m.Name.StartsWith("get_")) gameReads++;
            return IdlePerformanceTests.Default(m.ReturnType);
        }
        var client = IdlePerformanceTests.Fake<IClientState>(Services);
        var condition = IdlePerformanceTests.Fake<ICondition>(Services);
        var settings = new DeathSettings { Enabled = false };
        var controller = new DeathController(settings, pi, client, condition, IdlePerformanceTests.Fake<IObjectTable>(Services),
            IdlePerformanceTests.Fake<IPartyList>(Services), IdlePerformanceTests.Fake<IPlayerState>(Services),
            IdlePerformanceTests.Fake<IDataManager>(Services), IdlePerformanceTests.Fake<IGameInteropProvider>(Services), IdlePerformanceTests.Fake<ISigScanner>(Services), () => []);
        controller.Tick(); for (var i = 0; i < 200; i++) controller.Tick(); gameReads = 0;
        var before = GC.GetAllocatedBytesForCurrentThread(); var oldView = controller.View;
        for (var i = 0; i < 10000; i++) controller.Tick();
        Check(GC.GetAllocatedBytesForCurrentThread() - before == 0 && gameReads == 0 && ReferenceEquals(oldView, controller.View), "Disabled Death Recaps has idle work/allocations");
        controller.Request("preview-pvp"); controller.Tick(); Check(controller.View.Preview && controller.View.History.Length == 0, "Preview entered saved history");
        controller.Request("end-preview"); controller.Tick(); Check(!controller.View.Preview, "Preview did not clear");
        controller.Dispose(); Check(subscriptions == 0, "Death controller lifecycle leaked subscriptions");
        Console.WriteLine("PASS Death Recaps IPC unavailable/reload/version/session handling, disposal and 10,000 dormant ticks with zero allocations/service reads.");
    }
    private static void PacketFixture()
    {
        using var capture = new IncomingActionCapture(IdlePerformanceTests.Fake<IGameInteropProvider>(), IdlePerformanceTests.Fake<ISigScanner>());
        capture.Watch([42]);
        FFXIVClientStructs.FFXIV.Client.Game.Character.ActionEffectHandler.Header header = default;
        header.ActionType = 1; header.ActionId = 123; header.GlobalSequence = 456; header.NumTargets = 1;
        FFXIVClientStructs.FFXIV.Client.Game.Character.ActionEffectHandler.TargetEffects effects = default;
        effects.Effects[0].Type = 3; effects.Effects[0].Value = 7; effects.Effects[0].Param3 = 2; effects.Effects[0].Param4 = 0x40;
        effects.Effects[1].Type = 4; effects.Effects[1].Value = 500;
        effects.Effects[2].Type = 3; effects.Effects[2].Value = 999; effects.Effects[2].Param4 = 0x80;
        FFXIVClientStructs.FFXIV.Client.Game.Object.GameObjectId target = default; target.ObjectId = 42;
        capture.ObservePacket(99, &header, &effects, &target);
        Check(capture.TryRead(out var hit) && hit.Target == 42 && hit.Source == 99 && hit.Action == 123 && hit.Sequence == 456 && hit.Amount == 131079 && hit.Kind == RecapEventKind.Damage, "Native action layout or amount decoding failed");
        Check(capture.TryRead(out var heal) && heal.Kind == RecapEventKind.Healing && heal.Amount == 500 && !capture.TryRead(out _), "Healing/reflected effect filtering failed");
        target.ObjectId = 43; capture.ObservePacket(99, &header, &effects, &target); Check(!capture.TryRead(out _), "Unwatched target captured");
        target.ObjectId = 42; header.NumTargets = 33; capture.ObservePacket(99, &header, &effects, &target); Check(!capture.TryRead(out _), "Oversized packet accepted");
        header.NumTargets = 1; header.ActionType = 2; capture.ObservePacket(99, &header, &effects, &target); Check(!capture.TryRead(out _), "Unsupported action type accepted");
        header.ActionType = 1;
        for (var i = 0; i < 600; i++) capture.ObservePacket(99, &header, &effects, &target);
        Check(capture.Dropped == 176, "Native handoff did not enforce its 1024-event bound");
        capture.SetEnabled(false, false); Check(!capture.TryRead(out _), "Native disable retained stale events");
        Console.WriteLine("PASS experimental action packet memory layout, amount extension, healing, reflected/unwatched/type rejection and bounded handoff. This is not in-game attribution validation.");
    }
    private static void DeathLifecycle()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SentinelDeathLifecycle-" + Guid.NewGuid().ToString("N"));
        long clock = 0; var origin = DateTimeOffset.UtcNow; uint hp = 100; var inCombat = true;
        var name = new Dalamud.Game.Text.SeStringHandling.SeString(new List<Dalamud.Game.Text.SeStringHandling.Payload> { new Dalamud.Game.Text.SeStringHandling.Payloads.TextPayload("Private fixture name") });
        var actor = IdlePerformanceTests.Fake<Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter>((m, _) => m.Name switch {
            "get_EntityId" => 42u, "get_CurrentHp" => hp, "get_MaxHp" => 100u, "get_Name" => name,
            _ => IdlePerformanceTests.Default(m.ReturnType) });
        var pi = IdlePerformanceTests.Fake<IDalamudPluginInterface>((m, _) => m.Name == "GetPluginConfigDirectory" ? dir : IdlePerformanceTests.Default(m.ReturnType));
        var client = IdlePerformanceTests.Fake<IClientState>((m, _) => m.Name == "get_IsLoggedIn" ? true : IdlePerformanceTests.Default(m.ReturnType));
        var player = IdlePerformanceTests.Fake<IPlayerState>((m, _) => m.Name == "get_IsLoaded" ? true : m.Name == "get_ContentId" ? 123UL : IdlePerformanceTests.Default(m.ReturnType));
        var objects = IdlePerformanceTests.Fake<IObjectTable>((m, _) => m.Name == "get_LocalPlayer" ? actor : IdlePerformanceTests.Default(m.ReturnType));
        var condition = IdlePerformanceTests.Fake<ICondition>((m, a) => m.Name == "get_Item" ? (Dalamud.Game.ClientState.Conditions.ConditionFlag)a![0]! switch {
            Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty => true,
            Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat => inCombat, _ => false } : IdlePerformanceTests.Default(m.ReturnType));
        var settings = new DeathSettings { CaptureStatuses = false };
        DeathController Make() => new(settings, pi, client, condition, objects, IdlePerformanceTests.Fake<IPartyList>(), player,
            IdlePerformanceTests.Fake<IDataManager>(), IdlePerformanceTests.Fake<IGameInteropProvider>(), IdlePerformanceTests.Fake<ISigScanner>(), () => [], () => clock, () => origin.AddMilliseconds(clock));
        using (var c = Make()) {
            c.Tick(); hp = 50; clock = 125; c.Tick(); hp = 0; clock = 250; c.Tick();
            clock = 600; c.Tick(); Check(c.View.History.Length == 1 && c.View.Selected!.Fatal is null, "PvE death not recorded / fatal source fabricated");
            hp = 100; clock = 750; c.Tick(); hp = 0; clock = 875; c.Tick();
            hp = 100; clock = 1000; c.Tick(); clock = 1250; c.Tick();
            Check(c.View.History.Length == 2 && c.View.History[0].Events.Any(e => e.Hp == 0), "Rapid respawn lost detected death evidence");
            clock = 5000; c.Tick(); Check(!Directory.Exists(dir), "History wrote during combat");
        }
        var path = Path.Combine(dir, "death-recaps", "000000000000007B.json");
        Check(File.Exists(path) && !File.ReadAllText(path).Contains("Private fixture name"), "Unload persistence/redaction failed");
        using (var restored = Make()) { restored.Tick(); Check(restored.View.History.Length == 2, "Restart did not recover local history"); restored.Request("clear"); restored.Tick(); }
        var saved = new DeathHistory(); Check(saved.Load(File.ReadAllText(path), 30) && saved.Snapshot.Length == 0, "Clear did not persist");
        // Only files explicitly created in the unique fixture directory are removed.
        File.Delete(path); Directory.Delete(Path.GetDirectoryName(path)!); Directory.Delete(dir);
        Console.WriteLine("PASS observed PvE deaths, rapid respawn evidence, combat write deferral, unload/restart persistence, redaction and clear.");
    }
    public static void Run(ImFontPtr font)
    {
        var io = ImGui.GetIO(); var oldSize = io.DisplaySize; var oldScale = io.FontGlobalScale; var cases = 0;
        try {
            foreach (var theme in new[] { 0, 1 }) foreach (var scale in new[] { 1f, 1.2f, 1.5f, 2f })
            foreach (var width in new[] { 340f, 600f }) foreach (var pvp in new[] { false, true }) {
                io.DisplaySize = new(2400, 1800); io.FontGlobalScale = scale; io.AddMousePosEvent(-100, -100);
                var settings = new HubSettings { Theme = theme, ReducedMotion = true };
                var record = DeathPreview.Create(pvp); var view = new DeathView([], record, "Fixture capture", "Fixture HUD", true);
                var reads = 0; var actions = 0; var opens = 0;
                DeathView View() { reads++; return view; }
                using var popup = new DeathRecapWindow(settings, View, (_, _) => actions++, () => { }, () => { }, () => opens++, () => true, iconFont: () => font);
                popup.Show(record); var initial = JsonSerializer.Serialize(settings); Vector2 close = default;
                void Frame(bool full = false) {
                    ImGui.NewFrame(); var c = ImGui.GetCurrentContext(); var vars = c.StyleVarStack.Size; var colours = c.ColorStack.Size; var fonts = c.FontStack.Size;
                    popup.PreOpenCheck();
                    if (popup.IsOpen || full) {
                        if (!full) popup.PreDraw();
                        ImGui.SetNextWindowPos(new(40, 40), ImGuiCond.Always); ImGui.SetNextWindowSize(new Vector2(width, 560) * scale, ImGuiCond.Always);
                        var open = true;
                        var flags = full ? ImGuiWindowFlags.None : popup.Flags;
                        if (ImGui.Begin(full ? "DeathFullFixture" : DeathRecapWindow.WindowId, ref open, flags)) {
                            if (full) new DeathRecapsPanel(settings.DeathRecaps, View, (_, _) => actions++, () => { }, () => { }).Draw(); else popup.Draw();
                        }
                        var root = c.CurrentWindow; close = root.Pos + new Vector2(root.Size.X - 29 * scale, 28 * scale);
                        ImGui.End(); if (!full) { if (!open) popup.IsOpen = false; popup.PostDraw(); }
                    }
                    Check(c.StyleVarStack.Size == vars && c.ColorStack.Size == colours && c.FontStack.Size == fonts, "Death recap style/font stack leaked");
                    ImGui.Render();
                }
                for (var i = 0; i < 4; i++) Frame();
                var windows = ImGui.GetCurrentContext().Windows;
                for (var i = 0; i < windows.Size; i++) {
                    var w = windows[i]; var name = Marshal.PtrToStringUTF8((nint)w.Name) ?? "";
                    if (!name.StartsWith(DeathRecapWindow.WindowId) || !w.Active) continue;
                    Check(!w.ScrollbarX && w.ContentSize.X <= w.Size.X + 2, "Death popup content overflows horizontally");
                    Check(w.ContentSize.Y <= w.Size.Y + 2 || w.ScrollMax.Y > 0, "Death popup is clipped without scrolling");
                }
                if (theme == 1) {
                    io.AddMousePosEvent(close.X, close.Y); Frame(); io.AddMouseButtonEvent(0, true); Frame(); io.AddMouseButtonEvent(0, false); Frame();
                    Check(!popup.IsOpen, "Core death popup X did not close");
                } else popup.IsOpen = false;
                var closedReads = reads; Frame(); Frame(); Check(reads == closedReads && actions == 0, "Closed popup rendered or sent an action");
                Check(JsonSerializer.Serialize(settings) == initial, "Death drawing changed saved settings");
                Frame(true); Frame(true);
                foreach (var mode in Enum.GetValues<DeathPopupMode>()) {
                    settings.DeathRecaps.Popup = mode; popup.IsOpen = false; popup.Show(record with { Synthetic = false });
                    Check(popup.IsOpen == (mode == DeathPopupMode.Both || mode == DeathPopupMode.PvE && !pvp || mode == DeathPopupMode.PvP && pvp), "Popup content filter incorrect");
                }
                popup.IsOpen = false; cases++;
            }
        } finally { io.DisplaySize = oldSize; io.FontGlobalScale = oldScale; io.AddMousePosEvent(-100, -100); }
        Console.WriteLine($"PASS {cases} native death popup/full-page theme/scale/width/PvE/PvP cases, X, mode filters and dormant closed rendering.");
    }
}
